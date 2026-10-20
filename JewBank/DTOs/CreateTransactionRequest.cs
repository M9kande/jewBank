using System.ComponentModel.DataAnnotations;
using JewBank.Enums;

namespace JewBank.DTOs;

public class CreateTransactionRequest : IValidatableObject
{
    public required string SourceAccountId { get; set; } = default!;

    public string? TargetAccountId { get; set; }

    [Range(0.01, 1_000_000_000, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [EnumDataType(typeof(TransactionType))]
    public TransactionType TransactionType { get; set; }

    [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (decimal.Round(Amount, 2) != Amount)
            yield return new ValidationResult(
                "Amount can have at most 2 decimal places.", new[] { nameof(Amount) });

        if (TransactionType != TransactionType.Transfer) yield break;
        if (string.IsNullOrWhiteSpace(TargetAccountId))
            yield return new ValidationResult(
                "Target account is required for transfers.", new[] { nameof(TargetAccountId) });
        else if (TargetAccountId == SourceAccountId)
            yield return new ValidationResult(
                "Source and target accounts must differ.", new[] { nameof(TargetAccountId) });
    }
}