namespace WebApiCore.Application.Interfaces;

public interface IIpLockoutService
{
    bool IsBlocked(string ipAddress);
    void RegisterFailure(string ipAddress);
    void Reset(string ipAddress);
    TimeSpan? GetRemainingBlockTime(string ipAddress);
}