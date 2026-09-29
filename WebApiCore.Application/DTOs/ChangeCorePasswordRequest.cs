using System.ComponentModel.DataAnnotations;

namespace WebApiCore.Application.DTOs;

public class ChangeCorePasswordRequest
{
    // Sin MinLength a propósito: las claves maestras existentes pueden tener
    // menos de 8 caracteres (se validó de más en el pasado), y la verificación
    // de la vieja debe aceptarlas igual. El Mínimo de 8 rige solo para la nueva.
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