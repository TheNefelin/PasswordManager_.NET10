using PasswordManager_.NET10.ViewModels;

namespace PasswordManager_.NET10.Views.Components;

public partial class TestingPage : ContentPage
{
	public TestingPage(TestingViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
    }
}