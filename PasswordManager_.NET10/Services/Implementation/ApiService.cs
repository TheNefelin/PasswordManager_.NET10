using PasswordManager_.NET10.DTOs.Request;
using PasswordManager_.NET10.DTOs.Response;
using PasswordManager_.NET10.Exceptions;
using PasswordManager_.NET10.Helpers;
using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.Services.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;

namespace PasswordManager_.NET10.Services.Implementation;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private string? _currentApiToken;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiService(HttpClient httpClient)
    {
        var handler = new HttpClientHandler();

        // Para desarrollo: permitir SSL no seguro (SOLO en DEV)
#if DEBUG
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(Constants.API_BASE_URL),
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Headers por defecto
        _httpClient.DefaultRequestHeaders.Add("ApiKey", Constants.API_KEY);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "PasswordManager-MAUI/1.0");

        // Configurar opciones JSON
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Establece el token API para request autenticados
    /// </summary>
    public void SetAuthToken(string? token)
    {
        _currentApiToken = token;

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

    /// <summary>
    /// Deserializa respuesta JSON
    /// </summary>
    private async Task<ApiResponse<T>> DeserializeResponseAsync<T>(HttpContent content)
    {
        var json = await content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(json, _jsonOptions)
            ?? throw new ApiException("No se pudo deserializar la respuesta");
    }

    /// <summary>
    /// Login del usuario
    /// </summary>
    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                Constants.LOGIN_ENDPOINT,
                request
            );

            var content = await DeserializeResponseAsync<LoginResponse>(response.Content);

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(
                    content?.Message ?? "Error en login",
                    (int)response.StatusCode,
                    content?.Message
                );
            }

            return content;
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException($"Error de conexión: {ex.Message}", 0, ex.Message);
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiException($"Error inesperado: {ex.Message}", 0, ex.Message);
        }
    }

    /// <summary>
    /// GET genérico
    /// </summary>
    public async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint);
            var content = await DeserializeResponseAsync<T>(response.Content);

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(
                    content?.Message ?? "Error en GET",
                    (int)response.StatusCode
                );
            }

            return content;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiException($"Error GET: {ex.Message}");
        }
    }

    /// <summary>
    /// POST genérico
    /// </summary>
    public async Task<ApiResponse<T>> PostAsync<T>(string endpoint, object? data = null)
    {
        try
        {
            var response = data == null
                ? await _httpClient.PostAsync(endpoint, null)
                : await _httpClient.PostAsJsonAsync(endpoint, data);

            var content = await DeserializeResponseAsync<T>(response.Content);

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(
                    content?.Message ?? "Error en POST",
                    (int)response.StatusCode
                );
            }

            return content;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiException($"Error POST: {ex.Message}");
        }
    }

    /// <summary>
    /// PUT genérico
    /// </summary>
    public async Task<ApiResponse<T>> PutAsync<T>(string endpoint, object? data = null)
    {
        try
        {
            var response = data == null
                ? await _httpClient.PutAsync(endpoint, null)
                : await _httpClient.PutAsJsonAsync(endpoint, data);

            var content = await DeserializeResponseAsync<T>(response.Content);

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(
                    content?.Message ?? "Error en PUT",
                    (int)response.StatusCode
                );
            }

            return content;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiException($"Error PUT: {ex.Message}");
        }
    }

    /// <summary>
    /// DELETE genérico
    /// </summary>
    //public async Task<ApiResponse<bool>> DeleteAsync(string endpoint)
    //{
    //    try
    //    {
    //        var response = await _httpClient.DeleteAsync(endpoint);
    //        var content = await DeserializeResponseAsync<bool>(response.Content);

    //        if (!response.IsSuccessStatusCode)
    //        {
    //            throw new ApiException(
    //                content?.Message ?? "Error en DELETE",
    //                (int)response.StatusCode
    //            );
    //        }

    //        return content;
    //    }
    //    catch (ApiException)
    //    {
    //        throw;
    //    }
    //    catch (Exception ex)
    //    {
    //        throw new ApiException($"Error DELETE: {ex.Message}");
    //    }
    //}

    public async Task<ApiResponse<T>> DeleteAsync<T>(string endpoint, object? data = null)
    {
        try
        {
            HttpResponseMessage response;

            if (data == null)
            {
                response = await _httpClient.DeleteAsync(endpoint);
            }
            else
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
                request.Content = JsonContent.Create(data);
                response = await _httpClient.SendAsync(request);
            }

            var content = await DeserializeResponseAsync<T>(response.Content);

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(
                    content?.Message ?? "Error en DELETE",
                    (int)response.StatusCode
                );
            }

            return content;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiException($"Error DELETE: {ex.Message}");
        }
    }
}