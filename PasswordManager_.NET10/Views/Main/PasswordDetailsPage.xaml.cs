using PasswordManager_.NET10.ViewModels;

namespace PasswordManager_.NET10.Views.Main;

public partial class PasswordDetailsPage : ContentPage
{
	public PasswordDetailsPage(PasswordDetailsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
    }
}