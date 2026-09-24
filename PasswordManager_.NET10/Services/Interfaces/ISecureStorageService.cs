using PasswordManager_.NET10.Models;

namespace PasswordManager_.NET10.Services.Interfaces;

public interface ISecureStorageService
{
    Task SetSessionAsync(SessionData sessionData);
    Task ClearSessionAsync();
    Task<string?> GetUserIdAsync();
    Task<string?> GetEmailAsync();
    Task<string?> GetSqlTokenAsync();
    Task<string?> GetRoleAsync();
    Task<string?> GetApiTokenAsync();
    Task<int> GetExpireMinAsync();
    Task<DateTime?> GetExpirationTimeAsync();
    Task<bool> IsAuthenticatedAsync();
    Task SetBiometricsEnabledAsync(bool isEnabled);
    Task<bool> IsBiometricsEnabledAsync();

    Task SetSavedPasswordAsync(string encryptedPassword);
    Task<string?> GetSavedPasswordAsync();
    Task ClearSavedPasswordAsync();
    Task<bool> HasSavedPasswordAsync();

    Task SetSavePasswordOnNextLoginAsync(bool value);
    Task<bool> GetSavePasswordOnNextLoginAsync();
    Task ClearSavePasswordOnNextLoginAsync();
}