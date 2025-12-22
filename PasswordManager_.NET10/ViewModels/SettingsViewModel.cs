using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;
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
    private string appVersion = "1.0.0";

    [ObservableProperty]
    private string selectedTheme = "Auto";

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

                SelectedTheme = await _themeService.GetThemeAsync();

                // Cargar estado de biometría
                await LoadBiometricStatusAsync();
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

            // Navegar a LoginPage
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Este evento debe ser capturado por AppShell o la app
                SessionExpiredEvent?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SettingsViewModel-PerformAutoLogout] Error during auto logout: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
        }
    }

    /// <summary>
    /// Evento para notificar cuando la sesión expira
    /// </summary>
    public event EventHandler? SessionExpiredEvent;

    /// <summary>
    /// Limpiar recursos cuando se destruye el ViewModel
    /// </summary>
    public void Cleanup()
    {
        _sessionTimer?.Stop();
        _sessionTimer?.Dispose();

        // Limpiar datos para que la próxima sesión cargue nuevos
        UserId = string.Empty;
        Role = string.Empty;
        SqlToken = string.Empty;
        ApiToken = string.Empty;
        SessionTimeRemaining = string.Empty;
        IsSessionExpired = false;

        _logger.LogInformation("[SettingsViewModel-Cleanup] Resources cleaned up");
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

            IsBiometricAvailable = await _biometricService.IsBiometricAvailableAsync();
            IsBiometricEnabled = await _biometricService.IsBiometricEnabledAsync();

            _logger.LogInformation("[SettingsViewModel-LoadBiometricStatusAsync] Biometric - Available: {Available}, Enabled: {Enabled}",
                IsBiometricAvailable, IsBiometricEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SettingsViewModel-LoadBiometricStatusAsync] Error loading biometric status");
            IsBiometricAvailable = false;
            IsBiometricEnabled = false;
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
}