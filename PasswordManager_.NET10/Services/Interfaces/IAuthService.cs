using PasswordManager_.NET10.Models;

namespace PasswordManager_.NET10.Services.Interfaces;

public interface IAuthService
{
    Task<bool> RegisterAsync(string email, string password, string confirmPassword);
    Task<User> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task<User?> GetCurrentUserAsync();
    Task<bool> IsAuthenticatedAsync();

    Task SetSavePasswordOnNextLoginAsync(bool value);
    Task<bool> GetSavePasswordOnNextLoginAsync();
    Task SavePasswordAsync(string encryptedPassword);
    Task<string?> GetSavedPasswordAsync();
    Task ClearSavedPasswordAsync();
    Task<bool> HasSavedPasswordAsync();
}