using WillyShop.Contracts;
using WillyShop.Models;

namespace WillyShop.Services.Auth;

public interface ITokenService
{
    Task<AuthResponse> IssueAsync(ApplicationUser user, string? ipAddress, CancellationToken cancellationToken);
    Task<AuthResponse?> RotateAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task RevokeAsync(string refreshToken, string? ipAddress, string reason, CancellationToken cancellationToken);
    void WriteRefreshTokenCookie(HttpResponse response, string token, DateTime expiresAt);
    void DeleteRefreshTokenCookie(HttpResponse response);
}