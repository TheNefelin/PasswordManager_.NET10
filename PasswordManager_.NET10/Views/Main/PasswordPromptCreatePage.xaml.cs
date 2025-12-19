using PasswordManager_.NET10.ViewModels;

namespace PasswordManager_.NET10.Views.Main;

public partial class PasswordPromptCreatePage : ContentPage
{
	public PasswordPromptCreatePage(PasswordPromptCreateViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
    }
}