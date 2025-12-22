using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.ViewModels;

public partial class PasswordPromptCreateViewModel : BaseViewModel
{

    [ObservableProperty]
    string newPassword = string.Empty;

    [ObservableProperty]
    string confirmPassword = string.Empty;

    [ObservableProperty]
    string message = string.Empty;

    [ObservableProperty]
    bool isPassword1 = true;

    [ObservableProperty]
    bool isPassword2 = true;

    private readonly ILogger<PasswordPromptCreateViewModel> _logger;
    private readonly ICoreDataService _coreDataService;

    public PasswordPromptCreateViewModel(
        ILogger<PasswordPromptCreateViewModel> logger,
        ICoreDataService coreDataService)
    {
        _logger = logger;
        _coreDataService = coreDataService;
    }

    [RelayCommand]
    public async Task AcceptClickedAsync()
    {
        _logger.LogInformation("Aceptando nueva contraseña desde PasswordPromptCreateViewModel.");

        if (NewPassword != ConfirmPassword)
        {
            Message = "Las contraseñas no coinciden.";
            return;
        }

        try
        {
            var coreUserIV = await _coreDataService.RegisterCorePasswordAsync(NewPassword);
            _logger.LogInformation("Nueva contraseña creada exitosamente desde PasswordPromptCreateViewModel.");

            await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
                "Aviso",
                $"Contraseña creada correctamente, Id: {coreUserIV.IV}",
                "OK"
            );

            Message = "";
            _ = Application.Current!.Windows[0].Page!.Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear la nueva contraseña desde PasswordPromptCreateViewModel.");
            Message = $"Error al crear la nueva contraseña: {ex.Message}";
            return;
        }
        finally
        {
            NewPassword = "";
            ConfirmPassword = "";
        }
    }

    [RelayCommand]
    public async Task CancelClickedAsync()
    {
        _logger.LogInformation("Cancelando nueva contraseña desde PasswordPromptCreateViewModel.");

        _ = Application.Current!.Windows[0].Page!.Navigation.PopAsync();

        NewPassword = "";
        ConfirmPassword = "";
        Message = "";
    }

    [RelayCommand]
    public void ToggleIsPassword1()
    {
        IsPassword1 = !IsPassword1;
    }

    [RelayCommand]
    public void ToggleIsPassword2()
    {
        IsPassword2 = !IsPassword2;
    }
}
