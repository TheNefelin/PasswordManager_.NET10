namespace WebApiCore.Infrastructure.Security;

public sealed class IpLockoutOptions
{
    public required int MaxFailures { get; init; }
    public required TimeSpan FailureWindow { get; init; }
    public required TimeSpan BlockDuration { get; init; }
}