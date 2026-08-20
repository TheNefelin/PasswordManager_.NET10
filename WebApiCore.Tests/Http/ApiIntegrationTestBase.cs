using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using WebApiCore.Infrastructure.Data;
using WebApiCore.Tests.Helpers;

namespace WebApiCore.Tests.Http;

[Collection("Database")]
public abstract class ApiIntegrationTestBase : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly List<Guid> _createdUserIds = new();

    protected ApiFactory Factory { get; }

    protected string TestIp { get; } = $"192.0.2.{Random.Shared.Next(1, 255)}";

    protected ApiIntegrationTestBase(ApiFactory factory) => Factory = factory;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        using var connection = TestDb.CreateContext().CreateConnection();
        foreach (var userId in _createdUserIds)
        {
            await connection.ExecuteAsync("DELETE FROM PM_CoreData WHERE User_Id = @userId", new { userId });
            await connection.ExecuteAsync("DELETE FROM Auth_Users WHERE User_Id = @userId", new { userId });
        }
    }

    protected HttpClient CreateClient() => Factory.CreateClientWithApiKey(TestIp);

    protected HttpClient CreateClientWithoutApiKey() => Factory.CreateClientWithoutApiKey(TestIp);

    protected void TrackCreatedUser(Guid userId) => _createdUserIds.Add(userId);

    protected static string NewEmail() => $"http_{Guid.NewGuid():N}@example.com";

    protected async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email)
        => await client.PostAsJsonAsync("/api/auth/register", new { email, password1 = "Password123", password2 = "Password123" }, TestContext.Current.CancellationToken);

    protected async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
        => await client.PostAsJsonAsync("/api/auth/login", new { email, password }, TestContext.Current.CancellationToken);

    protected async Task<Guid> ParseUserIdAsync(HttpResponseMessage response)
    {
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var userId = Guid.Parse(json.RootElement.GetProperty("data").GetProperty("user_Id").GetString()!);
        TrackCreatedUser(userId);
        return userId;
    }

    protected static async Task<(Guid UserId, Guid SqlToken, string Jwt)> ParseLoginAsync(HttpResponseMessage response)
    {
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        return (
            Guid.Parse(data.GetProperty("user_Id").GetString()!),
            Guid.Parse(data.GetProperty("sqlToken").GetString()!),
            data.GetProperty("apiToken").GetString()!);
    }
}