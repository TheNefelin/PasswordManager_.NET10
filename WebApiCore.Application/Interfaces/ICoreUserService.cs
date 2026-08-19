using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;

namespace WebApiCore.Application.Interfaces;

public interface ICoreUserService
{
    Task<ApiResponse<CoreUserIV>> RegisterCoreUserPasswordAsync(CoreUserPassword coreUserRequest, CancellationToken cancellationToken);
    Task<ApiResponse<CoreUserIV>> GetCoreUserIVAsync(CoreUserPassword coreUserRequest, CancellationToken cancellationToken);
}