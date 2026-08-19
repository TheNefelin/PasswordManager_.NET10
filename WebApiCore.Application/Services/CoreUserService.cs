using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Interfaces;
using WebApiCore.Domain.Entities;
using WebApiCore.Domain.Interfaces;

namespace WebApiCore.Application.Services;

public class CoreUserService : ICoreUserService
{
    private readonly ICoreUserRepository _coreUserRepository;
    private readonly IPasswordHasher _passwordHasher;

    public CoreUserService(ICoreUserRepository coreUserRepository, IPasswordHasher passwordHasher)
    {
        _coreUserRepository = coreUserRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<ApiResponse<CoreUserIV>> RegisterCoreUserPasswordAsync(CoreUserPassword coreUserPassword, CancellationToken cancellationToken)
    {
        var coreUser = await _coreUserRepository.GetCoreUserAsync(
            new CoreUser
            {
                User_Id = coreUserPassword.CoreUser.User_Id,
                SqlToken = coreUserPassword.CoreUser.SqlToken
            },
            cancellationToken);

        if (coreUser == null)
            return ApiResponse.Failure<CoreUserIV>(401, "Debes iniciar sesión.");

        if (!string.IsNullOrEmpty(coreUser.HashPM) && !string.IsNullOrEmpty(coreUser.SaltPM))
            return ApiResponse.Failure<CoreUserIV>(400, "Ya tienes una clave de encriptación creada.");

        var (hash, salt) = _passwordHasher.HashPassword(coreUserPassword.Password);
        coreUser.HashPM = hash;
        coreUser.SaltPM = salt;

        await _coreUserRepository.RegisterCoreUserPasswordAsync(coreUser, cancellationToken);

        return ApiResponse.Success(
            new CoreUserIV { IV = salt },
            "Clave de encriptación creada correctamente.");
    }

    public async Task<ApiResponse<CoreUserIV>> GetCoreUserIVAsync(CoreUserPassword coreUserPassword, CancellationToken cancellationToken)
    {
        var coreUser = await _coreUserRepository.GetCoreUserAsync(
            new CoreUser
            {
                User_Id = coreUserPassword.CoreUser.User_Id,
                SqlToken = coreUserPassword.CoreUser.SqlToken
            },
            cancellationToken);

        if (coreUser == null)
            return ApiResponse.Failure<CoreUserIV>(401, "Debes iniciar sesión.");

        if (string.IsNullOrEmpty(coreUser.HashPM) || string.IsNullOrEmpty(coreUser.SaltPM))
            return ApiResponse.Failure<CoreUserIV>(401, "Debes crear una clave de encriptación.");

        if (!_passwordHasher.VerifyPassword(coreUserPassword.Password, coreUser.HashPM, coreUser.SaltPM))
            return ApiResponse.Failure<CoreUserIV>(401, "Usuario o contraseña incorrecta.");

        return ApiResponse.Success(
            new CoreUserIV { IV = coreUser.SaltPM },
            "Autenticación exitosa.");
    }
}