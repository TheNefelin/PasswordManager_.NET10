using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Tests.Fakes;

public class FakeDialogService : IDialogService
{
    public bool ConfirmResult { get; set; } = true;
    public int ErrorCount { get; private set; }
    public int InfoCount { get; private set; }
    public int ConfirmCount { get; private set; }

    public Task ShowErrorAsync(string message, string title = "Error")
    {
        ErrorCount++;
        return Task.CompletedTask;
    }

    public Task ShowInfoAsync(string message, string title = "Información", string cancel = "OK")
    {
        InfoCount++;
        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmAsync(string message, string title, string accept, string cancel)
    {
        ConfirmCount++;
        return Task.FromResult(ConfirmResult);
    }
}