namespace WebApiCore.Application.Common;

public sealed class RegistrationDisabledException : Exception
{
    public RegistrationDisabledException() : base("El registro de usuarios está deshabilitado.")
    {
    }
}
