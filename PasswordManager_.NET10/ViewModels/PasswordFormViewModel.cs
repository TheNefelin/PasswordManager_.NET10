using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.ViewModels;

public partial class PasswordFormViewModel : BaseViewModel
{
    private readonly ILogger<PasswordDetailsViewModel> _logger;
    private readonly ICoreDataService _coreDataService;
    private readonly IEncryptionService _encryptionService;

    // ==================== UI STATE ====================
    [ObservableProperty]
    string data01 = string.Empty; // Título/Nombre

    [ObservableProperty]
    string data02 = string.Empty; // Usuario

    [ObservableProperty]
    string data03 = string.Empty; // Contraseña

    [ObservableProperty]
    string encryptingPassword = string.Empty;

    [ObservableProperty]
    bool isPassword = true;

    [ObservableProperty]
    bool isPasswordEncrypt = true;

    [ObservableProperty]
    bool isLoading = false;

    [ObservableProperty]
    bool isEditing = false; // true si estamos editando, false si creando

    public TaskCompletionSource<CoreSecretData?>? CompletionSource { get; set; }
    private CoreSecretData? _currentItem;

    public PasswordFormViewModel(
        ILogger<PasswordDetailsViewModel> logger,
        ICoreDataService coreDataService,
        IEncryptionService encryptionService)
    {
        _logger = logger;
        _coreDataService = coreDataService;
        _encryptionService = encryptionService;

        Title = "Nueva Contraseña";
    }

    // ==================== PUBLIC METHODS ====================
    /// <summary>
    /// Inicializa el formulario para crear una nueva contraseña
    /// </summary>
    public void InitializeCreate(Guid userId)
    {
        _currentItem = null;
        IsEditing = false;
        Title = "Nueva Contraseña";
        Data01 = string.Empty;
        Data02 = string.Empty;
        Data03 = string.Empty;
    }

    /// <summary>
    /// Inicializa el formulario para editar una contraseña existente
    /// </summary>
    public void InitializeEdit(CoreSecretData item)
    {
        _currentItem = item;
        IsEditing = true;
        Title = "Editar Contraseña";
        Data01 = item.Data01;
        Data02 = item.Data02;
        Data03 = item.Data03;
    }

    // ==================== COMMANDS ====================
    [RelayCommand]
    public async Task SavePassword()
    {
        try
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(Data01) ||
                string.IsNullOrWhiteSpace(Data02) ||
                string.IsNullOrWhiteSpace(Data03) ||
                string.IsNullOrWhiteSpace(EncryptingPassword))
            {
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
                    "Validación",
                    "Todos los campos son obligatorios",
                    "OK"
                );
                return;
            }

            IsLoading = true;

            if (IsEditing && _currentItem != null)
            {
                // ==================== UPDATE ====================
                _currentItem.Data01 = Data01;
                _currentItem.Data02 = Data02;
                _currentItem.Data03 = Data03;

                var returnItem = new CoreSecretData() { 
                    Data_Id = _currentItem.Data_Id,
                    Data01 = _currentItem.Data01,
                    Data02 = _currentItem.Data02,
                    Data03 = _currentItem.Data03,
                    User_Id = _currentItem.User_Id
                };

                var coreUserIV = await _coreDataService.GetCoreUserIVAsync(EncryptingPassword);
                var newItemEncrypted = _encryptionService.EncryptData(_currentItem, EncryptingPassword, coreUserIV.IV);
                var result = await _coreDataService.UpdateCoreDataAsync(newItemEncrypted);

                CompletionSource?.SetResult(returnItem);

                await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
                    "Éxito",
                    "Contraseña actualizada correctamente",
                    "OK"
                );
            }
            else
            {
                // ==================== CREATE ====================
                var newItem = new CoreSecretData
                {
                    Data_Id = Guid.NewGuid(),
                    Data01 = Data01,
                    Data02 = Data02,
                    Data03 = Data03,
                    User_Id = Guid.Empty
                };

                var returnItem = new CoreSecretData()
                {
                    Data_Id = newItem.Data_Id,
                    Data01 = newItem!.Data01,
                    Data02 = newItem.Data02,
                    Data03 = newItem.Data03,
                    User_Id = newItem.User_Id
                };

                var coreUserIV = await _coreDataService.GetCoreUserIVAsync(EncryptingPassword);
                var newItemEncrypted = _encryptionService.EncryptData(newItem, EncryptingPassword, coreUserIV.IV);
                var result = await _coreDataService.CreateCoreDataAsync(newItemEncrypted);

                returnItem.User_Id = result.User_Id;
                returnItem.Data_Id = result.Data_Id;

                CompletionSource?.SetResult(returnItem);

                await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
                    "Éxito",
                    "Contraseña creada correctamente",
                    "OK"
                );
            }

            // Volver a la página anterior
            //await Shell.Current.GoToAsync("..");
            await Application.Current!.Windows[0].Page!.Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
                "Error",
                ex.Message,
                "OK"
            );
            _logger.LogError(ex, "[PasswordFormViewModel] Error saving password");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task Cancel()
    {
        await Shell.Current.GoToAsync("..");
        CompletionSource?.SetResult(null);
        //await Application.Current!.Windows[0].Page!.Navigation.PopModalAsync();
    }

    [RelayCommand]
    public void GeneratePassword()
    {
        // Generar contraseña aleatoria de 16 caracteres
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%";
        var random = new Random();
        Data03 = new string(Enumerable.Range(0, 16)
            .Select(_ => chars[random.Next(chars.Length)])
            .ToArray());
    }

    [RelayCommand]
    public void ToggleIsPassword()
    {
        IsPassword = !IsPassword;
    }

    [RelayCommand]
    public void ToggleIsPasswordEncrypt()
    {
        IsPasswordEncrypt = !IsPasswordEncrypt;
    }
}
