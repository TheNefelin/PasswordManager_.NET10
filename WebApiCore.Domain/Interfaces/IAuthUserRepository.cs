using WebApiCore.Domain.Entities;
using WebApiCore.Domain.Models;

namespace WebApiCore.Domain.Interfaces;

public interface IAuthUserRepository
{
    Task<SqlResponse?> CreateUserAsync(AuthUser authUser, CancellationToken cancellationToken);
    Task<AuthUser?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);
    Task<Guid> NewSqlToken(string email, CancellationToken cancellationToken);
}