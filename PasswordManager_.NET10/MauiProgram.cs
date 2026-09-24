using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Helpers;
using PasswordManager_.NET10.Services.Implementation;
using PasswordManager_.NET10.Services.Interfaces;
using PasswordManager_.NET10.ViewModels;
using PasswordManager_.NET10.Views.Authentication;
using PasswordManager_.NET10.Views.Components;
using PasswordManager_.NET10.Views.Main;
using Plugin.Maui.Biometric;

namespace PasswordManager_.NET10;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureServices();

#if DEBUG
		builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    /// <summary>
    /// Registrar todos los servicios de la aplicación
    /// </summary>
    private static MauiAppBuilder ConfigureServices(this MauiAppBuilder builder)
    {
        // Servicios
        builder.Services
            .AddSingleton(sp => CreateApiHttpClient())
            .AddSingleton<ISessionManager, SessionManager>()
            .AddSingleton<IBiometricService, BiometricService>()
            .AddSingleton<ISecureStorageService, SecureStorageService>()
            .AddSingleton<IThemeService, ThemeService>()
            .AddSingleton<IEncryptionService, EncryptionService>()
            .AddSingleton<IApiService, ApiService>()
            .AddSingleton<IAuthService, AuthService>()
            .AddSingleton<ICoreDataService, CoreDataService>()
            .AddSingleton<INavigationService, NavigationService>()
            .AddSingleton<IDialogService, DialogService>();

        // ViewModels (Singleton para screens principales)
        builder.Services
            .AddTransient<RegisterViewModel>()
            .AddTransient<LoginViewModel>()
            .AddTransient<SettingsViewModel>()
            .AddTransient<PasswordDetailsViewModel>()
            .AddTransient<PasswordFormViewModel>()
            .AddTransient<PasswordPromptCreateViewModel>()
            .AddTransient<HelpViewModel>();

        // Views/Pages
        builder.Services
            .AddTransient<AppShell>()
            .AddTransient<RegisterPage>()
            .AddTransient<LoginPage>()
            .AddTransient<SettingsPage>()
            .AddTransient<PasswordDetailsPage>()
            .AddTransient<PasswordFormPage>()
            .AddTransient<PasswordPromptCreatePage>()
            .AddTransient<HelpPage>();

        // Test
        builder.Services
            .AddSingleton<TestingViewModel>()
            .AddTransient<TestingPage>();

        // Biometría del plugin
        builder.Services.AddSingleton<IBiometric>(BiometricAuthenticationService.Default);

        return builder;
    }

    /// <summary>
    /// Crea el HttpClient de la API con la configuración base (BaseAddress, headers y timeout).
    /// El bypass de validación de certificados solo se habilita en DEBUG para desarrollo.
    /// </summary>
    private static HttpClient CreateApiHttpClient()
    {
#if DEBUG
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
#else
        var handler = new HttpClientHandler();
#endif

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(Constants.API_BASE_URL),
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.Add("ApiKey", Constants.API_KEY);
        client.DefaultRequestHeaders.Add("User-Agent", "PasswordManager-MAUI/1.0");

        return client;
    }
}
