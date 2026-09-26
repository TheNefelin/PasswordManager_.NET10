using WebApiCore.Application.DTOs;

namespace WebApiCore.Application.Interfaces;

public interface ICoreDataService
{
    Task<IEnumerable<CoreDataResponse>> GetAllAsync(Guid userId, Guid sqlToken, CancellationToken cancellationToken);
    Task<CoreDataResponse> InsertAsync(Guid userId, CoreDataRequest coreData, CancellationToken cancellationToken);
    Task<CoreDataResponse> UpdateAsync(Guid userId, CoreDataRequest coreData, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, CoreDataDelete coreDataDelete, CancellationToken cancellationToken);
}
