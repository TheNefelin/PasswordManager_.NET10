using Dapper;
using WebApiCore.Domain.Entities;
using WebApiCore.Domain.Models;
using WebApiCore.Infrastructure.Repositories;
using WebApiCore.Infrastructure.Security;
using WebApiCore.Tests.Helpers;

namespace WebApiCore.Tests.Auth;

[Collection("Database")]
public class AuthRepositoryTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateUserAsync_WithValidData_ReturnsSuccess()
    {
        var repository = new AuthUserRepository(Context);
        var user = new AuthUser
        {
            User_Id = Guid.NewGuid(),
            Email = NewEmail(),
            HashLogin = "hash",
            SaltLogin = "salt"
        };

        var result = await repository.CreateUserAsync(user, CancellationToken.None);

        Assert.Equal(UserCreationStatus.Created, result);
        TrackCreatedUser(user.User_Id);
    }

    [Fact]
    public async Task GetUserByEmailAsync_ReturnsUser()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);

        var repository = new AuthUserRepository(Context);
        var user = await repository.GetUserByEmailAsync(email, CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal("USER", user.Role);
    }

    [Fact]
    public async Task NewSqlToken_UpdatesToken()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);

        var repository = new AuthUserRepository(Context);
        var token = await repository.NewSqlToken(email, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, token);

        // El token crudo no se persiste: en la base solo queda su SHA-256.
        var user = await repository.GetUserByEmailAsync(email, CancellationToken.None);
        Assert.Equal(SqlTokenHasher.Hash(token), user!.SqlTokenHash);
    }

    [Fact]
    public async Task NewSqlToken_DoesNotStoreTheRawToken()
    {
        var email = NewEmail();
        await CreateUserDirectAsync(email);

        var repository = new AuthUserRepository(Context);
        var token = await repository.NewSqlToken(email, CancellationToken.None);

        var storedHash = await ReadStoredHashAsync(email);
        Assert.NotNull(storedHash);
        Assert.NotEqual(token.ToString(), storedHash);
        Assert.Equal(64, storedHash.Length);
    }

    private async Task<string?> ReadStoredHashAsync(string email)
    {
        using var connection = Context.CreateConnection();

        return await connection.QuerySingleAsync<string?>(
            "SELECT SqlTokenHash FROM Auth_Users WHERE Email = @Email",
            new { Email = email });
    }
}