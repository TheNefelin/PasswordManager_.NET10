using PasswordManager_.NET10.DTOs.Response;
using PasswordManager_.NET10.Models;

namespace PasswordManager_.NET10.Services.Interfaces;

public interface ICoreDataService
{
    Task<CoreUserIV> RegisterCorePasswordAsync(string password);
    Task<CoreUserIV> GetCoreUserIVAsync(string password);
    Task<IEnumerable<CoreSecretData>> GetAllCoreDataAsync();
    Task<CoreSecretData> CreateCoreDataAsync(CoreSecretData coreSecretData);
    Task<CoreSecretData> UpdateCoreDataAsync(CoreSecretData coreSecretData);
    Task DeleteCoreDataAsync(Guid dataId);

    /// <summary>
    /// Cambia la clave maestra de forma atómica: reemplaza HashPM, SaltPM y SqlToken
    /// y sustituye todos los datos por los registros re-cifrados provistos.
    /// </summary>
    /// <param name="oldPassword">Clave maestra actual.</param>
    /// <param name="newPassword">Nueva clave maestra (mínimo 8 caracteres).</param>
    /// <param name="newIv">Nueva sal/IV (base64 de 16 bytes) generada por el cliente.</param>
    /// <param name="reEncryptedRecords">Registros re-cifrados con (nueva clave, newIv).</param>
    Task<CoreUserChangeResponse> ChangeCorePasswordAsync(
        string oldPassword,
        string newPassword,
        string newIv,
        IEnumerable<CoreSecretData> reEncryptedRecords);
}
