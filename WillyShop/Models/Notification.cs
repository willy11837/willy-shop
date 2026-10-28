using System.ComponentModel.DataAnnotations;

namespace WillyShop.Models;

public enum NotificationType
{
    BackInStock = 0
}

public sealed class BackInStockSubscription
{
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = null!;

    [Required]
    public string ProductCode { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? NotifiedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Product Product { get; set; } = null!;
}

public sealed class UserNotification
{
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = null!;

    public NotificationType Type { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = null!;

    [Required]
    [StringLength(500)]
    public string Message { get; set; } = null!;

    [StringLength(450)]
    public string? ProductCode { get; set; }

    [StringLength(500)]
    public string? TargetUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
}