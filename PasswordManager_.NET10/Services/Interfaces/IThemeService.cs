namespace PasswordManager_.NET10.Services.Interfaces;

public interface IThemeService
{
    Task<string> GetThemeAsync();
    Task SetThemeAsync(string theme);
    void ApplyTheme(string theme);
    Task LoadAndApplyThemeAsync();
}