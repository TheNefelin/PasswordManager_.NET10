using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Messages;
using PasswordManager_.NET10.Services.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace PasswordManager_.NET10.ViewModels;

public partial class RegisterViewModel : BaseViewModel
{
    private readonly ILogger<RegisterViewModel> _logger;
    private readonly IAuthService _authService;

    [ObservableProperty]
    string email = string.Empty;

    [ObservableProperty]
    string password = string.Empty;

    [ObservableProperty]
    string confirmPassword = string.Empty;

    [ObservableProperty]
    bool isLoading = false;

    [ObservableProperty]
    string message = string.Empty;

    [ObservableProperty]
    bool isPassword = true;

    public RegisterViewModel(
        ILogger<RegisterViewModel> logger,
        IAuthService authService)
    {
        _logger = logger;
        _authService = authService;
    }

    [RelayCommand]
    public async Task RegisterAsync()
    {
        // Validar primero
        if (!ValidateFieldsDetailed())
        {
            return;
        }

        try
        {
            IsLoading = true;
            var result = await _authService.RegisterAsync(Email, Password, confirmPassword);

            if (!result)
            {
                Message = "Error en el registro. Intenta de nuevo.";
                return;
            }

            _logger.LogInformation("[RegisterViewModel-RegisterAsync] Registration successful for email: {Email}", Email);

            // Enviar mensaje con el email
            WeakReferenceMessenger.Default.Send(new RegistrationCompletedMessage(Email));

            // Cerrar modal
            await Application.Current!.Windows[0].Page!.Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            Message = ex.Message ?? "Error en el registro. Intenta de nuevo.";
            _logger.LogError(ex, "[RegisterViewModel-RegisterAsync] Registration error");
        }
        finally
        {
            IsLoading = false;
            ClearField();
        }
    }

    /// <summary>
    /// Comando para cancelar y volver a LoginPage
    /// </summary>
    [RelayCommand]
    public async Task CancelAsync()
    {
        try
        {
            _logger.LogInformation("[RegisterViewModel-CancelAsync] User cancelled registration");
            await Application.Current!.Windows[0].Page!.Navigation.PopModalAsync();
            ClearField();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RegisterViewModel-CancelAsync] Error cancelling registration: {Message}", ex.Message);
        }
    }

    private bool ValidateFieldsDetailed()
    {
        Message = string.Empty;

        if (string.IsNullOrWhiteSpace(Email))
        {
            Message = "El email es requerido";
            return false;
        }

        if (!new EmailAddressAttribute().IsValid(Email))
        {
            Message = "El email no es válido";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            Message = "La contraseña es requerida";
            return false;
        }

        if (Password.Length < 6)
        {
            Message = "La contraseña debe tener al menos 6 caracteres";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            Message = "Debe confirmar la contraseña";
            return false;
        }

        if (Password != ConfirmPassword)
        {
            Message = "Las contraseñas no coinciden";
            return false;
        }

        return true;
    }

    [RelayCommand]
    public void ToggleIsPassword()
    {
        IsPassword = !IsPassword;
    }

    private void ClearField()
    {
        Email = "";
        Password = "";
        ConfirmPassword = "";
    }
}
