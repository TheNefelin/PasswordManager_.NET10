using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.ViewModels;

public partial class PasswordPromptCreateViewModel : BaseViewModel
{

    [ObservableProperty]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsPassword1 { get; set; } = true;

    [ObservableProperty]
    public partial bool IsPassword2 { get; set; } = true;

    private readonly ILogger<PasswordPromptCreateViewModel> _logger;
    private readonly ICoreDataService _coreDataService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;

    public PasswordPromptCreateViewModel(
        ILogger<PasswordPromptCreateViewModel> logger,
        ICoreDataService coreDataService,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _logger = logger;
        _coreDataService = coreDataService;
        _navigationService = navigationService;
        _dialogService = dialogService;
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

            await _dialogService.ShowInfoAsync(
                $"Contraseña creada correctamente, Id: {coreUserIV.IV}",
                "Aviso"
            );

            Message = "";
            await _navigationService.PopAsync();
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

        await _navigationService.PopAsync();

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
