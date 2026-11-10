using GoonBank.Enums;

namespace GoonBank.Models;

public class BlihPayment
{
    public required string BlihPaymentId { get; set; } = string.Empty;
    public required string TransactionId { get; set; } = string.Empty;
    public required string BlihCodeId { get; set; } = string.Empty;
    public required string PayerAccountId { get; set; } = string.Empty;
    public required string MerchantId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public BlihPaymentStatus Status { get; set; }
    public DateTime InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
