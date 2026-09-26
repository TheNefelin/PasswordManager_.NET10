namespace WebApiCore.Application.Common;

public sealed class UserSessionInvalidException : Exception
{
    public UserSessionInvalidException() : base("Debes iniciar sesión.")
    {
    }
}
