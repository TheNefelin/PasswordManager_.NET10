namespace PasswordManager_.NET10.DTOs.Request;

public class CoreUserIVRequest
{
    public required string Password { get; set; }
    public required CoreUserRequest CoreUser { get; set; }
}
