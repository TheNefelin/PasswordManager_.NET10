namespace PasswordManager_.NET10.DTOs.Request;

public class CoreDataRequest
{
    public required Guid Data_Id { get; set; }
    public required string Data01 { get; set; }
    public required string Data02 { get; set; }
    public required string Data03 { get; set; }
    public required CoreUserRequest CoreUser { get; set; }
}
