namespace WebApiCore.Application.Common;

public sealed class TooManyLoginAttemptsException : Exception
{
    public TooManyLoginAttemptsException() : base("Demasiados intentos fallidos de inicio de sesión. Intenta nuevamente más tarde.")
    {
    }
}
