namespace WebApiCore.Domain.Entities;

public class AuthUser
{
    public Guid User_Id { get; set; }
    public required string Email { get; set; }
    public required string HashLogin { get; set; }
    public required string SaltLogin { get; set; }
    public string? HashPM { get; set; }
    public string? SaltPM { get; set; }
    public Guid SqlToken { get; set; }
    public string? Role { get; set; }
}