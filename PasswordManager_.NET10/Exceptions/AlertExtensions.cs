namespace PasswordManager_.NET10.Exceptions;

public static class AlertExtensions
{
    public static async Task ShowErrorAsync(this Exception ex, string title = "Error")
    {
        await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
            title,
            ex.Message,
            "OK"
        );
    }
}
