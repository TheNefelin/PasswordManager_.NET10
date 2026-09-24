using Microsoft.Maui.Controls;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Tests.Fakes;

public class FakeNavigationService : INavigationService
{
    public int GoToLoginCount { get; private set; }
    public int GoToAppShellCount { get; private set; }
    public int PushModalCount { get; private set; }
    public int PopModalCount { get; private set; }
    public int PopCount { get; private set; }

    public Task GoToAppShellAsync()
    {
        GoToAppShellCount++;
        return Task.CompletedTask;
    }

    public Task GoToLoginAsync()
    {
        GoToLoginCount++;
        return Task.CompletedTask;
    }

    public Task PushModalAsync<T>() where T : Page
    {
        PushModalCount++;
        return Task.CompletedTask;
    }

    public Task PushModalAsync(Page page)
    {
        PushModalCount++;
        return Task.CompletedTask;
    }

    public Task PopModalAsync()
    {
        PopModalCount++;
        return Task.CompletedTask;
    }

    public Task PopAsync()
    {
        PopCount++;
        return Task.CompletedTask;
    }
}