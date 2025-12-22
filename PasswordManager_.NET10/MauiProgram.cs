using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
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
            .ConfigureServices(); ;

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
            .AddSingleton<HttpClient>()
            .AddSingleton<IBiometricService, BiometricService>()
            .AddSingleton<ISecureStorageService, SecureStorageService>()
            .AddSingleton<IThemeService, ThemeService>()
            .AddSingleton<IEncryptionService, EncryptionService>()
            .AddSingleton<IApiService, ApiService>()
            .AddSingleton<IAuthService, AuthService>()
            .AddSingleton<ICoreDataService, CoreDataService>();

        // ViewModels (Singleton para screens principales)
        builder.Services
            .AddTransient<RegisterViewModel>()
            .AddTransient<LoginViewModel>()
            .AddSingleton<SettingsViewModel>()
            .AddTransient<PasswordDetailsViewModel>()
            .AddTransient<PasswordFormViewModel>()
            .AddTransient<PasswordPromptCreateViewModel>()
            .AddTransient<HelpViewModel>();

        // Views/Pages
        builder.Services
            .AddTransient<AppShell>()
            .AddTransient<RegisterPage>()
            .AddTransient<LoginPage>()
            .AddSingleton<SettingsPage>()
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
}
