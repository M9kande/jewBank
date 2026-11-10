using GoonBank.Enums;
using System.ComponentModel.DataAnnotations;

namespace GoonBank.DTOs;

public class CreateAccountRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public required string Email { get; set; } = string.Empty;

    [Required]
    public required string PasswordHash { get; set; } = string.Empty;

    [Required]
    public required DateOnly DateOfBirth { get; set; } = new DateOnly();

    [Required]
    [Length(3, 3)]
    public required string Currency { get; set; } = "PLN";

    [Required]
    [EnumDataType(typeof(AccountType))]
    public required AccountType AccountType { get; set; }
}
