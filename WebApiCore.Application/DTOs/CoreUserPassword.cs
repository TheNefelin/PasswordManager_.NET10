using System.ComponentModel.DataAnnotations;

namespace WebApiCore.Application.DTOs;

public class CoreUserPassword
{
    // Mínimo 6 en la verificación (use). Nota: hay claves legacy creadas sin
    // validación de longitud; si una clave existente tuviera menos de 6
    // caracteres, esta validación la bloquearía (fuente de fallo conocida y
    // documentada en DEVELOPMENT.md). Destino futuro: subir a 8.
    [MinLength(6)]
    [MaxLength(50)]
    public required string Password { get; set; }
    public required CoreUserRequest CoreUser { get; set; }
}