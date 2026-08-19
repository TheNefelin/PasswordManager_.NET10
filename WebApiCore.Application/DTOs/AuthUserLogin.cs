using System.ComponentModel.DataAnnotations;

namespace WebApiCore.Application.DTOs;

public class AuthUserLogin
{
    [EmailAddress]
    [MaxLength(100)]
    public required string Email { get; set; }

    [MinLength(6)]
    [MaxLength(50)]
    public required string Password { get; set; }
}