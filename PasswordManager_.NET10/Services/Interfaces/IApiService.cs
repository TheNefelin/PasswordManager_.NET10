using PasswordManager_.NET10.DTOs.Request;
using PasswordManager_.NET10.DTOs.Response;
using PasswordManager_.NET10.Models;

namespace PasswordManager_.NET10.Services.Interfaces;

public interface IApiService
{
    void SetAuthToken(string? token);
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<T>> GetAsync<T>(string endpoint);
    Task<ApiResponse<T>> PostAsync<T>(string endpoint, object? data = null);
    Task<ApiResponse<T>> PutAsync<T>(string endpoint, object? data = null);
    Task<ApiResponse<T>> DeleteAsync<T>(string endpoint, object? data = null);
}