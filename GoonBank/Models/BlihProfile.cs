using GoonBank.Enums;

namespace GoonBank.Models;

using GoonBank.Enums;

public class BlihProfile
{
    public required string BlihProfileId { get; set; } = string.Empty;
    public required string AccountId { get; set; } = string.Empty;
    public string PhoneHash { get; set; } = string.Empty;
    public BlihProfileStatus Status { get; set; }
    public DateTime RegisteredAt { get; set; }
}