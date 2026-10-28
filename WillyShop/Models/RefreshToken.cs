using System.ComponentModel.DataAnnotations;

namespace WillyShop.Models;

public sealed class RefreshToken
{
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string TokenFamily { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public long? ReplacedByTokenId { get; set; }

    [MaxLength(64)]
    public string? CreatedByIp { get; set; }

    [MaxLength(64)]
    public string? RevokedByIp { get; set; }

    [MaxLength(200)]
    public string? RevocationReason { get; set; }

    public ApplicationUser User { get; set; } = null!;
}