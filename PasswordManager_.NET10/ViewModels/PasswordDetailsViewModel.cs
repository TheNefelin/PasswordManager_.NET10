using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.Services.Interfaces;
using PasswordManager_.NET10.Views.Main;
using System.Collections.ObjectModel;


namespace PasswordManager_.NET10.ViewModels;

public partial class PasswordDetailsViewModel : BaseViewModel
{
    private readonly ILogger<PasswordDetailsViewModel> _logger;
    private readonly ICoreDataService _coreDataService;
    private readonly IEncryptionService _encryptionService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly PasswordFormViewModel _passwordFormViewModel;
    private CancellationTokenSource? _searchCancellationTokenSource;
    private const int SEARCH_DEBOUNCE_MS = 300;

    // ==================== UI STATE ====================
    // Propiedades que controlan solo la visualización

    [ObservableProperty]
    public partial bool IsMenuOpen { get; set; } = false;

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = false;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<CoreSecretData> DisplayedPasswordItems { get; set; } = new();

    private List<CoreSecretData> passwordItems = new();

    // ==================== CONSTRUCTOR ====================
    public PasswordDetailsViewModel(
        ILogger<PasswordDetailsViewModel> logger,
        ICoreDataService coreDataService,
        IEncryptionService encryptionService,
        INavigationService navigationService,
        IDialogService dialogService,
        PasswordFormViewModel passwordFormViewModel)
    {
        _logger = logger;
        _coreDataService = coreDataService;
        _encryptionService = encryptionService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _passwordFormViewModel = passwordFormViewModel;

        Title = "Password Details";

        // Suscribirse a cambios de SearchText con debounce
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SearchText))
            {
                _ = PerformSearchAsync();
            }
        };
    }

    // ==================== UI COMMANDS ====================
    // Comandos que solo afectan la UI (sin llamadas a API/BD)

    [RelayCommand]
    public void ToggleMenu()
    {
        IsMenuOpen = !IsMenuOpen;
    }

    // ==================== BUSINESS LOGIC COMMANDS ====================
    // Comandos que cargan/modifican datos

    [RelayCommand]
    public void ClearPasswords()
    {
        DisplayedPasswordItems.Clear();
        passwordItems.Clear();
    }

    [RelayCommand]
    public async Task DownloadPasswords()
    {
        try
        {
            ToggleMenu();
            IsLoading = true;
            DisplayedPasswordItems.Clear();

            var passwords = await _coreDataService.GetAllCoreDataAsync();
            var items = passwords.ToList();

            passwordItems = items;

            foreach (var item in items)
            {
                DisplayedPasswordItems.Add(item);
            }

            SearchText = string.Empty;
        }
        catch (Exception ex)
        {
            // Mostrar error al usuario
            await _dialogService.ShowErrorAsync(ex.Message);
            // Log para debugging
            _logger.LogError(ex, "[PasswordDetailsViewModel] Error downloading passwords");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ==================== SEARCH LOGIC ====================
    /// <summary>
    /// Realiza búsqueda con debounce (espera 300ms sin escribir antes de buscar)
    /// Busca en Data01 y Data02
    /// </summary>
    private async Task PerformSearchAsync()
    {
        // Cancelar búsqueda anterior
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource = new CancellationTokenSource();

        try
        {
            // Esperar 300ms antes de ejecutar (debounce)
            await Task.Delay(SEARCH_DEBOUNCE_MS, _searchCancellationTokenSource.Token);

            // Filtrar resultados
            DisplayedPasswordItems.Clear();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // Sin filtro, mostrar todos
                foreach (var item in passwordItems)
                {
                    DisplayedPasswordItems.Add(item);
                }
            }
            else
            {
                // Filtrar por búsqueda
                string searchLower = SearchText.ToLower();
                var filtered = passwordItems.Where(p =>
                    p.Data01.ToLower().Contains(searchLower) ||
                    p.Data02.ToLower().Contains(searchLower)
                );

                foreach (var item in filtered)
                {
                    DisplayedPasswordItems.Add(item);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PasswordDetailsViewModel] Search cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PasswordDetailsViewModel] Error during search");
        }
    }

    [RelayCommand]
    public async Task DecryptAll()
    {
        try
        {
            ToggleMenu();

            if (passwordItems.Count > 0 && !_encryptionService.IsEncrypted(passwordItems[0].Data01))
            {
                await _dialogService.ShowInfoAsync(
                    "Los datos ya están desencriptados",
                    "Información"
                );
                return;
            }
            else
            {
                // Pedir contraseña
                var password = await PromptPasswordAsync();
                if (string.IsNullOrEmpty(password)) return;

                IsLoading = true;
                DisplayedPasswordItems.Clear();

                // Obtener IV
                var coreUserIV = await _coreDataService.GetCoreUserIVAsync(password);

                // Desencriptar colección original
                var decryptedItems = _encryptionService.DecryptDataCollection(
                    passwordItems,
                    password,
                    coreUserIV.IV
                ).OrderBy(p => p.Data01).ToList();

                passwordItems = decryptedItems;

                // Actualizar PasswordItems
                foreach (var item in decryptedItems)
                {
                    DisplayedPasswordItems.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Muestra un diálogo para ingresar contraseña
    /// </summary>
    private async Task<string?> PromptPasswordAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        var page = new PasswordPromptPage();
        // Pasar el TaskCompletionSource al page
        page.CompletionSource = tcs;

        await _navigationService.PushModalAsync(page);

        // Esperar a que el usuario responda
        return await tcs.Task;
    }

    [RelayCommand]
    public async Task PromptNewEncryptionKey()
    {
        try
        {
            _ = _navigationService.PushModalAsync<PasswordPromptCreatePage>();

            IsLoading = true;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(ex.Message);
        }
        finally
        {
            ToggleMenu();
            IsLoading = false;
        }
    }

    public override void Cleanup()
    {
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource?.Dispose();
        base.Cleanup();
    }

    [RelayCommand]
    public async Task CreateSecret()
    {
        var tcs = new TaskCompletionSource<CoreSecretData?>();
        var viewModel = _passwordFormViewModel;
        viewModel.CompletionSource = tcs;
        viewModel.InitializeCreate(Guid.Empty);

        var page = new PasswordFormPage(viewModel);
        await _navigationService.PushModalAsync(page);
        var newItem = await tcs.Task;

        SearchText = string.Empty;
        if (newItem == null) return;

        passwordItems.Add(newItem);
    }

    [RelayCommand]
    public async Task EditPassword(CoreSecretData item)
    {
        if (_encryptionService.IsEncrypted(item.Data01))
        {
            await _dialogService.ShowInfoAsync(
                "Debes desencriptarlos antes de editar los datos",
                "Información"
            );
            return;
        };

        var tcs = new TaskCompletionSource<CoreSecretData?>();
        var viewModel = _passwordFormViewModel;
        viewModel.CompletionSource = tcs;
        viewModel.InitializeEdit(item);

        var page = new PasswordFormPage(viewModel);
        await _navigationService.PushModalAsync(page);
        var updatedItem = await tcs.Task;

        SearchText = string.Empty;
        if (updatedItem == null) return;

        item.Data01 = updatedItem!.Data01;
        item.Data02 = updatedItem.Data02;
        item.Data03 = updatedItem.Data03;
    }

    [RelayCommand]
    public async Task DeletePassword(CoreSecretData item)
    {
        if (_encryptionService.IsEncrypted(item.Data01))
        {
            await _dialogService.ShowInfoAsync(
                "Debes desencriptarlos antes de eliminar los datos",
                "Información"
            );
            return;
        };

        // Confirmar eliminación
        bool confirm = await _dialogService.ShowConfirmAsync(
            $"¿Estás seguro de que deseas eliminar '{item.Data01}'?",
            "Confirmar eliminación",
            "Sí, eliminar",
            "Cancelar"
        );

        if (!confirm) return;

        try
        {
            await _coreDataService.DeleteCoreDataAsync(item.Data_Id);

            await _dialogService.ShowInfoAsync("Se ha eliminado correctamente", "Éxito");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(ex.Message);
        }
    }

    [RelayCommand]
    public void TogglePasswordVisibility(CoreSecretData item)
    {
        item.IsPasswordVisible = !item.IsPasswordVisible;
    }

    [RelayCommand]
    public async Task CopyToClipboard(string text)
    {
        await Clipboard.Default.SetTextAsync(text);
    }
}