using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Tests.Fakes;

public class FakeThemeService : IThemeService
{
    public string Theme { get; set; } = "Dark";

    public Task<string> GetThemeAsync() => Task.FromResult(Theme);

    public Task SetThemeAsync(string theme)
    {
        Theme = theme;
        return Task.CompletedTask;
    }

    public void ApplyTheme(string theme) => Theme = theme;

    public Task LoadAndApplyThemeAsync()
    {
        ApplyTheme(Theme);
        return Task.CompletedTask;
    }
}