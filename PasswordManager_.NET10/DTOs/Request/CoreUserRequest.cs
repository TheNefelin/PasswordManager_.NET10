namespace PasswordManager_.NET10.DTOs.Request;

public class CoreUserRequest
{
    public required Guid User_Id { get; set; }
    public required Guid SqlToken { get; set; }
}
