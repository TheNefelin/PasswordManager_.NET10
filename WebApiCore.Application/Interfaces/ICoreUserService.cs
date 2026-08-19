using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;

namespace WebApiCore.Application.Interfaces;

public interface ICoreUserService
{
    Task<ApiResponse<CoreUserIV>> RegisterCoreUserPasswordAsync(Guid userId, CoreUserPassword coreUserRequest, CancellationToken cancellationToken);
    Task<ApiResponse<CoreUserIV>> GetCoreUserIVAsync(Guid userId, CoreUserPassword coreUserRequest, CancellationToken cancellationToken);
}