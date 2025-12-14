using System.ComponentModel.DataAnnotations;

namespace PasswordManager_.NET10.DTOs.Request;

public class LoginRequest
{
    [EmailAddress]
    [MaxLength(100)]
    public required string Email { get; set; }

    [MinLength(6)]
    [MaxLength(50)]
    public required string Password { get; set; }
}