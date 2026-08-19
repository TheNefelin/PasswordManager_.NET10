using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Interfaces;
using WebApiCore.Domain.Entities;
using WebApiCore.Domain.Interfaces;

namespace WebApiCore.Application.Services;

public class AuthUserService : IAuthUserService
{
    private readonly IAuthUserRepository _authUserRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthTokenService _authTokenService;

    public AuthUserService(
        IAuthUserRepository authUserRepository,
        IPasswordHasher passwordHasher,
        IAuthTokenService authTokenService)
    {
        _authUserRepository = authUserRepository;
        _passwordHasher = passwordHasher;
        _authTokenService = authTokenService;
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

    public async Task<ApiResponse<AuthUserLogged>> LoginAsync(AuthUserLogin authUserLogin, CancellationToken cancellationToken)
    {
        var authUser = await _authUserRepository.GetUserByEmailAsync(authUserLogin.Email, cancellationToken);

        if (authUser == null)
            return ApiResponse.Failure<AuthUserLogged>(401, "Usuario o contraseña incorrecta.");

        if (!_passwordHasher.VerifyPassword(authUserLogin.Password, authUser.HashLogin, authUser.SaltLogin))
            return ApiResponse.Failure<AuthUserLogged>(401, "Usuario o contraseña incorrecta.");

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