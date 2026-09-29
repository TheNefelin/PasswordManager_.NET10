using PasswordManager_.NET10.ViewModels;

namespace PasswordManager_.NET10.Views.Main;

public partial class ChangeMasterPasswordPage : ContentPage
{
    public ChangeMasterPasswordPage(ChangeMasterPasswordViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}