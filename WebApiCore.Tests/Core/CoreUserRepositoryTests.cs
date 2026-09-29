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

    [Fact]
    public async Task ChangeCorePasswordAsync_UpdatesHashSaltRotatesTokenAndReplacesData()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var coreUserRepository = new CoreUserRepository(Context);
        var dataRepository = new CoreDataRepository(Context);
        var (hash, salt) = new PasswordHasher().HashPassword("SecretPM");

        await coreUserRepository.RegisterCoreUserPasswordAsync(
            new CoreUser { User_Id = userId, SqlToken = sqlToken, HashPM = hash, SaltPM = salt },
            CancellationToken.None);

        var original = await dataRepository.InsertAsync(
            new CoreData { Data01 = "a", Data02 = "b", Data03 = "c", User_Id = userId },
            CancellationToken.None);

        var (newHash, newSalt) = new PasswordHasher().HashPassword("NewSecretPM");
        var newToken = Guid.NewGuid();
        var replacement = new CoreData { Data_Id = Guid.NewGuid(), Data01 = "x", Data02 = "y", Data03 = "z", User_Id = userId };

        await coreUserRepository.ChangeCorePasswordAsync(userId, newHash, newSalt, newToken, new[] { replacement }, CancellationToken.None);

        var withNewToken = await coreUserRepository.GetCoreUserAsync(
            new CoreUser { User_Id = userId, SqlToken = newToken },
            CancellationToken.None);

        Assert.NotNull(withNewToken);
        Assert.Equal(newHash, withNewToken.HashPM);
        Assert.Equal(newSalt, withNewToken.SaltPM);

        var withOldToken = await coreUserRepository.GetCoreUserAsync(
            new CoreUser { User_Id = userId, SqlToken = sqlToken },
            CancellationToken.None);

        Assert.Null(withOldToken);

        var all = (await dataRepository.GetAllAsync(new CoreData { User_Id = userId }, CancellationToken.None)).ToList();
        Assert.Single(all);
        Assert.DoesNotContain(all, x => x.Data_Id == original.Data_Id);
    }
}