namespace PasswordManager_.NET10.Services.Interfaces;

public interface ISessionManager
{
    Task LoginAsync(string email, string password);
    Task Logout(bool hasExpired);
    Task PerformFullLogoutAsync(string? message = null);
    Task<TimeSpan> GetRemainingTimeAsync();
    Task<bool> IsSessionExpiredAsync();
}