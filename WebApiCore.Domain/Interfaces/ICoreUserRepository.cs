using WebApiCore.Domain.Entities;

namespace WebApiCore.Domain.Interfaces;

public interface ICoreUserRepository
{
    Task<CoreUser?> GetCoreUserAsync(CoreUser coreUser, CancellationToken cancellationToken);
    Task RegisterCoreUserPasswordAsync(CoreUser coreUser, CancellationToken cancellationToken);
}