namespace PasswordManager_.NET10.DTOs.Request;

public class CoreDataDeleteRequest
{
    public required Guid Data_Id { get; set; }
    public required CoreUserRequest CoreUser { get; set; }
}
