using System.Collections.Concurrent;
using WebApiCore.Application.Interfaces;

namespace WebApiCore.Infrastructure.Security;

public class IpLockoutService : IIpLockoutService
{
    private readonly IpLockoutOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, LockoutEntry> _entries = new();

    public IpLockoutService(IpLockoutOptions options, TimeProvider? timeProvider = null)
    {
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsBlocked(string ipAddress)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entry = _entries.GetValueOrDefault(ipAddress);
        if (entry is null)
            return false;

        if (entry.BlockedUntil is DateTime blockedUntil && blockedUntil > now)
            return true;

        if (entry.BlockedUntil is DateTime expired && expired <= now)
        {
            _entries.TryRemove(ipAddress, out _);
            return false;
        }

        if (now - entry.LastFailureUtc > _options.FailureWindow)
        {
            _entries.TryRemove(ipAddress, out _);
            return false;
        }

        return false;
    }

    public void RegisterFailure(string ipAddress)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entry = _entries.GetOrAdd(ipAddress, _ => new LockoutEntry());

        lock (entry)
        {
            if (now - entry.LastFailureUtc > _options.FailureWindow)
            {
                entry.FailureCount = 0;
                entry.LastFailureUtc = now;
            }

            entry.FailureCount++;
            entry.LastFailureUtc = now;

            if (entry.FailureCount >= _options.MaxFailures)
                entry.BlockedUntil = now.Add(_options.BlockDuration);
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
            var remaining = blockedUntil - _timeProvider.GetUtcNow().UtcDateTime;
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