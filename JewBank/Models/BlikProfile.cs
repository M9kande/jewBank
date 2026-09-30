namespace JewBank.Models;

using JewBank.Enums;

public class BlikProfile
{
    public required string BlikProfileId { get; set; } = string.Empty;
    public required string CustomerId { get; set; } = string.Empty;
    public string PhoneHash { get; set; } = string.Empty;
    public BlikProfileStatus Status { get; set; }
    public DateTime RegisteredAt { get; set; }
}