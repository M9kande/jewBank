namespace JewBank.DTOs;

using JewBank.Enums;
using System.ComponentModel.DataAnnotations;

public class CreateAccountRequest
{

    public required string CustomerId { get; set; } = string.Empty;
    
    [Length(3, 3)]
    public required string Currency { get; set; } = "PLN";
    
    public required AccountType AccountType { get; set; }
}