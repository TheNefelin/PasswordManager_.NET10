using PasswordManager_.NET10.ViewModels;

namespace PasswordManager_.NET10.Views.Main;

public partial class HelpPage : ContentPage
{
	public HelpPage(HelpViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
    }
}