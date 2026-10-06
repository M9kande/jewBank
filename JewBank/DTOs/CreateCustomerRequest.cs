namespace JewBank.DTOs;

using JewBank.Enums;
using System.ComponentModel.DataAnnotations;

public class CreateCustomerRequest
{

    public required string FirstName { get; set; } = string.Empty;
    
    public required string LastName { get; set; } = string.Empty;
    
    public required string Email { get; set; } = string.Empty;
    
    public required string PasswordHash { get; set; } = string.Empty;
    
    public required DateOnly DateOfBirth { get; set; } =  new DateOnly();
    
    
}