using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace PasswordManager_.NET10.Models;

public partial class CoreSecretData : ObservableObject
{
    public Guid Data_Id { get; set; }
    public required string Data01 { get; set; }
    public required string Data02 { get; set; }
    public required string Data03 { get; set; }
    public Guid User_Id { get; set; }

    // UI State (no viene del servidor)
    [ObservableProperty]
    [JsonIgnore]
    public partial bool IsExpanded { get; set; } = false;
    [ObservableProperty]
    [JsonIgnore]
    public partial bool IsPasswordVisible { get; set; } = true;
}