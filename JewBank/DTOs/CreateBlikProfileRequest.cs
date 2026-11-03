namespace JewBank.DTOs;

public class CreateBlikProfileRequest
{
    public required string CustomerId { get; set; }
    public required string PhoneHash { get; set; }
}