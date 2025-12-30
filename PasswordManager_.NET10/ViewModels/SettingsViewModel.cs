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
    private CancellationTokenSource _timerCts; // Para cancelar el timer

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
    private string appVersion = "Beta 1.0.1";

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
        _timerCts = new CancellationTokenSource();

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

                // Calcular tiempo restante
                var timeRemaining = currentUser.TokenExpiry - DateTime.UtcNow;
                if (timeRemaining.TotalSeconds > 0)
                {
                    _secondsRemaining = (int)timeRemaining.TotalSeconds;

                    // IMPORTANTE: Detener timer anterior si existe
                    StopSessionTimer();

                    // Crear nuevo CancellationTokenSource
                    _timerCts = new CancellationTokenSource();

                    StartSessionTimer();
                }
                else
                {
                    IsSessionExpired = true;
                    SessionTimeRemaining = "Sesión expirada";
                    _logger.LogWarning("[SettingsViewModel-LoadSessionDataAsync] Session already expired");
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
    /// Iniciar timer de cuenta regresiva - Versión corregida con mejor manejo de threading
    /// </summary>
    private void StartSessionTimer()
    {
        _sessionTimer = new System.Timers.Timer(1000); // Cada segundo
        _sessionTimer.Elapsed += SessionTimer_Elapsed;
        _sessionTimer.AutoReset = true;
        _sessionTimer.Start();

        UpdateSessionTime(); // Actualizar inmediatamente
    }

    /// <summary>
    /// Handler del timer - evita múltiples callbacks simultáneos
    /// </summary>
    private void SessionTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        if (_timerCts.Token.IsCancellationRequested)
            return;

        UpdateSessionTime();
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

        _timerCts?.Cancel();
    }

    /// <summary>
    /// Actualizar tiempo de sesión
    /// </summary>
    private void UpdateSessionTime()
    {
        _secondsRemaining--;

        if (_secondsRemaining <= 0)
        {
            StopSessionTimer();

            IsSessionExpired = true;
            SessionTimeRemaining = "Sesión expirada";

            _logger.LogWarning("[SettingsViewModel-UpdateSessionTime] Session expired");

            // Ejecutar logout automático en el main thread
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await PerformAutoLogout();
            });
        }
        else
        {
            var timeSpan = TimeSpan.FromSeconds(_secondsRemaining);
            string newTimeRemaining = $"{timeSpan.Hours:D2}h {timeSpan.Minutes:D2}m {timeSpan.Seconds:D2}s";

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

    /// <summary>
    /// Logout automático cuando expira la sesión
    /// </summary>
    private async Task PerformAutoLogout()
    {
        try
        {
            _logger.LogInformation("[SettingsViewModel-PerformAutoLogout] Auto logout triggered");
            StopSessionTimer();
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
        var helpPage = _serviceProvider.GetRequiredService<HelpPage>();
        await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(helpPage);
    }

    [RelayCommand]
    public async Task ToggleSavePasswordAsync()
    {
        try
        {
            _logger.LogInformation("[SettingsViewModel-ToggleSavePasswordAsync] Toggle save password. Current: {Current}", IsSavePasswordEnabled);

            if (IsSavePasswordEnabled)
            {
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