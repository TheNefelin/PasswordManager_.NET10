using Dapper;
using WebApiCore.Infrastructure.Data;
using WebApiCore.Infrastructure.Security;

namespace WebApiCore.Tests.Helpers;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected IDapperContext Context { get; } = TestDb.CreateContext();

    private readonly List<Guid> _createdUserIds = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var userId in _createdUserIds)
            await CleanupUserAsync(userId);
    }

    protected string NewEmail() => $"test_{Guid.NewGuid():N}@example.com";

    protected async Task<(Guid UserId, Guid SqlToken)> CreateUserDirectAsync(string email, string password = "Password123")
    {
        var userId = Guid.NewGuid();
        var sqlToken = Guid.NewGuid();
        var (hash, salt) = new PasswordHasher().HashPassword(password);

        using var connection = Context.CreateConnection();
        await connection.ExecuteAsync(
            "INSERT INTO Auth_Users (User_Id, Email, HashLogin, SaltLogin, SqlToken, Profile_Id) VALUES (@UserId, @Email, @HashLogin, @SaltLogin, @SqlToken, 2)",
            new { UserId = userId, Email = email, HashLogin = hash, SaltLogin = salt, SqlToken = sqlToken });

        _createdUserIds.Add(userId);
        return (userId, sqlToken);
    }

    protected void TrackCreatedUser(Guid userId) => _createdUserIds.Add(userId);

    private async Task CleanupUserAsync(Guid userId)
    {
        using var connection = Context.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM PM_CoreData WHERE User_Id = @userId", new { userId });
        await connection.ExecuteAsync("DELETE FROM Auth_Users WHERE User_Id = @userId", new { userId });
    }
}