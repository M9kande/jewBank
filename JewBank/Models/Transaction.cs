namespace JewBank.Models;

using JewBank.Enums;

public class Transaction
{
    public required string TransactionId { get; set; } = string.Empty;
    public TransactionType TransactionType { get; set; }
    public TransactionStatus Status { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}