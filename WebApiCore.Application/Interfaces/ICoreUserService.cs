using WebApiCore.Application.DTOs;

namespace WebApiCore.Application.Interfaces;

public interface ICoreUserService
{
    Task<CoreUserIV> RegisterCoreUserPasswordAsync(Guid userId, CoreUserPassword coreUserRequest, CancellationToken cancellationToken);
    Task<CoreUserIV> GetCoreUserIVAsync(Guid userId, CoreUserPassword coreUserRequest, CancellationToken cancellationToken);
}