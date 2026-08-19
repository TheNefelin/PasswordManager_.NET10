namespace WebApiCore.Domain.Entities;

public class CoreUser
{
    public required Guid User_Id { get; set; }
    public required Guid SqlToken { get; set; }
    public string? HashPM { get; set; }
    public string? SaltPM { get; set; }
}