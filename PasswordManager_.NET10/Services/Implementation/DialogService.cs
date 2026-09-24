using Microsoft.Maui.Controls;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Services.Implementation;

/// <summary>
/// Centraliza los diálogos sobre la página actual de la ventana activa.
/// </summary>
public class DialogService : IDialogService
{
    public Task ShowErrorAsync(string message, string title = "Error")
    {
        return Application.Current!.Windows[0].Page!.DisplayAlertAsync(title, message, "OK");
    }

    public Task ShowInfoAsync(string message, string title = "Información", string cancel = "OK")
    {
        return Application.Current!.Windows[0].Page!.DisplayAlertAsync(title, message, cancel);
    }

    public Task<bool> ShowConfirmAsync(string message, string title, string accept, string cancel)
    {
        return Application.Current!.Windows[0].Page!.DisplayAlertAsync(title, message, accept, cancel);
    }
}