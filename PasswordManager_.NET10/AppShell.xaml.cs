using FreakyKit.Utils;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;
using PasswordManager_.NET10.ViewModels;
using PasswordManager_.NET10.Views.Authentication;

namespace PasswordManager_.NET10;

public partial class AppShell : Shell
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthService _authService;
    private readonly ILogger<AppShell> _logger;
    private readonly SettingsViewModel _settingsViewModel;

    public AppShell(
        IServiceProvider serviceProvider,
        IAuthService authService,
        ILogger<AppShell> logger,
        SettingsViewModel settingsViewModel)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _authService = authService;
        _logger = logger;
        _settingsViewModel = settingsViewModel;

        _logger.LogInformation("[AppShell-Constructor] AppShell initialized");

        // Precargar datos de SettingsViewModel
        PreloadSettingsData();
    }

    /// <summary>
    /// Precarga datos de SettingsViewModel en background
    /// </summary>
    private void PreloadSettingsData()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                _logger.LogInformation("[AppShell-PreloadSettingsData] Starting preload of settings data");
                await _settingsViewModel.LoadSessionDataAsync();
                _logger.LogInformation("[AppShell-PreloadSettingsData] Settings data preloaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AppShell-PreloadSettingsData] Error preloading settings data: {ExceptionType} - {Message}",
                    ex.GetType().Name, ex.Message);
            }
        });
    }

    /// <summary>
    /// Manejador del botón Logout
    /// </summary>
    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await PerformLogout();
    }

    /// <summary>
    /// Realiza el logout con confirmación
    /// </summary>
    public async Task PerformLogout()
    {
        try
        {
            _logger.LogInformation("[AppShell-PerformLogout] Logout initiated");

            // Pedir confirmación
            bool confirmed = await Shell.Current.DisplayAlertAsync(
                "Cerrar sesión",
                "¿Estás seguro de que deseas cerrar sesión?",
                "Sí",
                "No"
            );

            if (!confirmed)
            {
                _logger.LogInformation("[AppShell-PerformLogout] Logout cancelled by user");
                return;
            }

            // Limpiar sesión en SecureStorage
            await _authService.LogoutAsync();
            _logger.LogInformation("[AppShell-PerformLogout] Session cleared");

            // Cleanup de SettingsViewModel
            _settingsViewModel.Cleanup();

            var loginPage = _serviceProvider.GetRequiredService<LoginPage>();

            // En MAUI, Application.Current.MainPage es la clave
            Application.Current!.MainPage = loginPage;

            // Navegar a LoginPage
            //MainThread.BeginInvokeOnMainThread(() =>
            //{
            //    var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
            //    Application.Current!.Windows[0].Page = new NavigationPage(loginPage);
            //    _logger.LogInformation("[AppShell-PerformLogout] Navigated to LoginPage");
            //});
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AppShell-PerformLogout] Error during logout: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);

            await Shell.Current.DisplayAlertAsync(
                "Error",
                "Ocurrió un error al cerrar sesión",
                "OK"
            );
        }
    }

    /// <summary>
    /// Suscribirse a eventos de expiración de sesión
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            _settingsViewModel.SessionExpiredEvent += async (s, e) =>
            {
                _logger.LogInformation("[AppShell-OnAppearing] Session expired event triggered");
                await PerformLogout();
            };

            _logger.LogInformation("[AppShell-OnAppearing] Subscribed to SessionExpiredEvent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AppShell-OnAppearing] Error subscribing to SessionExpiredEvent: {Message}", ex.Message);
        }
    }
}
