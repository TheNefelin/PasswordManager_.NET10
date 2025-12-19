using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;
using Plugin.Maui.Biometric;

namespace PasswordManager_.NET10.Services.Implementation;

public class BiometricService : IBiometricService
{
    private readonly ILogger<BiometricService> _logger;
    private readonly ISecureStorageService _secureStorageService;

    private const string BIOMETRIC_ENABLED_KEY = "biometric_enabled";

    public BiometricService(
        ILogger<BiometricService> logger,
        ISecureStorageService secureStorageService)
    {
        _logger = logger;
        _secureStorageService = secureStorageService;
    }

    /// <summary>
    /// Verifica si el dispositivo soporta biometría
    /// </summary>

    public async Task<bool> IsBiometricAvailableAsync()
    {
        try
        {
            _logger.LogInformation("[BiometricService-IsBiometricAvailableAsync] Checking biometric availability");

            var status = await BiometricAuthenticationService.Default.GetAuthenticationStatusAsync();
            var result = status == BiometricHwStatus.Success;

            _logger.LogInformation("[BiometricService-IsBiometricAvailableAsync] Biometric available: {Available}", result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BiometricService-IsBiometricAvailableAsync] Error checking biometric availability");
            return false;
        }
    }

    /// <summary>
    /// Autentica con biometría
    /// </summary>
    public async Task<string?> AuthenticateWithBiometricAsync()
    {
        try
        {
            _logger.LogInformation("[BiometricService-AuthenticateWithBiometricAsync] Starting biometric authentication");

            var result = await BiometricAuthenticationService.Default.AuthenticateAsync(
                new AuthenticationRequest()
                {
                    Title = "Autenticación Biométrica",
                    Subtitle = "Usa tu huella dactilar",
                    Description = "Autenticación requerida",
                    NegativeText = "Cancelar"
                },
                CancellationToken.None
            );

            _logger.LogInformation("[BiometricService-AuthenticateWithBiometricAsync] Authentication result: {Success}", result.Status);

            if (result.Status == BiometricResponseStatus.Success)
            {
                // Obtener y retornar el email guardado
                var email = await _secureStorageService.GetEmailAsync();
                _logger.LogInformation("[BiometricService-AuthenticateWithBiometricAsync] Biometric authentication successful, email retrieved");
                return email;
            }
            else
            {
                // Retornar el error
                _logger.LogWarning("[BiometricService-AuthenticateWithBiometricAsync] Biometric authentication failed: {Error}", result.ErrorMsg);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BiometricService-AuthenticateWithBiometricAsync] Error during biometric authentication");
            return null;
        }
    }

    /// <summary>
    /// Guarda que biometría está habilitada
    /// </summary>
    public async Task EnableBiometricAsync(bool isEnabled)
    {
        try
        {
            _logger.LogInformation("[BiometricService-EnableBiometricAsync] Enabling biometric");

            await _secureStorageService.SetBiometricsEnabledAsync(isEnabled);

            _logger.LogInformation("[BiometricService-EnableBiometricAsync] Biometric enabled successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BiometricService-EnableBiometricAsync] Error enabling biometric");
            throw;
        }
    }

    /// <summary>
    /// Verifica si biometría está habilitada
    /// </summary>
    public async Task<bool> IsBiometricEnabledAsync()
    {
        try
        {
            _logger.LogInformation("[BiometricService-IsBiometricEnabledAsync] Checking if biometric is enabled");

            return await _secureStorageService.IsBiometricsEnabledAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BiometricService-IsBiometricEnabledAsync] Error checking biometric status");
            return false;
        }
    }
}
