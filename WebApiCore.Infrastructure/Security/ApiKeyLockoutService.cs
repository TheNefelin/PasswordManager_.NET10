using System.Collections.Concurrent;
using WebApiCore.Application.Interfaces;

namespace WebApiCore.Infrastructure.Security;

public class ApiKeyLockoutService : IApiKeyLockoutService
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan BlockDuration = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, LockoutEntry> _entries = new();

    public bool IsBlocked(string ipAddress)
    {
        var entry = _entries.GetValueOrDefault(ipAddress);
        if (entry is null)
            return false;

        if (entry.BlockedUntil is DateTime blockedUntil && blockedUntil > DateTime.UtcNow)
            return true;

        if (entry.BlockedUntil is DateTime expired && expired <= DateTime.UtcNow)
        {
            _entries.TryRemove(ipAddress, out _);
            return false;
        }

        if (DateTime.UtcNow - entry.LastFailureUtc > FailureWindow)
        {
            _entries.TryRemove(ipAddress, out _);
            return false;
        }

        return false;
    }

    public void RegisterFailure(string ipAddress)
    {
        var now = DateTime.UtcNow;
        var entry = _entries.GetOrAdd(ipAddress, _ => new LockoutEntry());

        lock (entry)
        {
            if (now - entry.LastFailureUtc > FailureWindow)
            {
                entry.FailureCount = 0;
                entry.LastFailureUtc = now;
            }

            entry.FailureCount++;
            entry.LastFailureUtc = now;

            if (entry.FailureCount >= MaxFailures)
                entry.BlockedUntil = now.Add(BlockDuration);
        }
    }

    public void Reset(string ipAddress)
    {
        _entries.TryRemove(ipAddress, out _);
    }

    public TimeSpan? GetRemainingBlockTime(string ipAddress)
    {
        var entry = _entries.GetValueOrDefault(ipAddress);
        if (entry?.BlockedUntil is DateTime blockedUntil)
        {
            var remaining = blockedUntil - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : null;
        }

        return null;
    }

    private sealed class LockoutEntry
    {
        public int FailureCount;
        public DateTime LastFailureUtc;
        public DateTime? BlockedUntil;
    }
}