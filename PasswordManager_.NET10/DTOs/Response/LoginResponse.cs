using System.Text.Json.Serialization;

namespace PasswordManager_.NET10.DTOs.Response;

public class LoginResponse
{
    [JsonPropertyName("user_Id")]
    public Guid UserId { get; set; }

    [JsonPropertyName("sqlToken")]
    public Guid SqlToken { get; set; }

    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("expireMin")]
    public required string ExpireMin { get; set; }

    [JsonPropertyName("apiToken")]
    public required string ApiToken { get; set; }
}