namespace JewBank.Models;

using JewBank.Enums;

public class Transfer
{
    public required string TransferId { get; set; } = string.Empty;
    public required string TransactionId { get; set; } = string.Empty;
    public required string SourceAccountId { get; set; } = string.Empty;
    public required string BeneficiaryId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public TransferType TransferType { get; set; }
    public TransferStatus Status { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime CreatedAt { get; set; }
}