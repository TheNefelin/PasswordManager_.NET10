using WebApiCore.Application.DTOs;
using WebApiCore.Application.Services;
using WebApiCore.Infrastructure.Repositories;
using WebApiCore.Infrastructure.Security;
using WebApiCore.Tests.Helpers;

namespace WebApiCore.Tests.Core;

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
            new CoreUserPassword { Password = "SecretPM", CoreUser = coreUser },
            CancellationToken.None);

        Assert.True(registerResult.IsSuccess);
        Assert.Equal(200, registerResult.StatusCode);

        var ivResult = await service.GetCoreUserIVAsync(
            new CoreUserPassword { Password = "SecretPM", CoreUser = coreUser },
            CancellationToken.None);

        Assert.True(ivResult.IsSuccess);
        Assert.Equal(registerResult.Data!.IV, ivResult.Data!.IV);
    }

    [Fact]
    public async Task GetCoreUserIVAsync_WithInvalidSession_ReturnsUnauthorized()
    {
        var service = CreateService();

        var result = await service.GetCoreUserIVAsync(new CoreUserPassword
        {
            Password = "SecretPM",
            CoreUser = new CoreUserRequest { User_Id = Guid.NewGuid(), SqlToken = Guid.NewGuid() }
        }, CancellationToken.None);

        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task RegisterCoreUserPasswordAsync_Twice_ReturnsBadRequest()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        var first = await service.RegisterCoreUserPasswordAsync(
            new CoreUserPassword { Password = "SecretPM", CoreUser = coreUser },
            CancellationToken.None);

        Assert.True(first.IsSuccess);

        var second = await service.RegisterCoreUserPasswordAsync(
            new CoreUserPassword { Password = "SecretPM", CoreUser = coreUser },
            CancellationToken.None);

        Assert.False(second.IsSuccess);
        Assert.Equal(400, second.StatusCode);
    }
}