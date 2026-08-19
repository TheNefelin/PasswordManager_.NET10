using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;

namespace WebApiCore.Application.Interfaces;

public interface IAuthUserService
{
    Task<ApiResponse<AuthUserResponse>> RegisterAsync(AuthUserRegister authUserRegister, CancellationToken cancellationToken);
    Task<ApiResponse<AuthUserLogged>> LoginAsync(AuthUserLogin authUserLogin, CancellationToken cancellationToken);
}