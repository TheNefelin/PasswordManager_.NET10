namespace PasswordManager_.NET10.Services.Interfaces;

public interface IStorageService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SaveAsync<T>(string key, T value) where T : class;
    Task DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);
}