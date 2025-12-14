using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordManager_.NET10.ViewModels;

/// <summary>
/// ViewModel base con propiedades comunes
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string title = string.Empty;

    public virtual void Cleanup()
    {
        // Método virtual para que las clases derivadas lo implementen si lo necesitan
    }
}