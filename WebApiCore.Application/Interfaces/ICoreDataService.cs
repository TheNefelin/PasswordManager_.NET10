using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Domain.Entities;

namespace WebApiCore.Application.Interfaces;

public interface ICoreDataService
{
    Task<ApiResponse<IEnumerable<CoreData>>> GetAllAsync(CoreUserRequest coreUser, CancellationToken cancellationToken);
    Task<ApiResponse<CoreData>> InsertAsync(CoreDataRequest coreData, CancellationToken cancellationToken);
    Task<ApiResponse<CoreData>> UpdateAsync(CoreDataRequest coreData, CancellationToken cancellationToken);
    Task<ApiResponse<object>> DeleteAsync(CoreDataDelete coreDataDelete, CancellationToken cancellationToken);
}