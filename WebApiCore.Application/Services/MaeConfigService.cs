using System.Security.Cryptography;
using System.Text;
using WebApiCore.Application.Interfaces;
using WebApiCore.Domain.Interfaces;

namespace WebApiCore.Application.Services;

public class MaeConfigService : IMaeConfigService
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromSeconds(30);

    private readonly IMaeConfigRepository _maeConfigRepository;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _cacheTtl;
    private readonly Lock _gate = new();
    private string? _cachedApiKey;
    private DateTimeOffset _cacheExpiresAt;

    public MaeConfigService(
        IMaeConfigRepository maeConfigRepository,
        TimeSpan? cacheTtl = null,
        TimeProvider? timeProvider = null)
    {
        _maeConfigRepository = maeConfigRepository;
        _cacheTtl = cacheTtl ?? DefaultCacheTtl;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<bool> ValidateApiKey(string apiKey, CancellationToken cancellationToken)
    {
        var sqlApiKey = await GetCachedApiKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(sqlApiKey))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(apiKey),
            Encoding.UTF8.GetBytes(sqlApiKey));
    }

    private async Task<string?> GetCachedApiKeyAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_cachedApiKey is not null && _cacheExpiresAt > _timeProvider.GetUtcNow())
                return _cachedApiKey;
        }

        var apiKey = await _maeConfigRepository.GetApiKeyAsync(cancellationToken);

        if (apiKey is not null)
        {
            lock (_gate)
            {
                _cachedApiKey = apiKey;
                _cacheExpiresAt = _timeProvider.GetUtcNow().Add(_cacheTtl);
            }
        }

        return apiKey;
    }
}