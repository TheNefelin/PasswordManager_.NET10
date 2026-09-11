using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Services.Implementation;

public class SessionManager : ISessionManager
{
    private readonly ILogger<SessionManager> _logger;
    private readonly IAuthService _authService;
    private readonly INavigationService _navigationService;

    public SessionManager(
        ILogger<SessionManager> logger, 
        IAuthService authService,
        INavigationService navigationService)
    {
        _logger = logger;
        _authService = authService;
        _navigationService = navigationService;
    }

    public async Task LoginAsync(string email, string password)
    {
        await _authService.LoginAsync(email, password);

        bool shouldSavePassword = await _authService.GetSavePasswordOnNextLoginAsync();
        if (shouldSavePassword)
        {
            try
            {
                // Guardar contraseña (el servicio se encarga de encriptar)
                await _authService.SavePasswordAsync(password);

                // Limpiar el flag
                await _authService.SetSavePasswordOnNextLoginAsync(false);

                _logger.LogInformation("[SessionManager-LoginAsync] Password saved successfully for next biometric login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SessionManager-LoginAsync] Error saving password: {Message}", ex.Message);
                // No fallar el login si hay error al guardar contraseña
            }
        }
    }

    public Task Logout(bool hasExpired)
    {
        return PerformFullLogoutAsync(hasExpired ? "La sesión ha expirado" : null);
    }

    public async Task PerformFullLogoutAsync(string? message = null)
    {
        var page = Application.Current?.Windows[0]?.Page;

        try
        {
            _logger.LogInformation("[SessionManager-PerformFullLogoutAsync] Logout initiated");
            bool confirmed;

            if (message != null)
            {
                if (page != null)
                {
                    await page.DisplayAlertAsync(
                        "Cerrar sesión",
                        message,
                        "Sí"
                    );
                }
                confirmed = true;
            }
            else
            {
                // Pedir confirmación
                confirmed = page == null || await page.DisplayAlertAsync(
                    "Cerrar sesión",
                    "¿Estás seguro de que deseas cerrar sesión?",
                    "Sí",
                    "No"
                );
            }

            if (!confirmed)
            {
                _logger.LogInformation("[SessionManager-PerformFullLogoutAsync] Logout cancelled by user");
                return;
            }

            // Limpiar sesión en SecureStorage
            await _authService.LogoutAsync();
            _logger.LogInformation("[SessionManager-PerformFullLogoutAsync] Session cleared");

            await _navigationService.GoToLoginAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SessionManager-PerformFullLogoutAsync] Error during logout: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);

            if (page != null)
            {
                await page.DisplayAlertAsync(
                    "Error",
                    "Ocurrió un error al cerrar sesión",
                    "OK"
                );
            }
        }
    }

    public async Task<TimeSpan> GetRemainingTimeAsync()
    {
        var currentUser = await _authService.GetCurrentUserAsync();
        if (currentUser == null)
        {
            return TimeSpan.Zero;
        }

        var remaining = currentUser.TokenExpiry - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public async Task<bool> IsSessionExpiredAsync()
    {
        var currentUser = await _authService.GetCurrentUserAsync();
        if (currentUser == null)
        {
            return true;
        }

        return currentUser.TokenExpiry <= DateTime.UtcNow;
    }
}