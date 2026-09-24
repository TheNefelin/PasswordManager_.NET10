using Microsoft.Extensions.Logging.Abstractions;
using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.Services.Implementation;
using PasswordManager_.NET10.Tests.Fakes;

namespace PasswordManager_.NET10.Tests.Services;

public class SessionManagerTests
{
    private readonly FakeAuthService _authService = new();
    private readonly FakeNavigationService _navigationService = new();
    private readonly SessionManager _sessionManager;

    public SessionManagerTests()
    {
        _sessionManager = new SessionManager(
            NullLogger<SessionManager>.Instance,
            _authService,
            _navigationService);
    }

    private static User CreateUser(DateTime tokenExpiry) => new()
    {
        UserId = Guid.NewGuid(),
        Email = "test@example.com",
        Role = "USER",
        ApiToken = "api-token",
        SqlToken = "sql-token",
        TokenExpiry = tokenExpiry,
        IsAuthenticated = true
    };

    [Fact]
    public async Task GetRemainingTimeAsync_WhenNoCurrentUser_ReturnsZero()
    {
        _authService.CurrentUser = null;

        var result = await _sessionManager.GetRemainingTimeAsync();

        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public async Task GetRemainingTimeAsync_WhenTokenInFuture_ReturnsPositiveTime()
    {
        _authService.CurrentUser = CreateUser(DateTime.UtcNow.AddMinutes(30));

        var result = await _sessionManager.GetRemainingTimeAsync();

        Assert.True(result > TimeSpan.Zero);
        Assert.True(result <= TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task GetRemainingTimeAsync_WhenTokenExpired_ReturnsZero()
    {
        _authService.CurrentUser = CreateUser(DateTime.UtcNow.AddMinutes(-30));

        var result = await _sessionManager.GetRemainingTimeAsync();

        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public async Task IsSessionExpiredAsync_WhenNoCurrentUser_ReturnsTrue()
    {
        _authService.CurrentUser = null;

        var result = await _sessionManager.IsSessionExpiredAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task IsSessionExpiredAsync_WhenTokenInFuture_ReturnsFalse()
    {
        _authService.CurrentUser = CreateUser(DateTime.UtcNow.AddMinutes(30));

        var result = await _sessionManager.IsSessionExpiredAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task IsSessionExpiredAsync_WhenTokenExpired_ReturnsTrue()
    {
        _authService.CurrentUser = CreateUser(DateTime.UtcNow.AddMinutes(-30));

        var result = await _sessionManager.IsSessionExpiredAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task LoginAsync_WhenFlagIsFalse_DoesNotSavePassword()
    {
        _authService.SavePasswordOnNextLogin = false;

        await _sessionManager.LoginAsync("test@example.com", "password");

        Assert.False(_authService.SavePasswordCalled);
        Assert.False(_authService.SetSavePasswordOnNextLoginCalled);
    }

    [Fact]
    public async Task LoginAsync_WhenFlagIsTrue_SavesPasswordAndClearsFlag()
    {
        _authService.SavePasswordOnNextLogin = true;

        await _sessionManager.LoginAsync("test@example.com", "password");

        Assert.True(_authService.SavePasswordCalled);
        Assert.True(_authService.SetSavePasswordOnNextLoginCalled);
        Assert.False(_authService.LastSetSavePasswordValue);
    }

    [Fact]
    public async Task LoginAsync_WhenSavePasswordThrows_DoesNotFailLogin()
    {
        _authService.SavePasswordOnNextLogin = true;
        _authService.SavePasswordThrows = true;

        await _sessionManager.LoginAsync("test@example.com", "password");
    }

    [Fact]
    public async Task Logout_WithoutExpiration_ClearsSessionAndNavigatesToLogin()
    {
        _authService.CurrentUser = CreateUser(DateTime.UtcNow.AddMinutes(30));

        await _sessionManager.Logout(false);

        Assert.True(_authService.LogoutCalled);
        Assert.Equal(1, _navigationService.GoToLoginCount);
        Assert.Null(_authService.CurrentUser);
    }

    [Fact]
    public async Task Logout_WhenExpired_ClearsSessionAndNavigatesToLogin()
    {
        _authService.CurrentUser = CreateUser(DateTime.UtcNow.AddMinutes(-30));

        await _sessionManager.Logout(true);

        Assert.True(_authService.LogoutCalled);
        Assert.Equal(1, _navigationService.GoToLoginCount);
    }

    [Fact]
    public async Task PerformFullLogoutAsync_WhenLogoutAsyncThrows_DoesNotPropagate()
    {
        _authService.CurrentUser = CreateUser(DateTime.UtcNow.AddMinutes(30));
        _authService.LogoutThrows = true;

        await _sessionManager.PerformFullLogoutAsync();

        Assert.False(_authService.LogoutCalled);
        Assert.Equal(0, _navigationService.GoToLoginCount);
    }
}