using System.ComponentModel.DataAnnotations;

namespace GoonBank.DTOs;

public class GenerateBlihCodeRequest
{
    [Required]
    public required string AccountId { get; set; }
}

public class BlihCodeResponse
{
    public required string Code { get; set; }
    public required DateTime ExpiresAt { get; set; }
}

public class InitiateBlihPaymentRequest
{
    [Required]
    public required string BlihCode { get; set; }

    [Required]
    public required string MerchantId { get; set; }

    [Range(0.01, 1_000_000_000, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }
}
