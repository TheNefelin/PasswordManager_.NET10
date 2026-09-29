namespace WebApiCore.Application.DTOs;

public class CoreUserChangeResponse
{
    // La nueva sal es el nuevo IV: la clave de cifrado del cliente deriva de
    // la nueva contraseña maestra + esta sal. El SqlToken rotado sustituye al
    // anterior: las demás sesiones quedan invalidadas.
    public required string IV { get; set; }
    public required Guid SqlToken { get; set; }
}