using System.ComponentModel.DataAnnotations;
using GoonBank.Enums;

namespace GoonBank.DTOs;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    public required string Password { get; set; }
}

public class AuthResponse
{
    public required string Token { get; set; }
    public required DateTime ExpiresAt { get; set; }
    public required string AccountId { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string AccountNo { get; set; }
    public required string Currency { get; set; }
    public decimal Balance { get; set; }
    public AccountType AccountType { get; set; }
}
