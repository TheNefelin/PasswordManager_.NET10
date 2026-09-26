using Dapper;
using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Services;
using WebApiCore.Infrastructure.Repositories;
using WebApiCore.Infrastructure.Security;
using WebApiCore.Tests.Helpers;

namespace WebApiCore.Tests.Auth;

[Collection("Database")]
public class AuthUserServiceTests : IntegrationTestBase
{
    private const string TestIp = "127.0.0.1";

    private static AuthUserService CreateService() => new(
        new AuthUserRepository(TestDb.CreateContext()),
        new MaeConfigRepository(TestDb.CreateContext()),
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

        Assert.NotEqual(Guid.Empty, result.User_Id);
        TrackCreatedUser(result.User_Id);
    }

    [Fact]
    public async Task RegisterAsync_WithMismatchedPasswords_ThrowsRequestValidationException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<RequestValidationException>(() => service.RegisterAsync(new AuthUserRegister
        {
            Email = NewEmail(),
            Password1 = "Password123",
            Password2 = "Password456"
        }, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsDuplicateEmailException()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);
        var service = CreateService();

        await Assert.ThrowsAsync<DuplicateEmailException>(() => service.RegisterAsync(new AuthUserRegister
        {
            Email = email,
            Password1 = "Password123",
            Password2 = "Password123"
        }, CancellationToken.None));
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

        Assert.False(string.IsNullOrEmpty(result.ApiToken));
        Assert.NotEqual(Guid.Empty, result.SqlToken);
        Assert.Equal("USER", result.Role);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ThrowsInvalidCredentialsException()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync(new AuthUserLogin
        {
            Email = email,
            Password = "WrongPassword"
        }, TestIp, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_WithNonexistentUser_ThrowsInvalidCredentialsException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync(new AuthUserLogin
        {
            Email = NewEmail(),
            Password = "Password123"
        }, TestIp, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterAsync_WhenRegistrationDisabled_ThrowsRegistrationDisabledException()
    {
        var service = CreateService();

        using var connection = Context.CreateConnection();
        await connection.ExecuteAsync("UPDATE Mae_Config SET IsEnableRegister = 0 WHERE Config_Id = 1");

        try
        {
            await Assert.ThrowsAsync<RegistrationDisabledException>(() => service.RegisterAsync(new AuthUserRegister
            {
                Email = NewEmail(),
                Password1 = "Password123",
                Password2 = "Password123"
            }, CancellationToken.None));
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
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync(new AuthUserLogin
            {
                Email = email,
                Password = "WrongPassword"
            }, TestIp, CancellationToken.None));
        }

        await Assert.ThrowsAsync<TooManyLoginAttemptsException>(() => service.LoginAsync(new AuthUserLogin
        {
            Email = email,
            Password = "Password123"
        }, TestIp, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_WhenIpBlocked_ReturnsTooManyRequests()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);
        var service = CreateService();

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync(new AuthUserLogin
            {
                Email = email,
                Password = "WrongPassword"
            }, TestIp, CancellationToken.None));
        }

        await Assert.ThrowsAsync<TooManyLoginAttemptsException>(() => service.LoginAsync(new AuthUserLogin
        {
            Email = email,
            Password = "Password123"
        }, TestIp, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_Success_ResetsFailureCountForTheIp()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);
        var service = CreateService();

        for (var i = 0; i < 4; i++)
        {
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync(new AuthUserLogin
            {
                Email = email,
                Password = "WrongPassword"
            }, TestIp, CancellationToken.None));
        }

        var success = await service.LoginAsync(new AuthUserLogin
        {
            Email = email,
            Password = "Password123"
        }, TestIp, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(success.ApiToken));

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync(new AuthUserLogin
        {
            Email = email,
            Password = "WrongPassword"
        }, TestIp, CancellationToken.None));
    }
}