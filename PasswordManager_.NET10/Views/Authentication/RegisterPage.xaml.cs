using PasswordManager_.NET10.ViewModels;

namespace PasswordManager_.NET10.Views.Authentication;

public partial class RegisterPage : ContentPage
{
	public RegisterPage(RegisterViewModel viewModel)
	{
		InitializeComponent();	
		BindingContext = viewModel;
    }
}