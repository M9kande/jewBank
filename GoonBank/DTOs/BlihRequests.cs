namespace GoonBank.DTOs;

public class GenerateBlihCodeRequest
{
    public required String AccountId { get; set; }
}

public class BlihCodeResponse
{
    public required String Code  { get; set; }
    public required DateTime ExpiresAt { get; set; }
}

public class InitiateBlihPaymentRequest
{
    public required String BlihCode  { get; set; }
    public required String MerchantId { get; set; }
    public decimal Amount { get; set; }
}
