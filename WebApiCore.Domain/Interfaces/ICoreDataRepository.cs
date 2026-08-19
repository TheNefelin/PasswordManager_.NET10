using WebApiCore.Domain.Entities;

namespace WebApiCore.Domain.Interfaces;

public interface ICoreDataRepository
{
    Task<IEnumerable<CoreData>> GetAllAsync(CoreData coreData, CancellationToken cancellationToken);
    Task<CoreData> InsertAsync(CoreData coreData, CancellationToken cancellationToken);
    Task<CoreData> UpdateAsync(CoreData coreData, CancellationToken cancellationToken);
    Task DeleteAsync(CoreData coreData, CancellationToken cancellationToken);
}