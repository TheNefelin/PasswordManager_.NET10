using Microsoft.Maui.Controls;

namespace PasswordManager_.NET10.Services.Interfaces;

public interface IDialogService
{
    Task ShowErrorAsync(string message, string title = "Error");
    Task ShowInfoAsync(string message, string title = "Información", string cancel = "OK");
    Task<bool> ShowConfirmAsync(string message, string title, string accept, string cancel);
}