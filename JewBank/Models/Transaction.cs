namespace JewBank.Models;

using JewBank.Enums;

public class Transaction
{
    public required string TransactionId { get; set; }
    public required string SourceAccountId { get; set; }
    public string? TargetAccountId { get; set; } // Nullable for cash deposits / ATM withdrawals
    public string? TargetIban { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; } // Transfer, Deposit, Withdrawal, etc.
    public TransactionStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}