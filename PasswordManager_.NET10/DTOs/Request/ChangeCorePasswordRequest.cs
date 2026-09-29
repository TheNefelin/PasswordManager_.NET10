namespace PasswordManager_.NET10.DTOs.Request;

public class ChangeCorePasswordRequest
{
    public required string OldPassword { get; set; }

    public required string NewPassword { get; set; }

    /// <summary>
    /// Nueva sal/IV (base64 de 16 bytes) que el cliente genera para re-cifrar
    /// los datos con la clave nueva y que el servidor persiste como nueva SaltPM.
    /// </summary>
    public required string Salt { get; set; }

    public required CoreUserRequest CoreUser { get; set; }

    public required List<CoreDataReplacement> Records { get; set; }
}