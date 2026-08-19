using WebApiCore.Application.DTOs;
using WebApiCore.Application.Services;
using WebApiCore.Infrastructure.Repositories;
using WebApiCore.Infrastructure.Security;
using WebApiCore.Tests.Helpers;

namespace WebApiCore.Tests.Auth;

public class AuthUserServiceTests : IntegrationTestBase
{
    private static AuthUserService CreateService() => new(
        new AuthUserRepository(TestDb.CreateContext()),
        new PasswordHasher(),
        new JwtTokenUtil(TestJwtOptions.Create()));

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
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.False(string.IsNullOrEmpty(result.Data!.ApiToken));
        Assert.NotEqual(Guid.Empty, result.Data.SqlToken);
        Assert.Equal("USER", result.Data.Role);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsBadRequest()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);
        var service = CreateService();

        var result = await service.LoginAsync(new AuthUserLogin
        {
            Email = email,
            Password = "WrongPassword"
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }
}