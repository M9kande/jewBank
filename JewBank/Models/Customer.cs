namespace JewBank.Models;

using JewBank.Enums;

public class Customer
{
    public required string CustomerId { get; set; } = string.Empty;
    public required string CustomerNo { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public CustomerStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}