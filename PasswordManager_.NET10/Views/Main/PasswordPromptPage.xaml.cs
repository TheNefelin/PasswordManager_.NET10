namespace PasswordManager_.NET10.Views.Main;

public partial class PasswordPromptPage : ContentPage
{
    public TaskCompletionSource<string?>? CompletionSource { get; set; }

    public PasswordPromptPage()
	{
		InitializeComponent();
	}

    private async void OnAcceptClicked(object sender, EventArgs e)
    {
        string password = PasswordEntry.Text;
        await Navigation.PopModalAsync();
        CompletionSource?.SetResult(password);
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        CompletionSource?.SetResult(null);
    }
}