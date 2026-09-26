using WebApiCore.Domain.Entities;

namespace WebApiCore.Domain.Interfaces;

public interface ICoreDataRepository
{
    Task<IEnumerable<CoreData>> GetAllAsync(CoreData coreData, CancellationToken cancellationToken);
    Task<CoreData> InsertAsync(CoreData coreData, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(CoreData coreData, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(CoreData coreData, CancellationToken cancellationToken);
}