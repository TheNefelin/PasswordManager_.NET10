using WebApiCore.Domain.Entities;

namespace WebApiCore.Application.Interfaces;

public record TokenResult(string Token, int ExpireMin);

public interface IAuthTokenService
{
    TokenResult GenerateToken(AuthUser user);
}