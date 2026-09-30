namespace JewBank.Models;

using JewBank.Enums;

public class BlikPayment
{
    public required string BlikPaymentId { get; set; } = string.Empty;
    public required string TransactionId { get; set; } = string.Empty;
    public required string BlikCodeId { get; set; } = string.Empty;
    public required string PayerAccountId { get; set; } = string.Empty;
    public required string MerchantId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public BlikPaymentStatus Status { get; set; }
    public DateTime InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}