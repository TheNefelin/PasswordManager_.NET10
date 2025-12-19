using PasswordManager_.NET10.ViewModels;

namespace PasswordManager_.NET10.Views.Authentication;

public partial class LoginPage : ContentPage
{
	public LoginPage(LoginViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Ejecutar validación de biometría cuando la página aparece
        if (BindingContext is LoginViewModel vm)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await vm.ValidateBiometricAsync();
            });
        }
    }
}