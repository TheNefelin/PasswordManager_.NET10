namespace WebApiCore.Application.DTOs;

public class CoreUserRequest
{
    public required Guid User_Id { get; set; }
    public required Guid SqlToken { get; set; }
}