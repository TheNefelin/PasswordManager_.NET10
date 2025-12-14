using PasswordManager_.NET10.Models;

namespace PasswordManager_.NET10.ViewModels;

// ==================== INTERFAZ ====================
// IEncryptionService.cs
public interface IEncryptionService
{
    /// <summary>
    /// Encripta un objeto CoreSecretData
    /// </summary>
    CoreSecretData EncryptData(CoreSecretData coreSecretData, string password, string iv);

    /// <summary>
    /// Desencripta un objeto CoreSecretData
    /// </summary>
    CoreSecretData DecryptData(CoreSecretData coreSecretData, string password, string iv);

    /// <summary>
    /// Encripta una colección completa de CoreSecretData
    /// </summary>
    IEnumerable<CoreSecretData> EncryptDataCollection(IEnumerable<CoreSecretData> collection, string password, string iv);

    /// <summary>
    /// Desencripta una colección completa de CoreSecretData
    /// </summary>
    IEnumerable<CoreSecretData> DecryptDataCollection(IEnumerable<CoreSecretData> collection, string password, string iv);

    /// <summary>
    /// Verifica si un texto está encriptado (Base64)
    /// </summary>
    bool IsEncrypted(string text);
}
