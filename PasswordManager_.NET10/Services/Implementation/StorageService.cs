using PasswordManager_.NET10.Services.Interfaces;
using System.Diagnostics;
using System.Text.Json;

namespace PasswordManager_.NET10.Services.Implementation;

public class StorageService : IStorageService
{
    private readonly string _storagePath;

    public StorageService()
    {
        _storagePath = FileSystem.AppDataDirectory;
    }

    /// <summary>
    /// Obtener datos del almacenamiento local
    /// </summary>
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var filePath = Path.Combine(_storagePath, $"{key}.json");

            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Guardar datos en almacenamiento local
    /// </summary>
    public async Task SaveAsync<T>(string key, T value) where T : class
    {
        try
        {
            var filePath = Path.Combine(_storagePath, $"{key}.json");
            var json = JsonSerializer.Serialize(value);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving to storage: {ex.Message}");
        }
    }

    /// <summary>
    /// Eliminar datos del almacenamiento local
    /// </summary>
    public async Task DeleteAsync(string key)
    {
        try
        {
            var filePath = Path.Combine(_storagePath, $"{key}.json");

            if (File.Exists(filePath))
                File.Delete(filePath);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error deleting from storage: {ex.Message}");
        }
    }

    /// <summary>
    /// Verificar si existe una clave en el almacenamiento
    /// </summary>
    public async Task<bool> ExistsAsync(string key)
    {
        var filePath = Path.Combine(_storagePath, $"{key}.json");
        return await Task.FromResult(File.Exists(filePath));
    }
}