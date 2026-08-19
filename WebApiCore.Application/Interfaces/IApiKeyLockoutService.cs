namespace WebApiCore.Application.Interfaces;

public interface IApiKeyLockoutService
{
    bool IsBlocked(string ipAddress);
    void RegisterFailure(string ipAddress);
    void Reset(string ipAddress);
    TimeSpan? GetRemainingBlockTime(string ipAddress);
}