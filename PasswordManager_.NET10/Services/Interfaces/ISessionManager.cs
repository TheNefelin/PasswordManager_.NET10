namespace PasswordManager_.NET10.Services.Interfaces;

public interface ISessionManager
{
    Task LoginAsync(string email, string password);
    Task Logout(bool hasExpired);
    void InitializeSession(int expireMinutes);
    void UpdateSessionTime();
    bool IsSessionExpired();
    TimeSpan GetRemainingTime();
    Task PerformFullLogoutAsync(string? message = null);
}