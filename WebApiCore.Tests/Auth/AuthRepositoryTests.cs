using WebApiCore.Domain.Entities;
using WebApiCore.Infrastructure.Repositories;
using WebApiCore.Tests.Helpers;

namespace WebApiCore.Tests.Auth;

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

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
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

        var user = await repository.GetUserByEmailAsync(email, CancellationToken.None);
        Assert.Equal(token, user!.SqlToken);
    }
}