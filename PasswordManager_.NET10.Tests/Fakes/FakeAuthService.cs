using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Tests.Fakes;

public class FakeAuthService : IAuthService
{
    public User? CurrentUser { get; set; }
    public bool SavePasswordOnNextLogin { get; set; }
    public string? SavedPassword { get; set; }
    public bool SavePasswordThrows { get; set; }
    public bool LogoutThrows { get; set; }

    public bool RegisterCalled { get; private set; }
    public bool LogoutCalled { get; private set; }
    public bool SavePasswordCalled { get; private set; }
    public bool SetSavePasswordOnNextLoginCalled { get; private set; }
    public bool ClearSavedPasswordCalled { get; private set; }
    public bool? LastSetSavePasswordValue { get; private set; }

    public Task<bool> RegisterAsync(string email, string password, string confirmPassword)
    {
        RegisterCalled = true;
        return Task.FromResult(true);
    }

    public Task<User> LoginAsync(string email, string password)
    {
        return Task.FromResult(new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            Role = "USER",
            ApiToken = "api-token",
            SqlToken = "sql-token",
            TokenExpiry = DateTime.UtcNow.AddMinutes(30),
            IsAuthenticated = true
        });
    }

    public Task LogoutAsync()
    {
        if (LogoutThrows)
        {
            throw new InvalidOperationException("Logout failed");
        }

        LogoutCalled = true;
        CurrentUser = null;
        return Task.CompletedTask;
    }

    public Task<User?> GetCurrentUserAsync() => Task.FromResult(CurrentUser);

    public Task<bool> IsAuthenticatedAsync() => Task.FromResult(CurrentUser != null);

    public Task SetSavePasswordOnNextLoginAsync(bool value)
    {
        SetSavePasswordOnNextLoginCalled = true;
        LastSetSavePasswordValue = value;
        SavePasswordOnNextLogin = value;
        return Task.CompletedTask;
    }

    public Task<bool> GetSavePasswordOnNextLoginAsync() => Task.FromResult(SavePasswordOnNextLogin);

    public Task SavePasswordAsync(string encryptedPassword)
    {
        if (SavePasswordThrows)
        {
            throw new InvalidOperationException("Cannot save password");
        }

        SavePasswordCalled = true;
        SavedPassword = encryptedPassword;
        return Task.CompletedTask;
    }

    public Task<string?> GetSavedPasswordAsync() => Task.FromResult(SavedPassword);

    public Task ClearSavedPasswordAsync()
    {
        ClearSavedPasswordCalled = true;
        SavedPassword = null;
        return Task.CompletedTask;
    }

    public Task<bool> HasSavedPasswordAsync() => Task.FromResult(SavedPassword != null);
}