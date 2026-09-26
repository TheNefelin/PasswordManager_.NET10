using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Services;
using WebApiCore.Infrastructure.Repositories;
using WebApiCore.Infrastructure.Security;
using WebApiCore.Tests.Helpers;

namespace WebApiCore.Tests.Core;

[Collection("Database")]
public class CoreUserServiceTests : IntegrationTestBase
{
    private static CoreUserService CreateService() => new(
        new CoreUserRepository(TestDb.CreateContext()),
        new PasswordHasher());

    [Fact]
    public async Task RegisterCoreUserPasswordAsync_ThenGetCoreUserIV_ReturnsSameIV()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        var registerResult = await service.RegisterCoreUserPasswordAsync(
            userId,
            new CoreUserPassword { Password = "SecretPM", CoreUser = coreUser },
            CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(registerResult.IV));

        var ivResult = await service.GetCoreUserIVAsync(
            userId,
            new CoreUserPassword { Password = "SecretPM", CoreUser = coreUser },
            CancellationToken.None);

        Assert.Equal(registerResult.IV, ivResult.IV);
    }

    [Fact]
    public async Task GetCoreUserIVAsync_WithInvalidSession_ThrowsUserSessionInvalidException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<UserSessionInvalidException>(() => service.GetCoreUserIVAsync(Guid.NewGuid(), new CoreUserPassword
        {
            Password = "SecretPM",
            CoreUser = new CoreUserRequest { User_Id = Guid.NewGuid(), SqlToken = Guid.NewGuid() }
        }, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterCoreUserPasswordAsync_Twice_ThrowsCorePasswordAlreadyExistsException()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        await service.RegisterCoreUserPasswordAsync(
            userId,
            new CoreUserPassword { Password = "SecretPM", CoreUser = coreUser },
            CancellationToken.None);

        await Assert.ThrowsAsync<CorePasswordAlreadyExistsException>(() => service.RegisterCoreUserPasswordAsync(
            userId,
            new CoreUserPassword { Password = "SecretPM", CoreUser = coreUser },
            CancellationToken.None));
    }

    [Fact]
    public async Task GetCoreUserIVAsync_WithWrongPassword_ThrowsInvalidCredentialsException()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        await service.RegisterCoreUserPasswordAsync(
            userId,
            new CoreUserPassword { Password = "CorrectPassword", CoreUser = coreUser },
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.GetCoreUserIVAsync(
            userId,
            new CoreUserPassword { Password = "WrongPassword", CoreUser = coreUser },
            CancellationToken.None));
    }
}