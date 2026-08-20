using WebApiCore.Application.Services;
using WebApiCore.Domain.Interfaces;

namespace WebApiCore.Tests.Services;

public class MaeConfigServiceTests
{
    [Fact]
    public async Task ValidateApiKey_WithMatchingKey_ReturnsTrue()
    {
        var service = new MaeConfigService(new StubMaeConfigRepository("Testing-777"));

        Assert.True(await service.ValidateApiKey("Testing-777", CancellationToken.None));
    }

    [Fact]
    public async Task ValidateApiKey_WithDifferentKey_ReturnsFalse()
    {
        var service = new MaeConfigService(new StubMaeConfigRepository("Testing-777"));

        Assert.False(await service.ValidateApiKey("WrongKey", CancellationToken.None));
    }

    [Fact]
    public async Task ValidateApiKey_WhenStoredKeyIsNullOrEmpty_ReturnsFalse()
    {
        Assert.False(await new MaeConfigService(new StubMaeConfigRepository(null)).ValidateApiKey("Testing-777", CancellationToken.None));
        Assert.False(await new MaeConfigService(new StubMaeConfigRepository(string.Empty)).ValidateApiKey("Testing-777", CancellationToken.None));
    }

    [Fact]
    public async Task ValidateApiKey_CachesApiKey_WithinTtl()
    {
        var repository = new CountingStubMaeConfigRepository("Testing-777");
        var timeProvider = new StubTimeProvider();
        var service = new MaeConfigService(repository, TimeSpan.FromSeconds(30), timeProvider);

        await service.ValidateApiKey("Testing-777", CancellationToken.None);
        await service.ValidateApiKey("Testing-777", CancellationToken.None);

        Assert.Equal(1, repository.GetApiKeyCalls);
    }

    [Fact]
    public async Task ValidateApiKey_AfterTtlExpiry_RefreshesFromRepository()
    {
        var repository = new CountingStubMaeConfigRepository("Testing-777");
        var timeProvider = new StubTimeProvider();
        var service = new MaeConfigService(repository, TimeSpan.FromSeconds(30), timeProvider);

        await service.ValidateApiKey("Testing-777", CancellationToken.None);
        timeProvider.UtcNow = timeProvider.UtcNow.AddSeconds(31);
        await service.ValidateApiKey("Testing-777", CancellationToken.None);

        Assert.Equal(2, repository.GetApiKeyCalls);
    }

    [Fact]
    public async Task ValidateApiKey_WhenCacheMissesAndKeyChanges_RefreshesApiKey()
    {
        var repository = new MutableStubMaeConfigRepository("Testing-777");
        var timeProvider = new StubTimeProvider();
        var service = new MaeConfigService(repository, TimeSpan.FromSeconds(30), timeProvider);

        Assert.True(await service.ValidateApiKey("Testing-777", CancellationToken.None));

        repository.ApiKey = "New-Key";
        timeProvider.UtcNow = timeProvider.UtcNow.AddSeconds(31);
        Assert.False(await service.ValidateApiKey("Testing-777", CancellationToken.None));
        Assert.True(await service.ValidateApiKey("New-Key", CancellationToken.None));
    }

    private sealed class StubMaeConfigRepository : IMaeConfigRepository
    {
        private readonly string? _apiKey;

        public StubMaeConfigRepository(string? apiKey)
        {
            _apiKey = apiKey;
        }

        public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken) => Task.FromResult(_apiKey);
    }

    private sealed class CountingStubMaeConfigRepository : IMaeConfigRepository
    {
        private readonly string? _apiKey;

        public CountingStubMaeConfigRepository(string? apiKey)
        {
            _apiKey = apiKey;
        }

        public int GetApiKeyCalls { get; private set; }

        public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken)
        {
            GetApiKeyCalls++;
            return Task.FromResult(_apiKey);
        }
    }

    private sealed class MutableStubMaeConfigRepository : IMaeConfigRepository
    {
        public MutableStubMaeConfigRepository(string? apiKey)
        {
            ApiKey = apiKey;
        }

        public string? ApiKey { get; set; }

        public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken) => Task.FromResult(ApiKey);
    }

    private sealed class StubTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}