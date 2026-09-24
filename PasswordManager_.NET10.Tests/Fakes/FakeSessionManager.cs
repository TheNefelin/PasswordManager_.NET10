using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Tests.Fakes;

public class FakeSessionManager : ISessionManager
{
    public bool SessionExpired { get; set; }
    public TimeSpan RemainingTime { get; set; } = TimeSpan.FromMinutes(30);
    public bool LoginCalled { get; private set; }
    public bool LogoutCalled { get; private set; }
    public bool FullLogoutCalled { get; private set; }
    public string? FullLogoutMessage { get; private set; }

    public Task LoginAsync(string email, string password)
    {
        LoginCalled = true;
        return Task.CompletedTask;
    }

    public Task Logout(bool hasExpired)
    {
        LogoutCalled = true;
        return Task.CompletedTask;
    }

    public Task PerformFullLogoutAsync(string? message = null)
    {
        FullLogoutCalled = true;
        FullLogoutMessage = message;
        return Task.CompletedTask;
    }

    public Task<TimeSpan> GetRemainingTimeAsync() => Task.FromResult(RemainingTime);

    public Task<bool> IsSessionExpiredAsync() => Task.FromResult(SessionExpired);
}