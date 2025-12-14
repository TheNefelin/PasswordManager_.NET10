using PasswordManager_.NET10.Models;

namespace PasswordManager_.NET10.Services.Interfaces;

public interface ICoreDataService
{
    Task<CoreUserIV> RegisterCorePasswordAsync(string password);
    Task<CoreUserIV> GetCoreUserIVAsync(string password);
    Task<IEnumerable<CoreSecretData>> GetAllCoreDataAsync();
    Task<CoreSecretData> CreateCoreDataAsync(CoreSecretData coreSecretData);
    Task<CoreSecretData> UpdateCoreDataAsync(CoreSecretData coreSecretData);
    Task<string> DeleteCoreDataAsync(Guid dataId);
}
