using System.ComponentModel.DataAnnotations;

namespace GoonBank.DTOs;

public class CreateBlihProfileRequest
{
    [Required]
    public required string AccountId { get; set; }

    [Required]
    public required string PhoneHash { get; set; }
}
