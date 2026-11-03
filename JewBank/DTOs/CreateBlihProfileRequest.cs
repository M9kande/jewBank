namespace JewBank.DTOs;

public class CreateBlihProfileRequest
{
    public required string AccountId { get; set; }
    public required string PhoneHash { get; set; }
}