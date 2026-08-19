using Dapper;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Services;
using WebApiCore.Infrastructure.Repositories;
using WebApiCore.Infrastructure.Security;
using WebApiCore.Tests.Helpers;

namespace WebApiCore.Tests.Auth;

public class AuthUserServiceTests : IntegrationTestBase
{
    private const string TestIp = "127.0.0.1";

    private static AuthUserService CreateService() => new(
        new AuthUserRepository(TestDb.CreateContext()),
        new PasswordHasher(),
        new JwtTokenUtil(TestJwtOptions.Create()),
        new IpLockoutService(LoginLockoutOptions()));

    private static IpLockoutOptions LoginLockoutOptions() => new()
    {
        MaxFailures = 5,
        FailureWindow = TimeSpan.FromMinutes(15),
        BlockDuration = TimeSpan.FromMinutes(15)
    };

    [Fact]
    public async Task RegisterAsync_WithMatchingPasswords_ReturnsSuccess()
    {
        var service = CreateService();

        var result = await service.RegisterAsync(new AuthUserRegister
        {
            Email = NewEmail(),
            Password1 = "Password123",
            Password2 = "Password123"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.StatusCode);
        Assert.NotEqual(Guid.Empty, result.Data!.User_Id);
        TrackCreatedUser(result.Data.User_Id);
    }

    [Fact]
    public async Task RegisterAsync_WithMismatchedPasswords_ReturnsBadRequest()
    {
        var service = CreateService();

        var result = await service.RegisterAsync(new AuthUserRegister
        {
            Email = NewEmail(),
            Password1 = "Password123",
            Password2 = "Password456"
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsBadRequest()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);
        var service = CreateService();

        var result = await service.RegisterAsync(new AuthUserRegister
        {
            Email = email,
            Password1 = "Password123",
            Password2 = "Password123"
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokenAndSqlToken()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);
        var service = CreateService();

        var result = await service.LoginAsync(new AuthUserLogin
        {
            Email = email,
            Password = "Password123"
        }, TestIp, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.False(string.IsNullOrEmpty(result.Data!.ApiToken));
        Assert.NotEqual(Guid.Empty, result.Data.SqlToken);
        Assert.Equal("USER", result.Data.Role);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsUnauthorized()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);
        var service = CreateService();

        var result = await service.LoginAsync(new AuthUserLogin
        {
            Email = email,
            Password = "WrongPassword"
        }, TestIp, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_WithNonexistentUser_ReturnsUnauthorized()
    {
        var service = CreateService();

        var result = await service.LoginAsync(new AuthUserLogin
        {
            Email = NewEmail(),
            Password = "Password123"
        }, TestIp, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task RegisterAsync_WhenRegistrationDisabled_ReturnsForbidden()
    {
        var service = CreateService();

        using var connection = Context.CreateConnection();
        await connection.ExecuteAsync("UPDATE Mae_Config SET IsEnableRegister = 0 WHERE Config_Id = 1");

        try
        {
            var result = await service.RegisterAsync(new AuthUserRegister
            {
                Email = NewEmail(),
                Password1 = "Password123",
                Password2 = "Password123"
            }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(403, result.StatusCode);
        }
        finally
        {
            await connection.ExecuteAsync("UPDATE Mae_Config SET IsEnableRegister = 1 WHERE Config_Id = 1");
        }
    }

    [Fact]
    public async Task LoginAsync_ReachingFailureLimit_BlocksTheIp()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);
        var service = CreateService();

        for (var i = 0; i < 5; i++)
        {
            var failed = await service.LoginAsync(new AuthUserLogin
            {
                Email = email,
                Password = "WrongPassword"
            }, TestIp, CancellationToken.None);

            Assert.Equal(401, failed.StatusCode);
        }

        var blocked = await service.LoginAsync(new AuthUserLogin
        {
            Email = email,
            Password = "Password123"
        }, TestIp, CancellationToken.None);

        Assert.False(blocked.IsSuccess);
        Assert.Equal(429, blocked.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_WhenIpBlocked_ReturnsTooManyRequests()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);
        var service = CreateService();

        for (var i = 0; i < 5; i++)
        {
            await service.LoginAsync(new AuthUserLogin
            {
                Email = email,
                Password = "WrongPassword"
            }, TestIp, CancellationToken.None);
        }

        var result = await service.LoginAsync(new AuthUserLogin
        {
            Email = email,
            Password = "Password123"
        }, TestIp, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(429, result.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_Success_ResetsFailureCountForTheIp()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);
        var service = CreateService();

        for (var i = 0; i < 4; i++)
        {
            await service.LoginAsync(new AuthUserLogin
            {
                Email = email,
                Password = "WrongPassword"
            }, TestIp, CancellationToken.None);
        }

        var success = await service.LoginAsync(new AuthUserLogin
        {
            Email = email,
            Password = "Password123"
        }, TestIp, CancellationToken.None);
        Assert.True(success.IsSuccess);

        var afterReset = await service.LoginAsync(new AuthUserLogin
        {
            Email = email,
            Password = "WrongPassword"
        }, TestIp, CancellationToken.None);
        Assert.Equal(401, afterReset.StatusCode);
    }
}