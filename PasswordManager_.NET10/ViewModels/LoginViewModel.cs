using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace PasswordManager_.NET10.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly ILogger<LoginViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string message = string.Empty;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private bool isFormValid = false;

    public LoginViewModel(
        ILogger<LoginViewModel> logger,
        IServiceProvider serviceProvider,
        IAuthService authService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _authService = authService;

        Title = "Login";
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

            // Intentar login
            var user = await _authService.LoginAsync(Email, Password);

            _logger.LogInformation("[LoginViewModel-LoginAsync] Login successful for user: {UserId}", user.UserId);

            // Limpiar campos
            Email = string.Empty;
            Password = string.Empty;
            Message = string.Empty; //Message = "Login exitoso";

            // Reemplazar la ventana actual con AppShell
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var appShell = _serviceProvider.GetRequiredService<AppShell>();
                Application.Current!.Windows[0].Page = appShell;
                _logger.LogInformation("[LoginViewModel-LoginAsync] Navigated to AppShell");
            });
        }
        catch (Exception ex)
        {
            Message = ex.Message ?? "Error en el login. Intenta de nuevo.";
            _logger.LogError(ex, "[LoginViewModel-LoginAsync] Login error: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
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
}