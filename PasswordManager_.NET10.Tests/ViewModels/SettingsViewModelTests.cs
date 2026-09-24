using Microsoft.Extensions.Logging.Abstractions;
using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.Tests.Fakes;
using PasswordManager_.NET10.ViewModels;

namespace PasswordManager_.NET10.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly FakeAuthService _authService = new();
    private readonly FakeThemeService _themeService = new();
    private readonly FakeBiometricService _biometricService = new();
    private readonly FakeSessionManager _sessionManager = new();
    private readonly FakeNavigationService _navigationService = new();
    private readonly FakeDialogService _dialogService = new();

    private SettingsViewModel CreateViewModel() => new(
        NullLogger<SettingsViewModel>.Instance,
        _authService,
        _themeService,
        _biometricService,
        _sessionManager,
        _navigationService,
        _dialogService);

    private static User CreateExpiredUser() => new()
    {
        UserId = Guid.NewGuid(),
        Email = "test@example.com",
        Role = "USER",
        ApiToken = "api-token",
        SqlToken = "sql-token",
        TokenExpiry = DateTime.UtcNow.AddMinutes(-30),
        IsAuthenticated = true
    };

    [Fact]
    public async Task LoadSessionDataAsync_WhenSessionExpired_SetsExpiredState()
    {
        var user = CreateExpiredUser();
        _authService.CurrentUser = user;
        _sessionManager.SessionExpired = true;

        var vm = CreateViewModel();
        await vm.LoadSessionDataAsync();

        Assert.Equal(user.UserId.ToString(), vm.UserId);
        Assert.Equal("USER", vm.Role);
        Assert.Equal(user.SqlToken, vm.SqlToken);
        Assert.Equal(user.ApiToken, vm.ApiToken);
        Assert.True(vm.IsSessionExpired);
        Assert.Equal("Sesión expirada", vm.SessionTimeRemaining);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadSessionDataAsync_WhenNoCurrentUser_DoesNotPopulateAndDoesNotThrow()
    {
        _authService.CurrentUser = null;

        var vm = CreateViewModel();
        await vm.LoadSessionDataAsync();

        Assert.Equal(string.Empty, vm.UserId);
        Assert.Equal(string.Empty, vm.SqlToken);
        Assert.False(vm.IsSessionExpired);
        Assert.Equal(string.Empty, vm.SessionTimeRemaining);
        Assert.False(vm.IsLoading);
    }
}