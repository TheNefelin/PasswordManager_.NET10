namespace WebApiCore.Application.Common;

public sealed class CorePasswordNotConfiguredException : Exception
{
    public CorePasswordNotConfiguredException() : base("Debes crear una clave de encriptación.")
    {
    }
}
