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

    public async Task<CoreUserIV> RegisterCoreUserPasswordAsync(Guid userId, CoreUserPassword coreUserPassword, CancellationToken cancellationToken)
    {
        var coreUser = await _coreUserRepository.GetCoreUserAsync(
            new CoreUser
            {
                User_Id = userId,
                SqlToken = coreUserPassword.CoreUser.SqlToken
            },
            cancellationToken);

        if (coreUser == null)
            throw new UserSessionInvalidException();

        if (!string.IsNullOrEmpty(coreUser.HashPM) && !string.IsNullOrEmpty(coreUser.SaltPM))
            throw new CorePasswordAlreadyExistsException();

        var (hash, salt) = _passwordHasher.HashPassword(coreUserPassword.Password);
        coreUser.HashPM = hash;
        coreUser.SaltPM = salt;

        await _coreUserRepository.RegisterCoreUserPasswordAsync(coreUser, cancellationToken);

        return new CoreUserIV { IV = salt };
    }

    public async Task<CoreUserIV> GetCoreUserIVAsync(Guid userId, CoreUserPassword coreUserPassword, CancellationToken cancellationToken)
    {
        var coreUser = await _coreUserRepository.GetCoreUserAsync(
            new CoreUser
            {
                User_Id = userId,
                SqlToken = coreUserPassword.CoreUser.SqlToken
            },
            cancellationToken);

        if (coreUser == null)
            throw new UserSessionInvalidException();

        if (string.IsNullOrEmpty(coreUser.HashPM) || string.IsNullOrEmpty(coreUser.SaltPM))
            throw new CorePasswordNotConfiguredException();

        if (!_passwordHasher.VerifyPassword(coreUserPassword.Password, coreUser.HashPM, coreUser.SaltPM))
            throw new InvalidCredentialsException();

        return new CoreUserIV { IV = coreUser.SaltPM };
    }
}