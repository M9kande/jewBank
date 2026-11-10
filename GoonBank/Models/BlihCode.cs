using GoonBank.Enums;

namespace GoonBank.Models;

using GoonBank.Enums;

public class BlihCode
{
    public required string BlihCodeId { get; set; } = string.Empty;
    public required string BlihProfileId { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public BlihCodeStatus Status { get; set; }
}