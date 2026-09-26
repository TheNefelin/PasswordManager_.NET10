using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Interfaces;
using WebApiCore.Domain.Entities;
using WebApiCore.Domain.Interfaces;
using WebApiCore.Domain.Models;

namespace WebApiCore.Application.Services;

public class AuthUserService : IAuthUserService
{
    private readonly IAuthUserRepository _authUserRepository;
    private readonly IMaeConfigRepository _maeConfigRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthTokenService _authTokenService;
    private readonly IIpLockoutService _loginLockoutService;

    public AuthUserService(
        IAuthUserRepository authUserRepository,
        IMaeConfigRepository maeConfigRepository,
        IPasswordHasher passwordHasher,
        IAuthTokenService authTokenService,
        IIpLockoutService loginLockoutService)
    {
        _authUserRepository = authUserRepository;
        _maeConfigRepository = maeConfigRepository;
        _passwordHasher = passwordHasher;
        _authTokenService = authTokenService;
        _loginLockoutService = loginLockoutService;
    }

    public async Task<AuthUserResponse> RegisterAsync(AuthUserRegister authUserRegister, CancellationToken cancellationToken)
    {
        if (!authUserRegister.Password1.Equals(authUserRegister.Password2))
            throw new RequestValidationException("Las contraseñas no coinciden.");

        if (!await _maeConfigRepository.IsRegistrationEnabledAsync(cancellationToken))
            throw new RegistrationDisabledException();

        var (hash, salt) = _passwordHasher.HashPassword(authUserRegister.Password1);
        var authUser = new AuthUser
        {
            User_Id = Guid.NewGuid(),
            Email = authUserRegister.Email,
            HashLogin = hash,
            SaltLogin = salt
        };

        var result = await _authUserRepository.CreateUserAsync(authUser, cancellationToken);
        if (result == UserCreationStatus.EmailAlreadyExists)
            throw new DuplicateEmailException();

        return new AuthUserResponse { User_Id = authUser.User_Id };
    }

    public async Task<AuthUserLogged> LoginAsync(AuthUserLogin authUserLogin, string ipAddress, CancellationToken cancellationToken)
    {
        if (_loginLockoutService.IsBlocked(ipAddress))
            throw new TooManyLoginAttemptsException();

        var authUser = await _authUserRepository.GetUserByEmailAsync(authUserLogin.Email, cancellationToken);

        if (authUser == null)
        {
            _loginLockoutService.RegisterFailure(ipAddress);
            throw new InvalidCredentialsException();
        }

        if (!_passwordHasher.VerifyPassword(authUserLogin.Password, authUser.HashLogin, authUser.SaltLogin))
        {
            _loginLockoutService.RegisterFailure(ipAddress);
            throw new InvalidCredentialsException();
        }

        _loginLockoutService.Reset(ipAddress);

        var sqlToken = await _authUserRepository.NewSqlToken(authUser.Email, cancellationToken);
        var token = _authTokenService.GenerateToken(authUser);

        return new AuthUserLogged
        {
            User_Id = authUser.User_Id,
            SqlToken = sqlToken,
            Role = authUser.Role ?? "USER",
            ExpireMin = token.ExpireMin.ToString(),
            ApiToken = token.Token
        };
    }
}