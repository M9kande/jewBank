namespace JewBank.Models;

using JewBank.Enums;

public class BlikCode
{
    public required string BlikCodeId { get; set; } = string.Empty;
    public required string BlikProfileId { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public BlikCodeStatus Status { get; set; }
}