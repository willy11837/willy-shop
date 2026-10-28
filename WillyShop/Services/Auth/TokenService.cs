using System.IdentityModel.Tokens.Jwt;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WillyShop.Contracts;
using WillyShop.Data;
using WillyShop.Models;
using WillyShop.Options;

namespace WillyShop.Services.Auth;

public sealed class TokenService : ITokenService
{
    public const string RefreshTokenCookieName = "WillyShop.RefreshToken";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtOptions _options;
    private readonly IWebHostEnvironment _environment;

    public TokenService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IOptions<JwtOptions> options,
        IWebHostEnvironment environment)
    {
        _db = db;
        _userManager = userManager;
        _options = options.Value;
        _environment = environment;
    }

    public async Task<AuthResponse> IssueAsync(
        ApplicationUser user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var now = DateTime.UtcNow;
        var accessExpiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var refreshExpiresAt = now.AddDays(_options.RefreshTokenDays);
        var refreshToken = CreateSecureToken();
        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            TokenFamily = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt,
            CreatedByIp = LimitIp(ipAddress)
        };

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return CreateResponse(user, roles, accessExpiresAt, refreshToken, refreshExpiresAt);
    }

    public async Task<AuthResponse?> RotateAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var tokenHash = HashToken(refreshToken);
        var existing = await _db.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
        if (existing is null || existing.ExpiresAt <= DateTime.UtcNow)
            return null;

        if (existing.RevokedAt.HasValue)
        {
            await RevokeFamilyAsync(existing.TokenFamily, ipAddress, "偵測到 Refresh Token 重複使用。", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (await _userManager.IsLockedOutAsync(existing.User))
            return null;

        var roles = await _userManager.GetRolesAsync(existing.User);
        var now = DateTime.UtcNow;
        var nextTokenValue = CreateSecureToken();
        var next = new RefreshToken
        {
            UserId = existing.UserId,
            TokenHash = HashToken(nextTokenValue),
            TokenFamily = existing.TokenFamily,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_options.RefreshTokenDays),
            CreatedByIp = LimitIp(ipAddress)
        };

        _db.RefreshTokens.Add(next);
        existing.RevokedAt = now;
        existing.RevokedByIp = LimitIp(ipAddress);
        existing.RevocationReason = "Refresh Token 已輪替。";
        await _db.SaveChangesAsync(cancellationToken);
        existing.ReplacedByTokenId = next.Id;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var accessExpiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        return CreateResponse(existing.User, roles, accessExpiresAt, nextTokenValue, next.ExpiresAt);
    }

    public async Task RevokeAsync(
        string refreshToken,
        string? ipAddress,
        string reason,
        CancellationToken cancellationToken)
    {
        var hash = HashToken(refreshToken);
        var existing = await _db.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (existing is null || existing.RevokedAt.HasValue) return;

        existing.RevokedAt = DateTime.UtcNow;
        existing.RevokedByIp = LimitIp(ipAddress);
        existing.RevocationReason = reason;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public void WriteRefreshTokenCookie(HttpResponse response, string token, DateTime expiresAt)
    {
        response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment() || response.HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = "/api/v1/auth",
            Expires = new DateTimeOffset(expiresAt)
        });
    }

    public void DeleteRefreshTokenCookie(HttpResponse response)
    {
        response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment() || response.HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = "/api/v1/auth"
        });
    }

    private AuthResponse CreateResponse(
        ApplicationUser user,
        IList<string> roles,
        DateTime accessExpiresAt,
        string refreshToken,
        DateTime refreshExpiresAt)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: accessExpiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var currentUser = new CurrentUserDto(user.Id, user.UserName ?? string.Empty, user.DisplayName, user.Email, roles.ToList());
        return new AuthResponse(accessToken, accessExpiresAt, currentUser, refreshToken, refreshExpiresAt);
    }

    private async Task RevokeFamilyAsync(
        string tokenFamily,
        string? ipAddress,
        string reason,
        CancellationToken cancellationToken)
    {
        var activeTokens = await _db.RefreshTokens
            .Where(token => token.TokenFamily == tokenFamily && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
            token.RevokedByIp = LimitIp(ipAddress);
            token.RevocationReason = reason;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string CreateSecureToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string? LimitIp(string? ipAddress) => string.IsNullOrWhiteSpace(ipAddress)
        ? null
        : ipAddress.Length <= 64 ? ipAddress : ipAddress[..64];
}