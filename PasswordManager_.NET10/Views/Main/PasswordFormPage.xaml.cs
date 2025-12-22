using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.ViewModels;

namespace PasswordManager_.NET10.Views.Main;

public partial class PasswordFormPage : ContentPage
{
    public PasswordFormPage(PasswordFormViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
    }
}