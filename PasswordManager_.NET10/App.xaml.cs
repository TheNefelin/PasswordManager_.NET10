using Microsoft.Extensions.DependencyInjection;
using PasswordManager_.NET10.Services.Interfaces;
using PasswordManager_.NET10.Views.Authentication;

namespace PasswordManager_.NET10;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IThemeService _themeService;

    public App(
        IServiceProvider serviceProvider,
        IThemeService themeService)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _themeService = themeService;

        // Aplicar Dark de forma inmediata para evitar el parpadeo blanco en el arranque
        UserAppTheme = AppTheme.Dark;

        // Cargar el tema guardado una sola vez (puede sobrescribir el valor por defecto)
        _ = LoadSavedThemeAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var loginPage = _serviceProvider.GetRequiredService<LoginPage>()
            ?? throw new InvalidOperationException("LoginPage no pudo ser resuelta del contenedor DI");
        return new Window(loginPage);
    }

    private async Task LoadSavedThemeAsync()
    {
        await _themeService.LoadAndApplyThemeAsync();
    }
}