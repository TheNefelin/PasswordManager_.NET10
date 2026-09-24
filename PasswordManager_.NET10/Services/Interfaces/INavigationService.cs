using Microsoft.Maui.Controls;

namespace PasswordManager_.NET10.Services.Interfaces;

public interface INavigationService
{
    Task GoToAppShellAsync();
    Task GoToLoginAsync();
    Task PushModalAsync<T>() where T : Page;
    Task PushModalAsync(Page page);
    Task PopModalAsync();
    Task PopAsync();
}