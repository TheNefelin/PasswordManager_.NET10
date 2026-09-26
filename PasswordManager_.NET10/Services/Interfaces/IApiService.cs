namespace PasswordManager_.NET10.Services.Interfaces;

public interface IApiService
{
    void SetAuthToken(string? token);
    Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default);
    Task<T> PostAsync<T>(string endpoint, object? data = null, CancellationToken cancellationToken = default);
    Task<T> PutAsync<T>(string endpoint, object? data = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(string endpoint, object? data = null, CancellationToken cancellationToken = default);
}