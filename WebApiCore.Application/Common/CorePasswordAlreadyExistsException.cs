namespace WebApiCore.Application.Common;

public sealed class CorePasswordAlreadyExistsException : Exception
{
    public CorePasswordAlreadyExistsException() : base("Ya tienes una clave de encriptación creada.")
    {
    }
}
