namespace JewBank.Models;

using JewBank.Enums;

public class Merchant
{
    public required string MerchantId { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string Mcc { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public MerchantStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}