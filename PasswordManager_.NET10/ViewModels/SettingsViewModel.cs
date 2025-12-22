using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;
using PasswordManager_.NET10.Views.Authentication;
using PasswordManager_.NET10.Views.Main;

namespace PasswordManager_.NET10.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IAuthService _authService;
    private readonly IThemeService _themeService;
    private readonly IBiometricService _biometricService;
    private readonly IServiceProvider _serviceProvider;
    private System.Timers.Timer _sessionTimer;
    private int _secondsRemaining;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string userId = string.Empty;

    [ObservableProperty]
    private string role = string.Empty;

    [ObservableProperty]
    private string sqlToken = string.Empty;

    [ObservableProperty]
    private string apiToken = string.Empty;

    [ObservableProperty]
    private string sessionTimeRemaining = string.Empty;

    [ObservableProperty]
    private bool isSessionExpired = false;

    [ObservableProperty]
    private bool isBiometricEnabled = false;

    [ObservableProperty]
    private bool isBiometricAvailable;

    [ObservableProperty]
    private string appVersion = "Beta 1.0.0";

    [ObservableProperty]
    private string selectedTheme = "Auto";

    [ObservableProperty]
    private bool isSavePasswordEnabled = false;

    private bool _isInitializing = false;

    private readonly string[] _themes = { "Auto", "Light", "Dark" };

    public SettingsViewModel(
        ILogger<SettingsViewModel> logger,
        IAuthService authService,
        IThemeService themeService,
        IBiometricService biometricService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _authService = authService;
        _themeService = themeService;
        _biometricService = biometricService;
        _serviceProvider = serviceProvider;

        Title = "Settings";
    }

    partial void OnIsSavePasswordEnabledChanged(bool oldValue, bool newValue)
    {
        // Si estamos inicializando, ignorar el cambio
        if (_isInitializing)
            return;

        // Solo ejecutar si el usuario cambió el valor manualmente
        // (no cuando se carga desde storage)
        if (oldValue != newValue)
        {
            _ = ToggleSavePasswordAsync();
        }
    }

    /// <summary>
    /// Cargar datos de la sesión actual y estado de biometría
    /// </summary>
    public async Task LoadSessionDataAsync()
    {
        try
        {
            IsLoading = true;
            _logger.LogInformation("[SettingsViewModel-LoadSessionDataAsync] Loading session data");

            var currentUser = await _authService.GetCurrentUserAsync();

            if (currentUser != null)
            {
                UserId = currentUser.UserId.ToString();
                Role = currentUser.Role;
                SqlToken = currentUser.SqlToken;
                ApiToken = currentUser.ApiToken;

                // Calcular tiempo restante
                var timeRemaining = currentUser.TokenExpiry - DateTime.UtcNow;
                if (timeRemaining.TotalSeconds > 0)
                {
                    _secondsRemaining = (int)timeRemaining.TotalSeconds;
                    StartSessionTimer();
                }
                else
                {
                    IsSessionExpired = true;
                    SessionTimeRemaining = "Sesión expirada";
                    _logger.LogWarning("[SettingsViewModel-LoadSessionDataAsync] Session already expired");
                }

                _logger.LogInformation("[SettingsViewModel-LoadSessionDataAsync] Session data loaded successfully");

                // Cargar tema y biometría en paralelo (no secuencial)
                var themeTask = _themeService.GetThemeAsync();
                var biometricTask = LoadBiometricStatusAsync();

                await Task.WhenAll(themeTask, biometricTask);

                SelectedTheme = await themeTask;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SettingsViewModel-LoadSessionDataAsync] Error loading session data: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Iniciar timer de cuenta regresiva
    /// </summary>
    private void StartSessionTimer()
    {
        _sessionTimer = new System.Timers.Timer(1000); // Cada segundo
        _sessionTimer.Elapsed += (s, e) => UpdateSessionTime();
        _sessionTimer.AutoReset = true;
        _sessionTimer.Start();

        UpdateSessionTime(); // Actualizar inmediatamente
    }

    /// <summary>
    /// Actualizar tiempo de sesión
    /// </summary>
    private void UpdateSessionTime()
    {
        _secondsRemaining--;

        if (_secondsRemaining <= 0)
        {
            _sessionTimer?.Stop();
            _sessionTimer?.Dispose();

            IsSessionExpired = true;
            SessionTimeRemaining = "Sesión expirada";

            _logger.LogWarning("[SettingsViewModel-UpdateSessionTime] Session expired");

            // Ejecutar logout automático
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await PerformAutoLogout();
            });
        }
        else
        {
            var timeSpan = TimeSpan.FromSeconds(_secondsRemaining);
            SessionTimeRemaining = $"{timeSpan.Hours:D2}h {timeSpan.Minutes:D2}m {timeSpan.Seconds:D2}s";
        }
    }

    /// <summary>
    /// Logout automático cuando expira la sesión
    /// </summary>
    private async Task PerformAutoLogout()
    {
        try
        {
            _logger.LogInformation("[SettingsViewModel-PerformAutoLogout] Auto logout triggered");

            await _authService.LogoutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SettingsViewModel-PerformAutoLogout] Error during auto logout: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
        }
    }

    [RelayCommand]
    public async Task ChangeThemeAsync(string theme)
    {
        try
        {
            SelectedTheme = theme;
            await _themeService.SetThemeAsync(theme);
            _logger.LogInformation("[SettingsViewModel-ChangeThemeAsync] Theme changed: {Theme}", theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SettingsViewModel-ChangeThemeAsync] Error changing theme: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Cargar estado de biometría disponible y habilitado
    /// </summary>
    private async Task LoadBiometricStatusAsync()
    {
        try
        {
            _logger.LogInformation("[SettingsViewModel-LoadBiometricStatusAsync] Loading biometric status");

            _isInitializing = true; // Indicar que estamos cargando

            //IsBiometricAvailable = await _biometricService.IsBiometricAvailableAsync();
            IsBiometricAvailable = true;
            IsBiometricEnabled = await _biometricService.IsBiometricEnabledAsync();
            IsSavePasswordEnabled = await _authService.HasSavedPasswordAsync();

            _isInitializing = false; // Fin de carga

            _logger.LogInformation("[SettingsViewModel-LoadBiometricStatusAsync] Biometric - Available: {Available}, Enabled: {Enabled}", IsBiometricAvailable, IsBiometricEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SettingsViewModel-LoadBiometricStatusAsync] Error loading biometric status");
            IsSavePasswordEnabled = false;
            IsBiometricEnabled = false;
            IsBiometricAvailable = false;
        }
    }

    /// <summary>
    /// Toggle de biometría
    /// </summary>
    [RelayCommand]
    public async Task ToggleBiometricAsync()
    {
        try
        {
            _logger.LogInformation("[SettingsViewModel-ToggleBiometricAsync] Toggling biometric. Current state: {CurrentState}", IsBiometricEnabled);

            await _biometricService.EnableBiometricAsync(IsBiometricEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SettingsViewModel-ToggleBiometricAsync] Error toggling biometric: {Message}", ex.Message);
            IsBiometricAvailable = false;
            IsBiometricEnabled = false;
        }
    }

    [RelayCommand]
    public async Task GoToHelpAsync()
    {
        var helpPage = _serviceProvider.GetRequiredService<HelpPage>();
        await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(helpPage);
    }

    /// <summary>
    /// Toggle para guardar contraseña - requiere reinicio de sesión
    /// </summary>
    [RelayCommand]
    public async Task ToggleSavePasswordAsync()
    {
        try
        {
            _logger.LogInformation("[SettingsViewModel-ToggleSavePasswordAsync] Toggle save password. Current: {Current}", IsSavePasswordEnabled);

            if (IsSavePasswordEnabled)
            {
                // Usuario intenta ACTIVAR el switch
                // Mostrar alerta informativa
                bool confirmed = await Application.Current!.MainPage!.DisplayAlertAsync(
                    "Guardar Contraseña",
                    "Para completar este proceso debes iniciar sesión nuevamente.\n\nLuego de esto, la autenticación por biometría estará habilitada para tu próximo login.",
                    "Continuar",
                    "Cancelar"
                );

                if (!confirmed)
                {
                    _logger.LogInformation("[SettingsViewModel-ToggleSavePasswordAsync] User cancelled password save");
                    IsSavePasswordEnabled = false;
                    return;
                }

                // Establecer flag antes de logout
                await _authService.SetSavePasswordOnNextLoginAsync(true);
                _logger.LogInformation("[SettingsViewModel-ToggleSavePasswordAsync] Flag set to save password on next login");

                // Ejecutar logout
                await PerformLogoutForPasswordSave();
            }
            else
            {
                // Usuario intenta DESACTIVAR - simplemente borra la contraseña guardada
                await _authService.ClearSavedPasswordAsync();
                await _authService.SetSavePasswordOnNextLoginAsync(false);
                _logger.LogInformation("[SettingsViewModel-ToggleSavePasswordAsync] Saved password cleared");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SettingsViewModel-ToggleSavePasswordAsync] Error: {Message}", ex.Message);
            IsSavePasswordEnabled = false;
        }
    }

    /// <summary>
    /// Realizar logout para guardar contraseña
    /// </summary>
    private async Task PerformLogoutForPasswordSave()
    {
        try
        {
            _logger.LogInformation("[SettingsViewModel-PerformLogoutForPasswordSave] Performing logout for password save");

            // Limpiar sesión actual
            await _authService.LogoutAsync();

            // Navegar a LoginPage
            var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
            Application.Current!.MainPage = loginPage;

            _logger.LogInformation("[SettingsViewModel-PerformLogoutForPasswordSave] Logout completed, navigating to login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SettingsViewModel-PerformLogoutForPasswordSave] Error during logout: {Message}", ex.Message);
            await Application.Current!.MainPage!.DisplayAlertAsync("Error", "Ocurrió un error al procesar tu solicitud", "OK");
        }
    }
}