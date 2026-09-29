using System.ComponentModel.DataAnnotations;

namespace WebApiCore.Application.DTOs;

public class CoreUserPasswordCreate
{
    [MinLength(8)]
    [MaxLength(50)]
    public required string Password { get; set; }

    public required CoreUserRequest CoreUser { get; set; }
}