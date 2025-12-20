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
    private readonly IServiceProvider _serviceProvider;
    private CancellationTokenSource? _searchCancellationTokenSource;
    private const int SEARCH_DEBOUNCE_MS = 300;

    // ==================== UI STATE ====================
    // Propiedades que controlan solo la visualización

    [ObservableProperty]
    bool isMenuOpen = false;

    [ObservableProperty]
    bool isLoading = false;

    [ObservableProperty]
    bool isPassword = true;

    [ObservableProperty]
    string searchText = string.Empty;

    [ObservableProperty]
    ObservableCollection<CoreSecretData> displayedPasswordItems = new();

    private List<CoreSecretData> passwordItems = new();

    // ==================== CONSTRUCTOR ====================
    public PasswordDetailsViewModel(
        ILogger<PasswordDetailsViewModel> logger,
        ICoreDataService coreDataService,
        IEncryptionService encryptionService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _coreDataService = coreDataService;
        _encryptionService = encryptionService;
        _serviceProvider = serviceProvider;

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
            await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
                "Error",
                ex.Message,
                "OK"
            );
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
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
                    "Información",
                    "Los datos ya están desencriptados",
                    "OK"
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
            await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Error", ex.Message, "OK");
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

        await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(page);

        // Esperar a que el usuario responda
        return await tcs.Task;
    }

    [RelayCommand]
    public async Task PromptNewEncryptionKey()
    {
        try
        {
            var page = _serviceProvider.GetRequiredService<PasswordPromptCreatePage>();
            _ = Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(page);

            IsLoading = true;
        }
        catch (Exception ex)
        {
            await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Error", ex.Message, "OK");
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
        var viewModel = new PasswordFormViewModel(_logger, _coreDataService, _encryptionService);
        viewModel.InitializeCreate(Guid.Empty);

        var page = new PasswordFormPage(viewModel);
        await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(page);
    }

    [RelayCommand]
    public async Task EditPassword(CoreSecretData item)
    {
        if (_encryptionService.IsEncrypted(item.Data01))
        {
            await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
                "Información",
                "Debes desencriptarlos antes de editar los datos",
                "OK"
            );
            return;
        };

        var viewModel = new PasswordFormViewModel(_logger, _coreDataService, _encryptionService);
        viewModel.InitializeEdit(item);

        var page = new PasswordFormPage(viewModel);
        await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(page);
    }

    [RelayCommand]
    public async Task DeletePassword(CoreSecretData item)
    {
        if (_encryptionService.IsEncrypted(item.Data01))
        {
            await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
                "Información",
                "Debes desencriptarlos antes de eliminar los datos",
                "OK"
            );
            return;
        };

        // Confirmar eliminación
        bool confirm = await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
            "Confirmar eliminación",
            $"¿Estás seguro de que deseas eliminar '{item.Data01}'?",
            "Sí, eliminar",
            "Cancelar"
        );

        if (!confirm) return;

        try
        {
            var message = await _coreDataService.DeleteCoreDataAsync(item.Data_Id);

            await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
                "Éxito",
                message,
                "OK"
            );
        }
        catch (Exception ex)
        {
            await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    [RelayCommand]
    public void ToggleIsPassword()
    {
        IsPassword = !IsPassword;
    }
}