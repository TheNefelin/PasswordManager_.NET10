using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Helpers;
using PasswordManager_.NET10.Services.Interfaces;
using PasswordManager_.NET10.Views.Main;

namespace PasswordManager_.NET10.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IAuthService _authService;
    private readonly IThemeService _themeService;
    private readonly IBiometricService _biometricService;
    private readonly ISessionManager _sessionManager;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private System.Timers.Timer? _sessionTimer;

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = false;

    [ObservableProperty]
    public partial string UserId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Role { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SqlToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ApiToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SessionTimeRemaining { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSessionExpired { get; set; } = false;

    [ObservableProperty]
    public partial bool IsBiometricEnabled { get; set; } = false;

    [ObservableProperty]
    public partial bool IsBiometricAvailable { get; set; }

    [ObservableProperty]
    public partial string AppVersion { get; set; } = Constants.APP_VERSION;

    [ObservableProperty]
    public partial string SelectedTheme { get; set; } = "Dark";

    [ObservableProperty]
    public partial bool IsSavePasswordEnabled { get; set; } = false;

    private bool _isInitializing = false;

    public SettingsViewModel(
        ILogger<SettingsViewModel> logger,
        IAuthService authService,
        IThemeService themeService,
        IBiometricService biometricService,
        ISessionManager sessionManager,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _logger = logger;
        _authService = authService;
        _themeService = themeService;
        _biometricService = biometricService;
        _sessionManager = sessionManager;
        _navigationService = navigationService;
        _dialogService = dialogService;

        Title = "Settings";
    }

    partial void OnIsBiometricEnabledChanged(bool oldValue, bool newValue)
    {
        if (_isInitializing)
            return;

        // Si se desactiva biometría, desactivar automáticamente "Guardar contraseña"
        if (!newValue && IsSavePasswordEnabled)
        {
            _isInitializing = true;
            IsSavePasswordEnabled = false;
            _isInitializing = false;

            _logger.LogInformation("[SettingsViewModel-OnIsBiometricEnabledChanged] Biometric disabled, auto-disabling save password");
        }
    }

    partial void OnIsSavePasswordEnabledChanged(bool oldValue, bool newValue)
    {
        if (_isInitializing)
            return;

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

                // Determinar estado de sesión desde el manager
                if (await _sessionManager.IsSessionExpiredAsync())
                {
                    IsSessionExpired = true;
                    SessionTimeRemaining = "Sesión expirada";
                    _logger.LogWarning("[SettingsViewModel-LoadSessionDataAsync] Session already expired");
                }
                else
                {
                    StopSessionTimer();
                    StartSessionTimer();
                }

                _logger.LogInformation("[SettingsViewModel-LoadSessionDataAsync] Session data loaded successfully");

                // Cargar tema y biometría en paralelo
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
    /// Iniciar timer de cuenta regresiva (solo refresco visual)
    /// </summary>
    private void StartSessionTimer()
    {
        _sessionTimer = new System.Timers.Timer(1000);
        _sessionTimer.Elapsed += SessionTimer_Elapsed;
        _sessionTimer.AutoReset = true;
        _sessionTimer.Start();

        _ = RefreshSessionTimeAsync(); // Actualizar inmediatamente
    }

    /// <summary>
    /// Handler del timer - delega al manager para obener el estado de expiración real
    /// </summary>
    private void SessionTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _ = RefreshSessionTimeAsync();
    }

    /// <summary>
    /// Detener el timer de manera segura
    /// </summary>
    private void StopSessionTimer()
    {
        if (_sessionTimer != null)
        {
            _sessionTimer.Stop();
            _sessionTimer.Dispose();
            _sessionTimer = null;
        }
    }

    /// <summary>
    /// Actualizar tiempo de sesión usando el SessionManager como fuente de verdad
    /// </summary>
    private async Task RefreshSessionTimeAsync()
    {
        try
        {
            var timeRemaining = await _sessionManager.GetRemainingTimeAsync();

            // Sin sesión activa (ej. logout manual ya limpió el usuario): detener el timer sin diálogo
            if (timeRemaining <= TimeSpan.Zero && !await _authService.IsAuthenticatedAsync())
            {
                StopSessionTimer();
                return;
            }

            if (timeRemaining <= TimeSpan.Zero)
            {
                StopSessionTimer();

                _logger.LogWarning("[SettingsViewModel-RefreshSessionTime] Session expired");

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    IsSessionExpired = true;
                    SessionTimeRemaining = "Sesión expirada";
                    await _sessionManager.PerformFullLogoutAsync("La sesión ha expirado");
                });
            }
            else
            {
                string newTimeRemaining = $"{timeRemaining.Hours:D2}h {timeRemaining.Minutes:D2}m {timeRemaining.Seconds:D2}s";

                // Actualizar solo si cambió para evitar flickering
                if (SessionTimeRemaining != newTimeRemaining)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SessionTimeRemaining = newTimeRemaining;
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SettingsViewModel-RefreshSessionTime] Error updating session time");
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

    private async Task LoadBiometricStatusAsync()
    {
        try
        {
            _logger.LogInformation("[SettingsViewModel-LoadBiometricStatusAsync] Loading biometric status");

            _isInitializing = true;

            IsBiometricAvailable = await _biometricService.IsBiometricAvailableAsync();
            IsBiometricEnabled = await _biometricService.IsBiometricEnabledAsync();
            IsSavePasswordEnabled = await _authService.HasSavedPasswordAsync();

            _isInitializing = false;

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

    [RelayCommand]
    public async Task ToggleBiometricAsync()
    {
        try
        {
            _logger.LogInformation("[SettingsViewModel-ToggleBiometricAsync] Toggling biometric. Current state: {CurrentState}", IsBiometricEnabled);

            // Si se desactiva biometría
            if (!IsBiometricEnabled)
            {
                // Desactivar automáticamente "Guardar contraseña"
                if (IsSavePasswordEnabled)
                {
                    _isInitializing = true;
                    IsSavePasswordEnabled = false;
                    _isInitializing = false;

                    // Limpiar contraseña guardada
                    await _authService.ClearSavedPasswordAsync();
                    await _authService.SetSavePasswordOnNextLoginAsync(false);

                    _logger.LogInformation("[SettingsViewModel-ToggleBiometricAsync] Biometric disabled, auto-disabling and clearing save password");
                }
            }

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
        await _navigationService.PushModalAsync<HelpPage>();
    }

    [RelayCommand]
    public async Task ToggleSavePasswordAsync()
    {
        try
        {
            _logger.LogInformation("[SettingsViewModel-ToggleSavePasswordAsync] Toggle save password. Current: {Current}", IsSavePasswordEnabled);

            if (IsSavePasswordEnabled)
            {
                bool confirmed = await _dialogService.ShowConfirmAsync(
                    "Para completar este proceso debes iniciar sesión nuevamente.\n\nLuego de esto, la autenticación por biometría estará habilitada para tu próximo login.",
                    "Guardar Contraseña",
                    "Continuar",
                    "Cancelar"
                );

                if (!confirmed)
                {
                    _logger.LogInformation("[SettingsViewModel-ToggleSavePasswordAsync] User cancelled password save");
                    IsSavePasswordEnabled = false;
                    return;
                }

                await _authService.SetSavePasswordOnNextLoginAsync(true);
                _logger.LogInformation("[SettingsViewModel-ToggleSavePasswordAsync] Flag set to save password on next login");

                await PerformLogoutForPasswordSave();
            }
            else
            {
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

    private async Task PerformLogoutForPasswordSave()
    {
        try
        {
            _logger.LogInformation("[SettingsViewModel-PerformLogoutForPasswordSave] Performing logout for password save");

            StopSessionTimer();
            await _authService.LogoutAsync();

            await _navigationService.GoToLoginAsync();

            _logger.LogInformation("[SettingsViewModel-PerformLogoutForPasswordSave] Logout completed, navigating to login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SettingsViewModel-PerformLogoutForPasswordSave] Error during logout: {Message}", ex.Message);
            await _dialogService.ShowErrorAsync("Ocurrió un error al procesar tu solicitud");
        }
    }
}