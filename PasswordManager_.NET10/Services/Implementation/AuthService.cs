using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.DTOs.Request;
using PasswordManager_.NET10.DTOs.Response;
using PasswordManager_.NET10.Helpers;
using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Services.Implementation;

public class AuthService : IAuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly IApiService _apiService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly IEncryptionService _encryptionService;
    private User? _currentUser;

    public AuthService(
        ILogger<AuthService> logger,
        IApiService apiService,
        ISecureStorageService secureStorageService,
        IEncryptionService encryptionService)
    {
        _logger = logger;
        _apiService = apiService;
        _secureStorageService = secureStorageService;
        _encryptionService = encryptionService;
    }

    public async Task<bool> RegisterAsync(string email, string password, string confirmPassword)
    {
        try
        {
            _logger.LogInformation("[AuthService-RegisterAsync] Attempting registration for email: {Email}", email);

            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password1 = password,
                Password2 = confirmPassword
            };

            var response = await _apiService.PostAsync<RegisterResponse>(Constants.REGISTER_ENDPOINT, registerRequest);

            if (!response.IsSuccess)
            {
                _logger.LogWarning("[AuthService-RegisterAsync] Registration failed: {Message}", response.Message);
                throw new Exception($"Failed to register user. StatusCode: {response.StatusCode}, Message: {response.Message}");
            }

            _logger.LogInformation("[AuthService-RegisterAsync] Registration successful for email: {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService-LoginAsync] Login error: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Login del usuario
    /// </summary>
    public async Task<User> LoginAsync(string email, string password)
    {
        try
        {
            _logger.LogInformation("[AuthService-LoginAsync] Attempting login for email: {Email}", email);

            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = password
            };

            var response = await _apiService.PostAsync<LoginResponse>(Constants.LOGIN_ENDPOINT, loginRequest);

            if (!response.IsSuccess || response.Data == null)
            {
                _logger.LogWarning("[AuthService-LoginAsync] Login failed: {Message}", response.Message);
                throw new Exception($"Failed to register core password. StatusCode: {response.StatusCode}, Message: {response.Message}");
            }

            var expirationTime = DateTime.UtcNow.AddMinutes(int.Parse(response.Data.ExpireMin));

            // Crear modelo User
            var user = new User
            {
                UserId = response.Data.UserId,
                Email = email,
                Role = response.Data.Role,
                ApiToken = response.Data.ApiToken,
                SqlToken = response.Data.SqlToken.ToString(),
                TokenExpiry = expirationTime,
                IsAuthenticated = true
            };

            // Guardar en almacenamiento SEGURO (encriptado)
            var sessionData = new SessionData
            {
                UserId = user.UserId.ToString(),
                Email = email,
                SqlToken = user.SqlToken,
                Role = user.Role,
                ExpireMin = response.Data.ExpireMin,
                ApiToken = user.ApiToken,
                ExpirationTime = expirationTime
            };

            await _secureStorageService.SetSessionAsync(sessionData);
            _logger.LogInformation("[AuthService-LoginAsync] Session saved securely for user: {UserId}", user.UserId);

            // Configurar token en ApiService
            _apiService.SetAuthToken(user.ApiToken);

            _currentUser = user;

            _logger.LogInformation("[AuthService-LoginAsync] Login successful for user: {UserId}", user.UserId);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService-LoginAsync] Login error: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Logout del usuario
    /// </summary>
    public async Task LogoutAsync()
    {
        try
        {
            _logger.LogInformation("[AuthService-LogoutAsync] User logout initiated");

            await _secureStorageService.ClearSessionAsync();
            _apiService.SetAuthToken(null);
            _currentUser = null;

            _logger.LogInformation("[AuthService-LogoutAsync] Logout successful - session cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService-LogoutAsync] Logout error: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Obtener usuario actual
    /// </summary>
    public async Task<User?> GetCurrentUserAsync()
    {
        try
        {
            if (_currentUser != null)
                return _currentUser;

            _logger.LogDebug("[AuthService-GetCurrentUserAsync] Retrieving current user from secure storage");

            var userId = await _secureStorageService.GetUserIdAsync();

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogDebug("[AuthService-GetCurrentUserAsync] No user found in secure storage");
                return null;
            }

            var apiToken = await _secureStorageService.GetApiTokenAsync();
            var role = await _secureStorageService.GetRoleAsync();
            var sqlToken = await _secureStorageService.GetSqlTokenAsync();
            var expirationTime = await _secureStorageService.GetExpirationTimeAsync();

            _currentUser = new User
            {
                UserId = Guid.Parse(userId),
                ApiToken = apiToken ?? string.Empty,
                Role = role ?? string.Empty,
                SqlToken = sqlToken ?? string.Empty,
                TokenExpiry = expirationTime ?? DateTime.UtcNow,
                IsAuthenticated = true
            };

            if (_currentUser.ApiToken != null)
            {
                _apiService.SetAuthToken(_currentUser.ApiToken);
            }

            _logger.LogInformation("[AuthService-GetCurrentUserAsync] Current user retrieved: {UserId}", userId);
            return _currentUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService-GetCurrentUserAsync] Error retrieving current user: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Verificar si está autenticado
    /// </summary>
    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            _logger.LogDebug("[AuthService-IsAuthenticatedAsync] Checking authentication status");

            var isAuthenticated = await _secureStorageService.IsAuthenticatedAsync();

            _logger.LogInformation("[AuthService-IsAuthenticatedAsync] Authentication status: {IsAuthenticated}", isAuthenticated);
            return isAuthenticated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService-IsAuthenticatedAsync] Error checking authentication: {ExceptionType} - {Message}",
                ex.GetType().Name, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Establece el flag para guardar contraseña en el próximo login
    /// </summary>
    public async Task SetSavePasswordOnNextLoginAsync(bool value)
    {
        try
        {
            await _secureStorageService.SetSavePasswordOnNextLoginAsync(value);
            _logger.LogInformation("[AuthService-SetSavePasswordOnNextLoginAsync] Flag set to: {Value}", value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService-SetSavePasswordOnNextLoginAsync] Error setting flag: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Obtiene el flag de guardar contraseña en próximo login
    /// </summary>
    public async Task<bool> GetSavePasswordOnNextLoginAsync()
    {
        try
        {
            var result = await _secureStorageService.GetSavePasswordOnNextLoginAsync();
            _logger.LogDebug("[AuthService-GetSavePasswordOnNextLoginAsync] Flag value: {Value}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService-GetSavePasswordOnNextLoginAsync] Error getting flag: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Guarda la contraseña encriptada
    /// </summary>
    public async Task SavePasswordAsync(string password)
    {
        try
        {
            string encryptedPassword = _encryptionService.Encrypt(password);
            await _secureStorageService.SetSavedPasswordAsync(encryptedPassword);
            _logger.LogInformation("[AuthService-SavePasswordAsync] Password saved securely");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService-SavePasswordAsync] Error: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Borra la contraseña guardada
    /// </summary>
    public async Task ClearSavedPasswordAsync()
    {
        try
        {
            await _secureStorageService.ClearSavedPasswordAsync();
            _logger.LogInformation("[AuthService-ClearSavedPasswordAsync] Saved password cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService-ClearSavedPasswordAsync] Error clearing password: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Obtiene la contraseña encriptada guardada
    /// </summary>
    public async Task<string?> GetSavedPasswordAsync()
    {
        try
        {
            var encryptedPassword = await _secureStorageService.GetSavedPasswordAsync();
            if (string.IsNullOrEmpty(encryptedPassword))
                return null;

            return _encryptionService.Decrypt(encryptedPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService-GetSavedPasswordAsync] Error: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Verifica si hay una contraseña guardada
    /// </summary>
    public async Task<bool> HasSavedPasswordAsync()
    {
        try
        {
            var result = await _secureStorageService.HasSavedPasswordAsync();
            _logger.LogDebug("[AuthService-HasSavedPasswordAsync] Has saved password: {HasPassword}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthService-HasSavedPasswordAsync] Error checking saved password: {Message}", ex.Message);
            return false;
        }
    }
}