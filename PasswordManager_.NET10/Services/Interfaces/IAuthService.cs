using PasswordManager_.NET10.Models;

namespace PasswordManager_.NET10.Services.Interfaces;

public interface IAuthService
{
    Task<bool> RegisterAsync(string email, string password, string confirmPassword);
    Task<User> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task<User?> GetCurrentUserAsync();
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Adopta el SqlToken de la rotación del cambio de clave maestra,
    /// actualizando tanto la sesión en memoria como el almacenamiento seguro.
    /// </summary>
    Task UpdateSqlTokenAsync(string sqlToken);

    Task SetSavePasswordOnNextLoginAsync(bool value);
    Task<bool> GetSavePasswordOnNextLoginAsync();
    Task SavePasswordAsync(string encryptedPassword);
    Task<string?> GetSavedPasswordAsync();
    Task ClearSavedPasswordAsync();
    Task<bool> HasSavedPasswordAsync();
}