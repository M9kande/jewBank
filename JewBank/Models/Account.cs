namespace JewBank.Models;

using JewBank.Enums;

public class Account
{
    public required string AccountId { get; set; } = string.Empty;
    public required string CustomerId { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; } = 0.00m;
    public AccountType AccountType { get; set; }
    public AccountStatus Status { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}