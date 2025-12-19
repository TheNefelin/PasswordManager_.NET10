namespace PasswordManager_.NET10.Services.Interfaces;

public interface IBiometricService
{
    /// <summary>
    /// Verifica si el dispositivo soporta biometría
    /// </summary>
    Task<bool> IsBiometricAvailableAsync();

    /// <summary>
    /// Autentica con biometría
    /// </summary>
    Task<string?> AuthenticateWithBiometricAsync();

    /// <summary>
    /// Guarda si biometría está habilitada
    /// </summary>
    Task EnableBiometricAsync(bool isEnabled);

    /// <summary>
    /// Verifica si biometría está habilitada
    /// </summary>
    Task<bool> IsBiometricEnabledAsync();
}
