using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Exceptions;
using PasswordManager_.NET10.Services.Interfaces;
using System.Security.Cryptography;

namespace PasswordManager_.NET10.ViewModels;

public partial class ChangeMasterPasswordViewModel : BaseViewModel
{
    [ObservableProperty]
    public partial string OldPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = false;

    [ObservableProperty]
    public partial bool IsPassword1 { get; set; } = true;

    [ObservableProperty]
    public partial bool IsPassword2 { get; set; } = true;

    [ObservableProperty]
    public partial bool IsPassword3 { get; set; } = true;

    private readonly ILogger<ChangeMasterPasswordViewModel> _logger;
    private readonly ICoreDataService _coreDataService;
    private readonly IEncryptionService _encryptionService;
    private readonly IAuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;

    public ChangeMasterPasswordViewModel(
        ILogger<ChangeMasterPasswordViewModel> logger,
        ICoreDataService coreDataService,
        IEncryptionService encryptionService,
        IAuthService authService,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _logger = logger;
        _coreDataService = coreDataService;
        _encryptionService = encryptionService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;

        Title = "Cambiar contraseña maestra";
    }

    [RelayCommand]
    public async Task AcceptClickedAsync()
    {
        if (string.IsNullOrEmpty(OldPassword))
        {
            Message = "Debes ingresar la contraseña actual.";
            _logger.LogWarning("[ChangeMasterPasswordViewModel-AcceptClickedAsync] Old password is empty");
            return;
        }

        if (NewPassword.Length < 8)
        {
            Message = "La contraseña nueva debe tener al menos 8 caracteres.";
            _logger.LogWarning("[ChangeMasterPasswordViewModel-AcceptClickedAsync] New password too short");
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            Message = "Las contraseñas nuevas no coinciden.";
            _logger.LogWarning("[ChangeMasterPasswordViewModel-AcceptClickedAsync] Passwords do not match");
            return;
        }

        try
        {
            IsLoading = true;
            Message = "";
            _logger.LogInformation("[ChangeMasterPasswordViewModel-AcceptClickedAsync] Starting master password change");

            // 1) Validar la contraseña actual en el servidor y obtener el IV en uso.
            var currentIv = await _coreDataService.GetCoreUserIVAsync(OldPassword);

            // 2) Descargar los datos cifrados.
            var allData = (await _coreDataService.GetAllCoreDataAsync()).ToList();

            // 3) Desencriptar con la clave actual y su IV.
            var decrypted = _encryptionService.DecryptDataCollection(allData, OldPassword, currentIv.IV).ToList();

            // 4) Generar la nueva sal/IV y re-cifrar los datos con la clave nueva.
            var newIv = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var reEncrypted = _encryptionService.EncryptDataCollection(decrypted, NewPassword, newIv).ToList();

            // 5) Cambio atómico en el servidor: rota clave, sal, token y datos.
            var response = await _coreDataService.ChangeCorePasswordAsync(OldPassword, NewPassword, newIv, reEncrypted);

            // 6) Adoptar la sesión rotada y avisar.
            await _authService.UpdateSqlTokenAsync(response.SqlToken.ToString());

            _logger.LogInformation("[ChangeMasterPasswordViewModel-AcceptClickedAsync] Master password changed successfully");

            Message = "";
            await _dialogService.ShowInfoAsync(
                "Contraseña maestra cambiada correctamente. Las demás sesiones activas quedaron cerradas.",
                "Aviso");
            await _navigationService.PopAsync();
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "[ChangeMasterPasswordViewModel-AcceptClickedAsync] API error: {StatusCode}", ex.StatusCode);
            Message = ex.StatusCode == 401
                ? "La contraseña actual es incorrecta o tu sesión expiró. Vuelve a intentarlo."
                : ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ChangeMasterPasswordViewModel-AcceptClickedAsync] Error changing master password: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
            Message = "No se pudo cambiar la contraseña maestra. Intenta nuevamente.";
        }
        finally
        {
            IsLoading = false;
            OldPassword = "";
            NewPassword = "";
            ConfirmPassword = "";
        }
    }

    [RelayCommand]
    public async Task CancelClickedAsync()
    {
        _logger.LogInformation("[ChangeMasterPasswordViewModel-CancelClickedAsync] Cancelled master password change");

        await _navigationService.PopAsync();

        OldPassword = "";
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

    [RelayCommand]
    public void ToggleIsPassword3()
    {
        IsPassword3 = !IsPassword3;
    }
}