namespace PasswordManager_.NET10.Messages;

public class RegistrationCompletedMessage
{
    public string Email { get; }

    public RegistrationCompletedMessage(string email)
    {
        Email = email;
    }
}
