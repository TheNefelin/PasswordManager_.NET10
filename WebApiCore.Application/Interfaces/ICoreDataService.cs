using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;

namespace WebApiCore.Application.Interfaces;

public interface ICoreDataService
{
    Task<ApiResponse<IEnumerable<CoreDataResponse>>> GetAllAsync(CoreUserRequest coreUser, CancellationToken cancellationToken);
    Task<ApiResponse<CoreDataResponse>> InsertAsync(CoreDataRequest coreData, CancellationToken cancellationToken);
    Task<ApiResponse<CoreDataResponse>> UpdateAsync(CoreDataRequest coreData, CancellationToken cancellationToken);
    Task<ApiResponse<object>> DeleteAsync(CoreDataDelete coreDataDelete, CancellationToken cancellationToken);
}