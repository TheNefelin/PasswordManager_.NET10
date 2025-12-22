using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Services.Implementation;

/// <summary>
/// Service for secure storage of sensitive user session data.
/// Uses MAUI's SecureStorage which encrypts data at the OS level.
/// </summary>
public class SecureStorageService : ISecureStorageService
{
    private readonly ILogger<SecureStorageService> _logger;

    private const string KEY_USER_ID = "UserId";
    private const string KEY_EMAIL = "Email";
    private const string KEY_PASS = "Idiot";
    private const string KEY_SQL_TOKEN = "SqlToken";
    private const string KEY_ROLE = "Role";
    private const string KEY_EXPIRE_MIN = "ExpireMin";
    private const string KEY_API_TOKEN = "ApiToken";
    private const string KEY_EXPIRATION_TIME = "ExpirationTime";
    private const string KEY_BIOMETRICS_ENABLED = "BiometricsEnabled";

    public SecureStorageService(ILogger<SecureStorageService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Saves the entire session data securely
    /// </summary>
    public async Task SetSessionAsync(SessionData sessionData)
    {
        try
        {
            if (sessionData == null)
            {
                throw new ArgumentNullException(nameof(sessionData), "Session data cannot be null");
            }

            await SecureStorage.SetAsync(KEY_USER_ID, sessionData.UserId ?? string.Empty);
            await SecureStorage.SetAsync(KEY_EMAIL, sessionData.Email ?? string.Empty);
            await SecureStorage.SetAsync(KEY_SQL_TOKEN, sessionData.SqlToken ?? string.Empty);
            await SecureStorage.SetAsync(KEY_ROLE, sessionData.Role ?? string.Empty);
            await SecureStorage.SetAsync(KEY_EXPIRE_MIN, sessionData.ExpireMin ?? string.Empty);
            await SecureStorage.SetAsync(KEY_API_TOKEN, sessionData.ApiToken ?? string.Empty);
            await SecureStorage.SetAsync(KEY_EXPIRATION_TIME, sessionData.ExpirationTime.ToString("o"));

            _logger.LogInformation("[SecureStorageService-SetSessionAsync] Session saved successfully for user: {UserId}", sessionData.UserId);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "[SecureStorageService-SetSessionAsync] Validation error: {Message}", ex.Message);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "[SecureStorageService-SetSessionAsync] SecureStorage operation failed: {Message}", ex.Message);
            throw new Exception($"Failed to save session data: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SecureStorageService-SetSessionAsync] Unexpected error: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Clears all session data from secure storage
    /// </summary>
    public async Task ClearSessionAsync()
    {
        try
        {
            SecureStorage.Remove(KEY_USER_ID);
            SecureStorage.Remove(KEY_SQL_TOKEN);
            SecureStorage.Remove(KEY_ROLE);
            SecureStorage.Remove(KEY_EXPIRE_MIN);
            SecureStorage.Remove(KEY_API_TOKEN);
            SecureStorage.Remove(KEY_EXPIRATION_TIME);

            _logger.LogInformation("[SecureStorageService-ClearSessionAsync] Session cleared successfully");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SecureStorageService-ClearSessionAsync] Error clearing session: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the stored user ID
    /// </summary>
    public async Task<string?> GetUserIdAsync()
    {
        try
        {
            var userId = await SecureStorage.GetAsync(KEY_USER_ID);
            _logger.LogDebug("[SecureStorageService-GetUserIdAsync] User ID retrieved successfully");
            return userId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SecureStorageService-GetUserIdAsync] Error retrieving user ID: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
            return null;
        }
    }

    public async Task<string?> GetEmailAsync()
    {
        try
        {
            var email = await SecureStorage.GetAsync(KEY_EMAIL);
            _logger.LogDebug("[SecureStorageService-GetEmailAsync] Email retrieved successfully");
            return email;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SecureStorageService-GetEmailAsync] Error retrieving email: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Gets the stored SQL token
    /// </summary>
    public async Task<string?> GetSqlTokenAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync(KEY_SQL_TOKEN);
            _logger.LogDebug("[SecureStorageService-GetSqlTokenAsync] SQL token retrieved successfully");
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SecureStorageService-GetSqlTokenAsync] Error retrieving SQL token: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Gets the stored user role
    /// </summary>
    public async Task<string?> GetRoleAsync()
    {
        try
        {
            var role = await SecureStorage.GetAsync(KEY_ROLE);
            _logger.LogDebug("[SecureStorageService-GetRoleAsync] Role retrieved successfully");
            return role;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SecureStorageService-GetRoleAsync] Error retrieving role: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Gets the stored API token
    /// </summary>
    public async Task<string?> GetApiTokenAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync(KEY_API_TOKEN);
            _logger.LogDebug("[SecureStorageService-GetApiTokenAsync] API token retrieved successfully");
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SecureStorageService-GetApiTokenAsync] Error retrieving API token: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Gets the expiration minutes as an integer
    /// </summary>
    public async Task<int> GetExpireMinAsync()
    {
        try
        {
            var expireMinString = await SecureStorage.GetAsync(KEY_EXPIRE_MIN);

            if (string.IsNullOrEmpty(expireMinString))
            {
                _logger.LogWarning("[SecureStorageService-GetExpireMinAsync] Expire minutes not found, returning default value 0");
                return 0;
            }

            if (!int.TryParse(expireMinString, out var expireMin))
            {
                _logger.LogWarning("[SecureStorageService-GetExpireMinAsync] Invalid expire minutes format: {InvalidValue}, returning 0", expireMinString);
                return 0;
            }

            return expireMin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SecureStorageService-GetExpireMinAsync] Error retrieving expire minutes: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// Gets the expiration date/time
    /// </summary>
    public async Task<DateTime?> GetExpirationTimeAsync()
    {
        try
        {
            var expirationStr = await SecureStorage.GetAsync(KEY_EXPIRATION_TIME);

            if (string.IsNullOrEmpty(expirationStr))
            {
                _logger.LogWarning("[SecureStorageService-GetExpirationTimeAsync] Expiration time not found");
                return null;
            }

            if (!DateTime.TryParse(expirationStr, out var expirationTime))
            {
                _logger.LogWarning("[SecureStorageService-GetExpirationTimeAsync] Invalid expiration time format: {InvalidValue}", expirationStr);
                return null;
            }

            return expirationTime;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SecureStorageService-GetExpirationTimeAsync] Error retrieving expiration time: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Checks if user has a valid authenticated session
    /// </summary>
    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var token = await GetApiTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("[SecureStorageService-IsAuthenticatedAsync] No API token found - not authenticated");
                return false;
            }

            var expirationTime = await GetExpirationTimeAsync();

            if (expirationTime == null)
            {
                _logger.LogWarning("[SecureStorageService-IsAuthenticatedAsync] Expiration time not found - clearing session");
                await ClearSessionAsync();
                return false;
            }

            if (DateTime.UtcNow > expirationTime)
            {
                _logger.LogInformation("[SecureStorageService-IsAuthenticatedAsync] Session expired - clearing session");
                await ClearSessionAsync();
                return false;
            }

            _logger.LogDebug("[SecureStorageService-IsAuthenticatedAsync] User is authenticated");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SecureStorageService-IsAuthenticatedAsync] Error checking authentication: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
            return false;
        }
    }

    public async Task SetBiometricsEnabledAsync(bool isEnabled)
    {
       await SecureStorage.SetAsync(KEY_BIOMETRICS_ENABLED, isEnabled.ToString());
    }

    public async Task<bool> IsBiometricsEnabledAsync()
    {
        var enabledStr = await SecureStorage.GetAsync(KEY_BIOMETRICS_ENABLED);
        return bool.TryParse(enabledStr, out var isEnabled) && isEnabled;
    }
}