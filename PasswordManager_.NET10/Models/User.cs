namespace PasswordManager_.NET10.Models;

public class User
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string SqlToken { get; set; } = string.Empty;
    public DateTime TokenExpiry { get; set; }
    public bool IsAuthenticated { get; set; }
}