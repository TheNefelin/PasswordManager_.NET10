using WebApiCore.Domain.Entities;
using WebApiCore.Infrastructure.Repositories;
using WebApiCore.Infrastructure.Security;
using WebApiCore.Tests.Helpers;

namespace WebApiCore.Tests.Core;

[Collection("Database")]
public class CoreUserRepositoryTests : IntegrationTestBase
{
    [Fact]
    public async Task GetCoreUserAsync_WithInvalidSession_ReturnsNull()
    {
        var repository = new CoreUserRepository(Context);

        var result = await repository.GetCoreUserAsync(
            new CoreUser { User_Id = Guid.NewGuid(), SqlToken = Guid.NewGuid() },
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCoreUserAsync_WithValidSession_ReturnsUser()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var repository = new CoreUserRepository(Context);

        var result = await repository.GetCoreUserAsync(
            new CoreUser { User_Id = userId, SqlToken = sqlToken },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(userId, result.User_Id);
    }

    [Fact]
    public async Task RegisterCoreUserPasswordAsync_UpdatesHashAndSalt()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var repository = new CoreUserRepository(Context);
        var (hash, salt) = new PasswordHasher().HashPassword("SecretPM");

        await repository.RegisterCoreUserPasswordAsync(
            new CoreUser { User_Id = userId, SqlToken = sqlToken, HashPM = hash, SaltPM = salt },
            CancellationToken.None);

        var updated = await repository.GetCoreUserAsync(
            new CoreUser { User_Id = userId, SqlToken = sqlToken },
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(hash, updated.HashPM);
        Assert.Equal(salt, updated.SaltPM);
    }
}