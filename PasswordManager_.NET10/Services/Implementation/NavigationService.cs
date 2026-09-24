using Microsoft.Maui.Controls;
using PasswordManager_.NET10.Services.Interfaces;
using PasswordManager_.NET10.Views.Authentication;

namespace PasswordManager_.NET10.Services.Implementation;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task GoToAppShellAsync()
    {
        var appShell = _serviceProvider.GetRequiredService<AppShell>();
        Application.Current!.Windows[0].Page = appShell;
        return Task.CompletedTask;
    }

    public Task GoToLoginAsync()
    {
        var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
        Application.Current!.Windows[0].Page = loginPage;
        return Task.CompletedTask;
    }

    public async Task PushModalAsync<T>() where T : Page
    {
        var page = _serviceProvider.GetRequiredService<T>();
        await PushModalAsync(page);
    }

    public async Task PushModalAsync(Page page)
    {
        await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(page);
    }

    public async Task PopModalAsync()
    {
        await Application.Current!.Windows[0].Page!.Navigation.PopModalAsync();
    }

    public async Task PopAsync()
    {
        await Application.Current!.Windows[0].Page!.Navigation.PopAsync();
    }
}