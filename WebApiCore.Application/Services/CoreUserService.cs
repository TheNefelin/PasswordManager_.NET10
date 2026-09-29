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

    public async Task<CoreUserIV> RegisterCoreUserPasswordAsync(Guid userId, CoreUserPasswordCreate coreUserPassword, CancellationToken cancellationToken)
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

    public async Task<CoreUserChangeResponse> ChangeCorePasswordAsync(Guid userId, ChangeCorePasswordRequest request, CancellationToken cancellationToken)
    {
        var coreUser = await _coreUserRepository.GetCoreUserAsync(
            new CoreUser
            {
                User_Id = userId,
                SqlToken = request.CoreUser.SqlToken
            },
            cancellationToken);

        if (coreUser == null)
            throw new UserSessionInvalidException();

        if (string.IsNullOrEmpty(coreUser.HashPM) || string.IsNullOrEmpty(coreUser.SaltPM))
            throw new CorePasswordNotConfiguredException();

        if (!_passwordHasher.VerifyPassword(request.OldPassword, coreUser.HashPM, coreUser.SaltPM))
            throw new InvalidCredentialsException();

        // La sal/IV de 16 bytes la genera el cliente (dueño del cifrado) para poder
        // re-cifrar antes del cambio; acá solo se valida que sea material válido
        // antes de usarla en el hash PBKDF2.
        ValidNewSalt(request.Salt);
        var newHash = _passwordHasher.HashPassword(request.NewPassword, request.Salt);
        var newSqlToken = Guid.NewGuid();

        // La atomicidad (swap de clave + reemplazo de datos, todo o nada) vive
        // en una única transacción del repositorio: es el contrato que evita
        // perder datos si el request falla a mitad.
        await _coreUserRepository.ChangeCorePasswordAsync(
            userId,
            newHash,
            request.Salt,
            newSqlToken,
            request.Records.Select(ToEntity),
            cancellationToken);

        return new CoreUserChangeResponse { IV = request.Salt, SqlToken = newSqlToken };
    }

    private static void ValidNewSalt(string salt)
    {
        if (string.IsNullOrEmpty(salt))
            throw new RequestValidationException("La nueva sal es obligatoria.");

        try
        {
            byte[] bytes = Convert.FromBase64String(salt);
            if (bytes.Length != SaltSize)
                throw new RequestValidationException("La nueva sal debe ser de 16 bytes.");
        }
        catch (FormatException)
        {
            throw new RequestValidationException("La nueva sal no es base64 válido.");
        }
    }

    private const int SaltSize = 16;

    private static CoreData ToEntity(CoreDataReplacement replacement)
    {
        return new CoreData
        {
            Data_Id = replacement.Data_Id,
            Data01 = replacement.Data01,
            Data02 = replacement.Data02,
            Data03 = replacement.Data03
        };
    }
}