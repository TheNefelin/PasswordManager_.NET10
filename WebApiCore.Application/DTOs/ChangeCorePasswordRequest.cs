using System.ComponentModel.DataAnnotations;

namespace WebApiCore.Application.DTOs;

public class ChangeCorePasswordRequest
{
    // Mínimo 6 en la verificación de la clave actual, igual que en get-iv.
    // Nota: hay claves legacy creadas sin validación de longitud; si una clave
    // existente tuviera menos de 6 caracteres, este control la bloquearía y el
    // cambio de clave maestra no serviría como salida (fuente de fallo conocida,
    // documentada en DEVELOPMENT.md). Destino futuro: subir a 8.
    [MinLength(6)]
    [MaxLength(50)]
    public required string OldPassword { get; set; }

    [MinLength(8)]
    [MaxLength(50)]
    public required string NewPassword { get; set; }

    // Nueva sal/IV (base64 de 16 bytes) generada por el cliente. Es material
    // público del cifrado client-side, y su largo se valida en el servicio para
    // descartar valores inválidos o malformados.
    [Required]
    public required string Salt { get; set; }

    public required CoreUserRequest CoreUser { get; set; }

    public required List<CoreDataReplacement> Records { get; set; }
}