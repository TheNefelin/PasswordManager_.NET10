namespace WebApiCore.Application.Common;

public sealed class DuplicateEmailException : Exception
{
    public DuplicateEmailException() : base("El correo ya está registrado.")
    {
    }
}
