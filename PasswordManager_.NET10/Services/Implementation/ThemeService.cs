using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Services.Implementation;

public class ThemeService : IThemeService
{
    private readonly ILogger<ThemeService> _logger;
    private const string THEME_KEY = "AppTheme";
    private const string DEFAULT_THEME = "Auto";

    public ThemeService(ILogger<ThemeService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el tema guardado
    /// </summary>
    public async Task<string> GetThemeAsync()
    {
        try
        {
            var theme = await SecureStorage.GetAsync(THEME_KEY);
            return theme ?? DEFAULT_THEME;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ThemeService-GetThemeAsync] Error getting theme: {Message}", ex.Message);
            return DEFAULT_THEME;
        }
    }

    /// <summary>
    /// Guarda y aplica el tema
    /// </summary>
    public async Task SetThemeAsync(string theme)
    {
        try
        {
            await SecureStorage.SetAsync(THEME_KEY, theme);
            ApplyTheme(theme);
            _logger.LogInformation("[ThemeService-SetThemeAsync] Theme saved and applied: {Theme}", theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ThemeService-SetThemeAsync] Error setting theme: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Aplica el tema a la aplicación
    /// </summary>
    public void ApplyTheme(string theme)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Application.Current!.UserAppTheme = theme switch
                {
                    "Light" => AppTheme.Light,
                    "Dark" => AppTheme.Dark,
                    _ => AppTheme.Unspecified // Auto
                };
            });

            _logger.LogInformation("[ThemeService-ApplyTheme] Theme applied: {Theme}", theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ThemeService-ApplyTheme] Error applying theme: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Carga y aplica el tema guardado (para inicialización)
    /// </summary>
    public async Task LoadAndApplyThemeAsync()
    {
        try
        {
            var savedTheme = await GetThemeAsync();
            ApplyTheme(savedTheme);
            _logger.LogInformation("[ThemeService-LoadAndApplyThemeAsync] Theme loaded and applied: {Theme}", savedTheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ThemeService-LoadAndApplyThemeAsync] Error loading theme: {Message}", ex.Message);
        }
    }
}
