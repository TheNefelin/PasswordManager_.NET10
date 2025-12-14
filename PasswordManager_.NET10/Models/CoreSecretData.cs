using System.Text.Json.Serialization;

namespace PasswordManager_.NET10.Models;

public class CoreSecretData
{
    public Guid Data_Id { get; set; }
    public required string Data01 { get; set; }
    public required string Data02 { get; set; }
    public required string Data03 { get; set; }
    public Guid User_Id { get; set; }

    // UI State (no viene del servidor)
    [JsonIgnore]
    public bool IsExpanded { get; set; } = false;
}