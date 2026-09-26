using PasswordManager_.NET10.Exceptions;
using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.Services.Interfaces;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PasswordManager_.NET10.Services.Implementation;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public void SetAuthToken(string? token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
        }
    }

    public async Task<T> GetAsync<T>(
        string endpoint,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

        if (headers is not null)
        {
            foreach (var header in headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return await SendAsync<T>(request, "GET", cancellationToken);
    }

    public async Task<T> PostAsync<T>(
        string endpoint,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = data is null ? null : JsonContent.Create(data, options: _jsonOptions)
        };

        return await SendAsync<T>(request, "POST", cancellationToken);
    }

    public async Task<T> PutAsync<T>(
        string endpoint,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
        {
            Content = data is null ? null : JsonContent.Create(data, options: _jsonOptions)
        };

        return await SendAsync<T>(request, "PUT", cancellationToken);
    }

    public async Task DeleteAsync(
        string endpoint,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint)
        {
            Content = data is null ? null : JsonContent.Create(data, options: _jsonOptions)
        };

        await SendAsync<object>(request, "DELETE", cancellationToken);
    }

    private async Task<T> SendAsync<T>(
        HttpRequestMessage request,
        string method,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, method, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
            return default!;

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
            return default!;

        try
        {
            return JsonSerializer.Deserialize<T>(content, _jsonOptions)
                ?? throw new ApiException(
                    $"No se pudo deserializar la respuesta de {method}.",
                    (int)response.StatusCode);
        }
        catch (JsonException ex)
        {
            throw new ApiException(
                $"No se pudo deserializar la respuesta de {method}.",
                (int)response.StatusCode,
                ex.Message);
        }
    }

    private async Task<ApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        string method,
        CancellationToken cancellationToken)
    {
        ApiProblemDetails? problem = null;
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                problem = JsonSerializer.Deserialize<ApiProblemDetails>(content, _jsonOptions);
            }
            catch (JsonException)
            {
            }
        }

        var detail = problem?.Detail;
        var title = problem?.Title;
        var message = !string.IsNullOrWhiteSpace(detail)
            ? detail
            : !string.IsNullOrWhiteSpace(title)
                ? title
                : $"Error en {method}.";

        return new ApiException(
            message,
            (int)response.StatusCode,
            title,
            problem?.TraceId,
            problem?.Errors);
    }
}
