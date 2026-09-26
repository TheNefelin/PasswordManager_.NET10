using System.Net;
using PasswordManager_.NET10.DTOs.Response;
using PasswordManager_.NET10.Exceptions;
using PasswordManager_.NET10.Services.Implementation;
using PasswordManager_.NET10.Tests.Fakes;

namespace PasswordManager_.NET10.Tests.Services;

public class ApiServiceTests
{
    [Fact]
    public async Task GetAsync_WithFlatJsonResponse_ReturnsDto()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "user_Id": "11111111-1111-1111-1111-111111111111",
              "sqlToken": "22222222-2222-2222-2222-222222222222",
              "role": "USER",
              "expireMin": "60",
              "apiToken": "api-token"
            }
            """));
        using var client = CreateClient(handler);
        var service = new ApiService(client);

        var result = await service.GetAsync<LoginResponse>("/api/login-response", TestContext.Current.CancellationToken);

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), result.UserId);
        Assert.Equal("USER", result.Role);
        Assert.Equal("60", result.ExpireMin);
        Assert.Equal("api-token", result.ApiToken);
    }

    [Fact]
    public async Task PostAsync_WithCreatedResponse_ReturnsFlatDto()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.Created,
            """{"user_Id":"11111111-1111-1111-1111-111111111111"}"""));
        using var client = CreateClient(handler);
        var service = new ApiService(client);

        var result = await service.PostAsync<RegisterResponse>(
            "/api/register",
            new { email = "user@example.com" },
            TestContext.Current.CancellationToken);

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), result.User_Id);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
    }

    [Fact]
    public async Task DeleteAsync_WithNoContent_CompletesWithoutDeserializingBody()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = CreateClient(handler);
        var service = new ApiService(client);

        await service.DeleteAsync(
            "/api/core",
            new { data_Id = Guid.NewGuid() },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Delete, handler.LastRequest?.Method);
    }

    [Fact]
    public async Task GetAsync_WithProblemDetails_ThrowsApiExceptionWithServerDetails()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.BadRequest,
            """
            {
              "type": "https://tools.ietf.org/html/rfc9110",
              "title": "Solicitud incorrecta",
              "status": 400,
              "detail": "Los datos no son válidos.",
              "traceId": "trace-123",
              "errors": {
                "Email": ["El correo es obligatorio."]
              }
            }
            """,
            "application/problem+json"));
        using var client = CreateClient(handler);
        var service = new ApiService(client);

        var exception = await Assert.ThrowsAsync<ApiException>(
            () => service.GetAsync<LoginResponse>("/api/login-response", TestContext.Current.CancellationToken));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("Los datos no son válidos.", exception.Message);
        Assert.Equal("Solicitud incorrecta", exception.ErrorMessage);
        Assert.Equal("trace-123", exception.TraceId);
        Assert.NotNull(exception.ValidationErrors);
        Assert.Equal("El correo es obligatorio.", exception.ValidationErrors!["Email"][0]);
    }

    [Fact]
    public async Task GetAsync_WithNonJsonError_ThrowsApiExceptionWithoutJsonException()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>proxy error</html>")
        });
        using var client = CreateClient(handler);
        var service = new ApiService(client);

        var exception = await Assert.ThrowsAsync<ApiException>(
            () => service.GetAsync<LoginResponse>("/api/login-response", TestContext.Current.CancellationToken));

        Assert.Equal(502, exception.StatusCode);
        Assert.Equal("Error en GET.", exception.Message);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler)
        => new(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string content,
        string mediaType = "application/json")
        => new(statusCode)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, mediaType)
        };
}
