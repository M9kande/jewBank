namespace JewBank.DTOs;

public class GenerateBlikCodeRequest
{
    public required String CustomerId { get; set; }
    public required String AccountId { get; set; }
}

public class BlikCodeResponse
{
    public required String Code  { get; set; }
    public required DateTime ExpiresAt { get; set; }
}

public class InitiateBlikPaymentRequest
{
    public required String CustomerId { get; set; }
    public required String MerchantId { get; set; }
    public decimal Amount { get; set; }
}

public class ConfirmBlikPaymentRequest
{
    public bool Accept { get; set; }
}

