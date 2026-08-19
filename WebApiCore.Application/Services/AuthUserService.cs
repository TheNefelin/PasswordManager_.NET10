using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Interfaces;
using WebApiCore.Domain.Entities;
using WebApiCore.Domain.Interfaces;

namespace WebApiCore.Application.Services;

public class AuthUserService : IAuthUserService
{
    private const string TooManyLoginAttempts = "Demasiados intentos fallidos de inicio de sesión. Intenta nuevamente más tarde.";

    private readonly IAuthUserRepository _authUserRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthTokenService _authTokenService;
    private readonly IIpLockoutService _loginLockoutService;

    public AuthUserService(
        IAuthUserRepository authUserRepository,
        IPasswordHasher passwordHasher,
        IAuthTokenService authTokenService,
        IIpLockoutService loginLockoutService)
    {
        _authUserRepository = authUserRepository;
        _passwordHasher = passwordHasher;
        _authTokenService = authTokenService;
        _loginLockoutService = loginLockoutService;
    }

    public async Task<ApiResponse<AuthUserResponse>> RegisterAsync(AuthUserRegister authUserRegister, CancellationToken cancellationToken)
    {
        if (!authUserRegister.Password1.Equals(authUserRegister.Password2))
            return ApiResponse.Failure<AuthUserResponse>(400, "Las contraseñas no coinciden.");

        var existingUser = await _authUserRepository.GetUserByEmailAsync(authUserRegister.Email, cancellationToken);
        if (existingUser != null)
            return ApiResponse.Failure<AuthUserResponse>(400, "Ya estás registrado.");

        var (hash, salt) = _passwordHasher.HashPassword(authUserRegister.Password1);
        var authUser = new AuthUser
        {
            User_Id = Guid.NewGuid(),
            Email = authUserRegister.Email,
            HashLogin = hash,
            SaltLogin = salt
        };

        var result = await _authUserRepository.CreateUserAsync(authUser, cancellationToken);

        if (result == null || !result.IsSuccess)
            return ApiResponse.Failure<AuthUserResponse>(
                result?.StatusCode ?? 500,
                result?.Message ?? "No se pudo registrar el usuario.");

        return ApiResponse.Success(
            new AuthUserResponse { User_Id = authUser.User_Id },
            result.Message,
            result.StatusCode);
    }

    public async Task<ApiResponse<AuthUserLogged>> LoginAsync(AuthUserLogin authUserLogin, string ipAddress, CancellationToken cancellationToken)
    {
        if (_loginLockoutService.IsBlocked(ipAddress))
            return ApiResponse.Failure<AuthUserLogged>(429, TooManyLoginAttempts);

        var authUser = await _authUserRepository.GetUserByEmailAsync(authUserLogin.Email, cancellationToken);

        if (authUser == null)
        {
            _loginLockoutService.RegisterFailure(ipAddress);
            return ApiResponse.Failure<AuthUserLogged>(401, "Usuario o contraseña incorrecta.");
        }

        if (!_passwordHasher.VerifyPassword(authUserLogin.Password, authUser.HashLogin, authUser.SaltLogin))
        {
            _loginLockoutService.RegisterFailure(ipAddress);
            return ApiResponse.Failure<AuthUserLogged>(401, "Usuario o contraseña incorrecta.");
        }

        _loginLockoutService.Reset(ipAddress);

        var sqlToken = await _authUserRepository.NewSqlToken(authUser.Email, cancellationToken);
        var token = _authTokenService.GenerateToken(authUser);

        return ApiResponse.Success(
            new AuthUserLogged
            {
                User_Id = authUser.User_Id,
                SqlToken = sqlToken,
                Role = authUser.Role ?? "USER",
                ExpireMin = token.ExpireMin.ToString(),
                ApiToken = token.Token
            },
            "Login exitoso.");
    }
}