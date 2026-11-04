using System;

namespace JewBank.Models;

using JewBank.Enums;

public class Account
{
    public required string AccountId { get; set; } = string.Empty;
    
    public required string PasswordHash { get; set; } = string.Empty;
    
    public string FirstName { get; set; } = string.Empty;
    
    public string LastName { get; set; } = string.Empty;
    
    public string Email { get; set; } = string.Empty;
    
    public DateOnly DateOfBirth { get; set; }
    public string AccountNo { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; } = 0.00m;
    public AccountType AccountType { get; set; }
    public AccountStatus Status { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}