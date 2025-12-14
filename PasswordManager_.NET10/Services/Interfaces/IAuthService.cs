using PasswordManager_.NET10.Models;

namespace PasswordManager_.NET10.Services.Interfaces;

public interface IAuthService
{
    Task<User> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task<User?> GetCurrentUserAsync();
    Task<bool> IsAuthenticatedAsync();
}