using WebApiCore.Application.DTOs;

namespace WebApiCore.Application.Interfaces;

public interface ICoreUserService
{
    Task<CoreUserIV> RegisterCoreUserPasswordAsync(Guid userId, CoreUserPasswordCreate coreUserRequest, CancellationToken cancellationToken);
    Task<CoreUserIV> GetCoreUserIVAsync(Guid userId, CoreUserPassword coreUserRequest, CancellationToken cancellationToken);
    Task<CoreUserChangeResponse> ChangeCorePasswordAsync(Guid userId, ChangeCorePasswordRequest request, CancellationToken cancellationToken);
}