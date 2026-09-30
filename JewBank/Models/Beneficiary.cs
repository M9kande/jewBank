namespace JewBank.Models;

using JewBank.Enums;

public class Beneficiary
{
    public required string BeneficiaryId { get; set; } = string.Empty;
    public required string CustomerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string BankCountry { get; set; } = string.Empty;
    public BeneficiaryStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}