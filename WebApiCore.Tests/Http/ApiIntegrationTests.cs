using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace WebApiCore.Tests.Http;

public class ApiIntegrationTests : ApiIntegrationTestBase
{
    public ApiIntegrationTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Register_CreatesUser_Returns201()
    {
        var client = CreateClient();

        var response = await RegisterAsync(client, NewEmail());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var userId = await ParseUserIdAsync(response);
        Assert.NotEqual(Guid.Empty, userId);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndTokens()
    {
        var client = CreateClient();
        var email = NewEmail();
        await ParseUserIdAsync(await RegisterAsync(client, email));

        var response = await LoginAsync(client, email, "Password123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (userId, sqlToken, jwt) = await ParseLoginAsync(response);
        Assert.NotEqual(Guid.Empty, userId);
        Assert.NotEqual(Guid.Empty, sqlToken);
        Assert.False(string.IsNullOrEmpty(jwt));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = CreateClient();
        var email = NewEmail();
        await ParseUserIdAsync(await RegisterAsync(client, email));

        var response = await LoginAsync(client, email, "WrongPass");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithNonexistentUser_Returns401()
    {
        var client = CreateClient();

        var response = await LoginAsync(client, NewEmail(), "Password123");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Core_WithoutJwt_Returns401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/core", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Core_WithValidJwtAndSqlToken_Returns200()
    {
        var client = CreateClient();
        var email = NewEmail();
        await ParseUserIdAsync(await RegisterAsync(client, email));
        var (userId, sqlToken, jwt) = await ParseLoginAsync(await LoginAsync(client, email, "Password123"));
        TrackCreatedUser(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync($"/api/core?User_Id={userId}&SqlToken={sqlToken}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Core_WithJwtButWrongSqlToken_Returns401()
    {
        var client = CreateClient();
        var email = NewEmail();
        await ParseUserIdAsync(await RegisterAsync(client, email));
        var (userId, _, jwt) = await ParseLoginAsync(await LoginAsync(client, email, "Password123"));
        TrackCreatedUser(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync($"/api/core?User_Id={userId}&SqlToken={Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_FiveFailures_BlocksIp_Returns429()
    {
        var client = CreateClient();
        var email = NewEmail();
        await ParseUserIdAsync(await RegisterAsync(client, email));

        for (var i = 0; i < 5; i++)
        {
            var failed = await LoginAsync(client, email, "WrongPass");
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var blocked = await LoginAsync(client, email, "Password123");

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Login_OverRateLimit_Returns429()
    {
        var client = CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var response = await LoginAsync(client, NewEmail(), "WrongPass");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var throttled = await LoginAsync(client, NewEmail(), "WrongPass");

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    [Fact]
    public async Task Register_OverRateLimit_Returns429()
    {
        var client = CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var response = await RegisterAsync(client, NewEmail());
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            await ParseUserIdAsync(response);
        }

        var throttled = await RegisterAsync(client, NewEmail());

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    [Fact]
    public async Task MissingApiKey_Returns401()
    {
        var client = CreateClientWithoutApiKey();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = NewEmail(), password1 = "Password123", password2 = "Password123" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongApiKey_Returns401()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Remove("ApiKey");
        client.DefaultRequestHeaders.Add("ApiKey", "Wrong-Key");

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = NewEmail(), password = "Password123" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_Returns404_WithUniformEnvelope()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/does-not-exist", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(json.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(404, json.RootElement.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task SecurityHeaders_PresentOnApiResponse()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/core", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Contains("DENY", response.Headers.GetValues("X-Frame-Options"));
    }
}