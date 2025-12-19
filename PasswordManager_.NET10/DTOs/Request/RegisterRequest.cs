using System.ComponentModel.DataAnnotations;

namespace PasswordManager_.NET10.DTOs.Request;

public class RegisterRequest
{
    [EmailAddress]
    [MaxLength(100)]
    public required string Email { get; set; }

    [MinLength(6)]
    [MaxLength(50)]
    public required string Password1 { get; set; }

    [MinLength(6)]
    [MaxLength(50)]
    public required string Password2 { get; set; }
}
