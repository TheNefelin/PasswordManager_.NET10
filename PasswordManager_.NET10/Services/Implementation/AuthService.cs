using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.DTOs.Request;
using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Services.Implementation;

public class AuthService : IAuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly IApiService _apiService;
    private readonly ISecureStorageService _secureStorageService;
    private User? _currentUser;

    public AuthService(
        ILogger<AuthService> logger,
        IApiService apiService,
        ISecureStorageService secureStorageService)
    {
        _logger = logger;
        _apiService = apiService;
        _secureStorageService = secureStorageService;
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

            var response = await _apiService.LoginAsync(loginRequest);

            if (!response.IsSuccess || response.Data == null)
            {
                _logger.LogWarning("[AuthService-LoginAsync] Login failed: {Message}", response.Message);
                throw new Exception(response.Message ?? "Error en login");
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
}