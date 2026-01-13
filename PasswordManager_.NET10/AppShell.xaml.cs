using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;
using PasswordManager_.NET10.Views.Authentication;

namespace PasswordManager_.NET10;

public partial class AppShell : Shell
{
    private readonly ILogger<AppShell> _logger;
    //private readonly IServiceProvider _serviceProvider;
    //private readonly IAuthService _authService;
    private readonly ISessionManager _sessionManager;

    public AppShell(
        ILogger<AppShell> logger,
        //IServiceProvider serviceProvider,
        //IAuthService authService,
        ISessionManager sessionManager)
    {
        InitializeComponent();

        _logger = logger;
        //_serviceProvider = serviceProvider;
        //_authService = authService;
        _sessionManager = sessionManager;

        _logger.LogInformation("[AppShell-Constructor] AppShell initialized");
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await _sessionManager.Logout(false);
        //await PerformLogout(false);
    }

    //public async Task PerformLogout(bool hasExpired)
    //{
    //    try
    //    {
    //        _logger.LogInformation("[AppShell-PerformLogout] Logout initiated");
    //        bool confirmed;

    //        if (hasExpired)
    //        {
    //            await Shell.Current.DisplayAlertAsync(
    //                "Cerrar sesión",
    //                "La Session ha Expirado",
    //                "Sí"
    //            );
    //            confirmed = true;
    //        }
    //        else
    //        {
    //            // Pedir confirmación
    //            confirmed = await Shell.Current.DisplayAlertAsync(
    //                "Cerrar sesión",
    //                "¿Estás seguro de que deseas cerrar sesión?",
    //                "Sí",
    //                "No"
    //            );
    //        }

    //        if (!confirmed)
    //        {
    //            _logger.LogInformation("[AppShell-PerformLogout] Logout cancelled by user");
    //            return;
    //        }

    //        // Limpiar sesión en SecureStorage
    //        await _authService.LogoutAsync();
    //        _logger.LogInformation("[AppShell-PerformLogout] Session cleared");

    //        var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
    //        Window.Page = loginPage;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "[AppShell-PerformLogout] Error during logout: {ExceptionType} - {Message}",
    //            ex.GetType().Name, ex.Message);

    //        await Shell.Current.DisplayAlertAsync(
    //            "Error",
    //            "Ocurrió un error al cerrar sesión",
    //            "OK"
    //        );
    //    }
    //}
}
