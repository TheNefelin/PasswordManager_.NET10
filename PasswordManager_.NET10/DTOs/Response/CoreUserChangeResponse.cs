using System.Text.Json.Serialization;

namespace PasswordManager_.NET10.DTOs.Response;

public class CoreUserChangeResponse
{
    [JsonPropertyName("iv")]
    public required string IV { get; set; }

    [JsonPropertyName("sqlToken")]
    public Guid SqlToken { get; set; }
}