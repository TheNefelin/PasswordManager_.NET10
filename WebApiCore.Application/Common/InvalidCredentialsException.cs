namespace WebApiCore.Application.Common;

public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Usuario o contraseña incorrecta.")
    {
    }
}
