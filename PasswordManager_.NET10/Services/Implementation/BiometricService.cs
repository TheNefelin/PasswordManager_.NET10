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
    /// Verifica si el dispositivo soporta biometría (huella o reconocimiento facial)
    /// </summary>
    public async Task<bool> IsBiometricAvailableAsync()
    {
        try
        {
            _logger.LogInformation("[BiometricService-IsBiometricAvailableAsync] Checking biometric availability");

            // Obtener el estado de autenticación biométrica
            var status = await BiometricAuthenticationService.Default.GetAuthenticationStatusAsync();

            // BiometricHwStatus.Success = dispositivo soporta biometría
            // BiometricHwStatus.NoHardware = sin hardware biométrico
            // BiometricHwStatus.NoEnrollment = sin biometría configurada
            // BiometricHwStatus.NotEnabled = biometría no habilitada en el sistema

            bool isAvailable = status == BiometricHwStatus.Success;

            _logger.LogInformation(
                "[BiometricService-IsBiometricAvailableAsync] Biometric status: {Status}, Available: {Available}",
                status, isAvailable);

            return isAvailable;
        }
        catch (NotImplementedException)
        {
            _logger.LogWarning("[BiometricService-IsBiometricAvailableAsync] Biometric not implemented on this platform");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[BiometricService-IsBiometricAvailableAsync] Error checking biometric availability: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
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

            // Primero verificar que biometría está disponible
            var isAvailable = await IsBiometricAvailableAsync();
            if (!isAvailable)
            {
                _logger.LogWarning("[BiometricService-AuthenticateWithBiometricAsync] Biometric not available on this device");
                return null;
            }

            var result = await BiometricAuthenticationService.Default.AuthenticateAsync(
                new AuthenticationRequest()
                {
                    Title = "Autenticación Biométrica",
                    Subtitle = "Usa tu huella dactilar o reconocimiento facial",
                    Description = "Autenticación requerida para continuar",
                    NegativeText = "Cancelar"
                },
                CancellationToken.None
            );

            _logger.LogInformation("[BiometricService-AuthenticateWithBiometricAsync] Authentication result: {Status}", result.Status);

            if (result.Status == BiometricResponseStatus.Success)
            {
                // Obtener y retornar el email guardado
                var email = await _secureStorageService.GetEmailAsync();
                _logger.LogInformation("[BiometricService-AuthenticateWithBiometricAsync] Biometric authentication successful");
                return email;
            }
            else
            {
                _logger.LogWarning("[BiometricService-AuthenticateWithBiometricAsync] Biometric authentication failed: {Error}", result.ErrorMsg);
                return null;
            }
        }
        catch (NotImplementedException)
        {
            _logger.LogWarning("[BiometricService-AuthenticateWithBiometricAsync] Biometric not implemented on this platform");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[BiometricService-AuthenticateWithBiometricAsync] Error during biometric authentication: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Guarda si biometría está habilitada por el usuario
    /// </summary>
    public async Task EnableBiometricAsync(bool isEnabled)
    {
        try
        {
            _logger.LogInformation("[BiometricService-EnableBiometricAsync] Setting biometric enabled: {IsEnabled}", isEnabled);

            await _secureStorageService.SetBiometricsEnabledAsync(isEnabled);

            // Si se desactiva, limpiar datos relacionados
            if (!isEnabled)
            {
                _logger.LogInformation("[BiometricService-EnableBiometricAsync] Biometric disabled, clearing related data");
            }

            _logger.LogInformation("[BiometricService-EnableBiometricAsync] Biometric setting updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[BiometricService-EnableBiometricAsync] Error enabling biometric: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Verifica si el usuario ha habilitado biometría
    /// </summary>
    public async Task<bool> IsBiometricEnabledAsync()
    {
        try
        {
            _logger.LogInformation("[BiometricService-IsBiometricEnabledAsync] Checking if biometric is enabled by user");

            var isEnabled = await _secureStorageService.IsBiometricsEnabledAsync();

            _logger.LogInformation("[BiometricService-IsBiometricEnabledAsync] Biometric enabled: {IsEnabled}", isEnabled);

            return isEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[BiometricService-IsBiometricEnabledAsync] Error checking biometric status: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Verifica si biometría está disponible Y habilitada (método auxiliar útil)
    /// </summary>
    public async Task<bool> IsBiometricReadyAsync()
    {
        var isAvailable = await IsBiometricAvailableAsync();
        var isEnabled = await IsBiometricEnabledAsync();

        return isAvailable && isEnabled;
    }
}