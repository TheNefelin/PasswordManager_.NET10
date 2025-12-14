namespace PasswordManager_.NET10.Models;

public class SessionData
{
    public string UserId { get; set; } = string.Empty;
    public string SqlToken { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ExpireMin { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public DateTime ExpirationTime { get; set; }
}