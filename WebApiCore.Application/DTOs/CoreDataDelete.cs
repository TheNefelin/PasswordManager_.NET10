namespace WebApiCore.Application.DTOs;

public class CoreDataDelete
{
    public Guid Data_Id { get; set; }
    public required CoreUserRequest CoreUser { get; set; }
}