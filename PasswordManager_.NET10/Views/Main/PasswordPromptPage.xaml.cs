namespace PasswordManager_.NET10.Views.Main;

public partial class PasswordPromptPage : ContentPage
{
    public TaskCompletionSource<string?>? CompletionSource { get; set; }

    // Variable para controlar el estado actual
    private bool _isPasswordVisible = false;

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

    private void ToggleIsPassword_Clicked(object sender, EventArgs e)
    {
        // Alternar entre mostrar/ocultar contraseña
        _isPasswordVisible = !_isPasswordVisible;
        PasswordEntry.IsPassword = !_isPasswordVisible;

        //// Cambiar el texto/icono del botón según el estado
        //var button = (Button)sender;

        //if (_isPasswordVisible)
        //{
        //    button.Text = "Ocultar";
        //    // Si tienes un icono diferente para cuando está visible
        //    // button.ImageSource = "icon_eye_lock_closed_512.png";
        //}
        //else
        //{
        //    button.Text = "Ver";
        //    // Volver al icono original
        //    // button.ImageSource = "icon_eye_lock_open_512.png";
        //}
    }
}