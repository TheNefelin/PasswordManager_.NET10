using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;
using PasswordManager_.NET10.Views.Authentication;

namespace PasswordManager_.NET10.Services.Implementation;

public class SessionManager : ISessionManager
{
    private readonly ILogger<SessionManager> _logger;
    private readonly IAuthService _authService;
    private readonly IServiceProvider _serviceProvider;

    public SessionManager(
        ILogger<SessionManager> logger, 
        IAuthService authService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _authService = authService;
        _serviceProvider = serviceProvider;
    }

    public async Task LoginAsync(string email, string password)
    {
        // Intentar login
        var user = await _authService.LoginAsync(email, password);

        bool shouldSavePassword = await _authService.GetSavePasswordOnNextLoginAsync();
        if (shouldSavePassword)
        {
            try
            {
                // Guardar contraseña (el servicio se encarga de encriptar)
                await _authService.SavePasswordAsync(password);

                // Limpiar el flag
                await _authService.SetSavePasswordOnNextLoginAsync(false);

                _logger.LogInformation("[LoginViewModel-LoginAsync] Password saved successfully for next biometric login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LoginViewModel-LoginAsync] Error saving password: {Message}", ex.Message);
                // No fallar el login si hay error al guardar contraseña
            }
        }
    }

    public async Task Logout(bool hasExpired)
    {
        try
        {
            _logger.LogInformation("[AppShell-PerformLogout] Logout initiated");
            bool confirmed;

            if (hasExpired)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Cerrar sesión",
                    "La Session ha Expirado",
                    "Sí"
                );
                confirmed = true;
            }
            else
            {
                // Pedir confirmación
                confirmed = await Shell.Current.DisplayAlertAsync(
                    "Cerrar sesión",
                    "¿Estás seguro de que deseas cerrar sesión?",
                    "Sí",
                    "No"
                );
            }

            if (!confirmed)
            {
                _logger.LogInformation("[AppShell-PerformLogout] Logout cancelled by user");
                return;
            }

            // Limpiar sesión en SecureStorage
            await _authService.LogoutAsync();
            _logger.LogInformation("[AppShell-PerformLogout] Session cleared");

            var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
            Application.Current!.Windows[0].Page = loginPage;
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

    public TimeSpan GetRemainingTime()
    {
        throw new NotImplementedException();
    }

    public void InitializeSession(int expireMinutes)
    {
        throw new NotImplementedException();
    }

    public bool IsSessionExpired()
    {
        throw new NotImplementedException();
    }

    public Task PerformFullLogoutAsync(string? message = null)
    {
        throw new NotImplementedException();
    }

    public void UpdateSessionTime()
    {
        throw new NotImplementedException();
    }
}