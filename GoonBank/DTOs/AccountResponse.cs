using GoonBank.Enums;
using GoonBank.Models;

namespace GoonBank.DTOs;

public class AccountResponse
{
    public string AccountId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string AccountNo { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public AccountType AccountType { get; set; }
    public AccountStatus Status { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public static AccountResponse FromAccount(Account account)
    {
        return new AccountResponse
        {
            AccountId = account.AccountId,
            FirstName = account.FirstName,
            LastName = account.LastName,
            Email = account.Email,
            DateOfBirth = account.DateOfBirth,
            AccountNo = account.AccountNo,
            Currency = account.Currency,
            Balance = account.Balance,
            AccountType = account.AccountType,
            Status = account.Status,
            OpenedAt = account.OpenedAt,
            CreatedAt = account.CreatedAt,
            ClosedAt = account.ClosedAt,
        };
    }
}

