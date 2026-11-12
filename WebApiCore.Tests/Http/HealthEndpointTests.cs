using System.Net;

namespace WebApiCore.Tests.Http;

/// <summary>
/// Health checks con liveness separado de readiness:
/// <list type="bullet">
/// <item><description><c>/health/live</c>: solo verifica que el proceso responde (no toca la BD).</description></item>
/// <item><description><c>/health/ready</c>: ejecuta el <c>SqlHealthCheck</c> (<c>SELECT 1</c> contra la BD).</description></item>
/// <item><description><c>/health</c>: alias de readiness, para no romper el contrato previo.</description></item>
/// </list>
/// Los tres son <c>AllowAnonymous</c>, por eso los tests van sin la ApiKey.
/// </summary>
[Collection("Database")]
public class HealthEndpointTests : ApiIntegrationTestBase
{
    public HealthEndpointTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task HealthLive_Returns200_WithoutApiKey()
    {
        var client = CreateClientWithoutApiKey();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HealthReady_Returns200_WhenDatabaseIsReachable()
    {
        var client = CreateClientWithoutApiKey();

        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HealthAlias_Returns200_AndKeepsThePreviousContract()
    {
        var client = CreateClientWithoutApiKey();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
