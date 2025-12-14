using PasswordManager_.NET10.ViewModels;

namespace PasswordManager_.NET10.Views.Authentication;

public partial class LoginPage : ContentPage
{
	public LoginPage(LoginViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
    }
}