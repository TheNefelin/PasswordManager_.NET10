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
    public async Task Register_WithPasswordShorterThanPolicy_Returns400()
    {
        var client = CreateClient();

        // 5 caracteres: por debajo del mínimo de 6 de la política de registro.
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = NewEmail(), password1 = "Pass1", password2 = "Pass1" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.NotEmpty(problem.RootElement.GetProperty("errors").EnumerateObject());
    }

    [Fact]
    public async Task Register_WithPasswordAtPolicyMinimum_Returns201()
    {
        var client = CreateClient();

        // 6 caracteres: el mínimo exacto de la política, debe aceptarse.
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = NewEmail(), password1 = "Pass123", password2 = "Pass123" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var userId = await ParseUserIdAsync(response);
        Assert.NotEqual(Guid.Empty, userId);
    }

    [Fact]
    public async Task Register_WithMismatchedPasswords_Returns400ProblemDetails()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = NewEmail(), password1 = "Password123", password2 = "Password456" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Solicitud incorrecta", problem.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409ProblemDetails()
    {
        var client = CreateClient();
        var email = NewEmail();
        var firstResponse = await RegisterAsync(client, email);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        await ParseUserIdAsync(firstResponse);

        var response = await RegisterAsync(client, email);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(409, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Conflicto", problem.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
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
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(401, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("No autorizado", problem.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
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
        client.DefaultRequestHeaders.Add("SqlToken", sqlToken.ToString());

        var response = await client.GetAsync("/api/core", TestContext.Current.CancellationToken);

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
        client.DefaultRequestHeaders.Add("SqlToken", Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/core", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Core_WithoutSqlTokenHeader_Returns401()
    {
        var client = CreateClient();
        var email = NewEmail();
        await ParseUserIdAsync(await RegisterAsync(client, email));
        var (userId, _, jwt) = await ParseLoginAsync(await LoginAsync(client, email, "Password123"));
        TrackCreatedUser(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/api/core", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Core_SqlTokenInQueryString_IsNotAccepted()
    {
        // El token de sesión ya no se acepta en la URL: queda expuesto en logs,
        // proxies e historial. Aunque se envíe, la API lo ignora.
        var client = CreateClient();
        var email = NewEmail();
        await ParseUserIdAsync(await RegisterAsync(client, email));
        var (userId, sqlToken, jwt) = await ParseLoginAsync(await LoginAsync(client, email, "Password123"));
        TrackCreatedUser(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync(
            $"/api/core?User_Id={userId}&SqlToken={sqlToken}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithMasterPasswordShorterThan8_Returns400()
    {
        var client = CreateClient();
        var email = NewEmail();
        await ParseUserIdAsync(await RegisterAsync(client, email));
        var (userId, sqlToken, jwt) = await ParseLoginAsync(await LoginAsync(client, email, "Password123"));
        TrackCreatedUser(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.PostAsJsonAsync("/api/core/register-password",
            new { password = "short", coreUser = new { user_Id = userId, sqlToken } },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeCorePassword_WithWrongOldPassword_Returns401()
    {
        var client = CreateClient();
        var email = NewEmail();
        await ParseUserIdAsync(await RegisterAsync(client, email));
        var (userId, sqlToken, jwt) = await ParseLoginAsync(await LoginAsync(client, email, "Password123"));
        TrackCreatedUser(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        await client.PostAsJsonAsync("/api/core/register-password",
            new { password = "InitialPass", coreUser = new { user_Id = userId, sqlToken } },
            TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync("/api/core/change-password",
            new
            {
                oldPassword = "WrongOldPass",
                newPassword = "NewPasswordLong",
                salt = "AAAAAAAAAAAAAAAAAAAAAA==",
                coreUser = new { user_Id = userId, sqlToken },
                records = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangeCorePassword_WithShortNewPassword_Returns400()
    {
        var client = CreateClient();
        var email = NewEmail();
        await ParseUserIdAsync(await RegisterAsync(client, email));
        var (userId, sqlToken, jwt) = await ParseLoginAsync(await LoginAsync(client, email, "Password123"));
        TrackCreatedUser(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        await client.PostAsJsonAsync("/api/core/register-password",
            new { password = "InitialPass", coreUser = new { user_Id = userId, sqlToken } },
            TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync("/api/core/change-password",
            new
            {
                oldPassword = "InitialPass",
                newPassword = "short",
                coreUser = new { user_Id = userId, sqlToken },
                records = Array.Empty<object>()
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeCorePassword_FullFlow_RotatesKeyAndToken()
    {
        var client = CreateClient();
        var email = NewEmail();
        await ParseUserIdAsync(await RegisterAsync(client, email));
        var (userId, sqlToken, jwt) = await ParseLoginAsync(await LoginAsync(client, email, "Password123"));
        TrackCreatedUser(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var registerPasswordResponse = await client.PostAsJsonAsync("/api/core/register-password",
            new { password = "InitialPass", coreUser = new { user_Id = userId, sqlToken } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registerPasswordResponse.StatusCode);

        client.DefaultRequestHeaders.Add("SqlToken", sqlToken.ToString());
        var originalId = Guid.NewGuid();
        var insertCoreResponse = await client.PostAsJsonAsync("/api/core",
            new { data_Id = originalId, data01 = "a", data02 = "b", data03 = "c", coreUser = new { user_Id = userId, sqlToken } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, insertCoreResponse.StatusCode);

        var replacementId = Guid.NewGuid();
        var newSalt = "AAAAAAAAAAAAAAAAAAAAAA=="; // 16 bytes en cero, base64 válido.
        var changeResponse = await client.PostAsJsonAsync("/api/core/change-password",
            new
            {
                oldPassword = "InitialPass",
                newPassword = "NewPasswordLong",
                salt = newSalt,
                coreUser = new { user_Id = userId, sqlToken },
                records = new[] { new { data_Id = replacementId, data01 = "x", data02 = "y", data03 = "z" } }
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);
        using var changeJson = JsonDocument.Parse(await changeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var newIV = changeJson.RootElement.GetProperty("iv").GetString();
        var newSqlToken = Guid.Parse(changeJson.RootElement.GetProperty("sqlToken").GetString()!);
        Assert.Equal(newSalt, newIV);
        Assert.NotEqual(sqlToken, newSqlToken);

        // La vieja contraseña con el token ya rotado no abre sesión.
        client.DefaultRequestHeaders.Remove("SqlToken");
        var oldPasswordResponse = await client.PostAsJsonAsync("/api/core/get-iv",
            new { password = "InitialPass", coreUser = new { user_Id = userId, sqlToken } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordResponse.StatusCode);

        // La nueva contraseña con el token rotado funciona y devuelve el nuevo IV.
        client.DefaultRequestHeaders.Add("SqlToken", newSqlToken.ToString());
        var getIvNewResponse = await client.PostAsJsonAsync("/api/core/get-iv",
            new { password = "NewPasswordLong", coreUser = new { user_Id = userId, sqlToken = newSqlToken } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getIvNewResponse.StatusCode);
        using var ivJson = JsonDocument.Parse(await getIvNewResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(newIV, ivJson.RootElement.GetProperty("iv").GetString());

        // Los datos fueron reemplazados: solo queda el registro del cambio.
        var getAllResponse = await client.GetAsync("/api/core", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getAllResponse.StatusCode);
        using var coreJson = JsonDocument.Parse(await getAllResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(coreJson.RootElement.EnumerateArray(), x => x.GetProperty("data_Id").GetString() == replacementId.ToString());
        Assert.DoesNotContain(coreJson.RootElement.EnumerateArray(), x => x.GetProperty("data_Id").GetString() == originalId.ToString());
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
        client.DefaultRequestHeaders.Remove("X-ApiKey");
        client.DefaultRequestHeaders.Add("X-ApiKey", "Wrong-Key");

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = NewEmail(), password = "Password123" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_Returns404_WithProblemDetails()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/does-not-exist", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task SecurityHeaders_PresentOnApiRoute()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/core", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Contains("DENY", response.Headers.GetValues("X-Frame-Options"));
    }
}