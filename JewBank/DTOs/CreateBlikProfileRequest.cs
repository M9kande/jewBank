namespace JewBank.DTOs;

public class CreateBlikProfileRequest
{
    public required string AccountId { get; set; }
    public required string PhoneHash { get; set; }
}