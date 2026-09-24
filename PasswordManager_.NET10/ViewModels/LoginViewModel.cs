using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Messages;
using PasswordManager_.NET10.Services.Interfaces;
using PasswordManager_.NET10.Views.Authentication;
using System.ComponentModel.DataAnnotations;

namespace PasswordManager_.NET10.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly ILogger<LoginViewModel> _logger;
    private readonly IAuthService _authService;
    private readonly IBiometricService _biometricService;
    private readonly ISessionManager _sessionManager;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = false;

    [ObservableProperty]
    public partial bool IsFormValid { get; set; } = false;

    [ObservableProperty]
    public partial bool IsBiometricEnabled { get; set; } = false;

    [ObservableProperty]
    public partial bool IsPassword { get; set; } = true;

    public LoginViewModel(
        ILogger<LoginViewModel> logger,
        IAuthService authService,
        IBiometricService biometricService,
        ISessionManager sessionManager,
        INavigationService navigationService)
    {
        _logger = logger;
        _authService = authService;
        _biometricService = biometricService;
        _sessionManager = sessionManager;
        _navigationService = navigationService;

        Title = "Login";

        WeakReferenceMessenger.Default.Register<RegistrationCompletedMessage>(this,
        (recipient, message) =>
        {
            _logger.LogInformation("[LoginViewModel] Received registration email: {Email}", message.Email);
            Email = message.Email;
            Message = "Registro exitoso. Ingresa tu contraseña para continuar.";
        });
    }

    /// <summary>
    /// Se ejecuta cuando cambia Email o Password
    /// </summary>
    partial void OnEmailChanged(string value)
    {
        ValidateForm();
    }

    partial void OnPasswordChanged(string value)
    {
        ValidateForm();
    }

    /// <summary>
    /// Valida si el formulario es válido para habilitar el botón
    /// </summary>
    private void ValidateForm()
    {
        // Email válido y password con al menos 6 caracteres
        bool isEmailValid = !string.IsNullOrWhiteSpace(Email) && new EmailAddressAttribute().IsValid(Email);
        bool isPasswordValid = !string.IsNullOrWhiteSpace(Password) && Password.Length >= 6;

        IsFormValid = isEmailValid && isPasswordValid;
    }

    /// <summary>
    /// Validación detallada con mensajes de error específicos
    /// </summary>
    private bool ValidateFieldsDetailed()
    {
        Message = string.Empty;

        if (string.IsNullOrWhiteSpace(Email))
        {
            Message = "El email es requerido";
            _logger.LogWarning("[LoginViewModel-ValidateFieldsDetailed] Email is empty");
            return false;
        }

        if (!new EmailAddressAttribute().IsValid(Email))
        {
            Message = "El email no es válido";
            _logger.LogWarning("[LoginViewModel-ValidateFieldsDetailed] Invalid email format: {Email}", Email);
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            Message = "La contraseña es requerida";
            _logger.LogWarning("[LoginViewModel-ValidateFieldsDetailed] Password is empty");
            return false;
        }

        if (Password.Length < 6)
        {
            Message = "La contraseña debe tener al menos 6 caracteres";
            _logger.LogWarning("[LoginViewModel-ValidateFieldsDetailed] Password too short");
            return false;
        }

        return true;
    }

    public async Task ValidateBiometricAsync()
    {
        try
        {
            IsBiometricEnabled = await _biometricService.IsBiometricEnabledAsync();
            _logger.LogInformation("[LoginViewModel-ValidateBiometricAsync] Biometric enabled: {Enabled}", IsBiometricEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LoginViewModel-ValidateBiometricAsync] Error validating biometric");
            IsBiometricEnabled = false;
        }
    }

    /// <summary>
    /// Comando para ejecutar el login
    /// </summary>
    [RelayCommand]
    public async Task LoginAsync()
    {
        try
        {
            _logger.LogInformation("[LoginViewModel-LoginAsync] Login attempt for email: {Email}", Email);

            // Doble validación (por seguridad)
            if (!ValidateFieldsDetailed())
            {
                _logger.LogWarning("[LoginViewModel-LoginAsync] Validation failed: {Message}", Message);
                return;
            }

            IsLoading = true;
            Message = string.Empty;

            _logger.LogInformation("[LoginViewModel-LoginAsync] Calling AuthService.LoginAsync");
            await _sessionManager.LoginAsync(Email, Password);

            // Limpiar campos
            Email = string.Empty;
            Password = string.Empty;
            Message = string.Empty;

            await _navigationService.GoToAppShellAsync();
            _logger.LogInformation("[LoginViewModel-LoginAsync] Navigated to AppShell");
        }
        catch (Exception ex)
        {
            Message = ex.Message ?? "Error en el login. Intenta de nuevo.";
            _logger.LogError(ex, "[LoginViewModel-LoginAsync] Login error: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LoginByBiometric()
    {
        IsLoading = true;
        Message = string.Empty;

        // 1. Autenticar con biometría
        string? email = await _biometricService.AuthenticateWithBiometricAsync();
        _logger.LogInformation("[LoginViewModel-LoginByBiometric] Biometric authentication successful, email: {Email}", email);

        // 2. Obtener contraseña guardada (encriptada)
        string? password = await _authService.GetSavedPasswordAsync();
        if (!string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(email))
        {
            var user = await _authService.LoginAsync(email, password);

            await _navigationService.GoToAppShellAsync();
            _logger.LogInformation("[LoginViewModel-LoginByBiometric] Navigated to AppShell");

            IsLoading = false;
            return;
        }

        if (!string.IsNullOrEmpty(email))
        {
            Email = email;
            Password = string.Empty;
        }

        IsLoading = false;
    }

    /// <summary>
    /// Comando para navegar a RegisterPage
    /// </summary>
    [RelayCommand]
    public async Task GoToRegisterAsync()
    {
        try
        {
            _logger.LogInformation("[LoginViewModel-GoToRegisterAsync] Navigating to RegisterPage");

            await _navigationService.PushModalAsync<RegisterPage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LoginViewModel-GoToRegisterAsync] Error navigating to RegisterPage: {Message}", ex.Message);
        }
    }

    [RelayCommand]
    public void ToggleIsPassword()
    {
        IsPassword = !IsPassword;
    }

    [RelayCommand]
    public async Task OpenUrl(string url)
    {
        try
        {
            await Launcher.Default.OpenAsync(new Uri(url));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LoginViewModel-OpenUrl] Error opening URL: {Url}", url);
        }
    }
}