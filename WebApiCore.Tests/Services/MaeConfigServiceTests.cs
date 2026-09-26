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
    public async Task ValidateApiKey_AfterKeyRotation_TakesEffectImmediately()
    {
        // Sin caché: rotar la ApiKey en Mae_Config debe surtir efecto en el
        // siguiente request, sin esperar ninguna ventana de expiración.
        var repository = new MutableStubMaeConfigRepository("Testing-777");
        var service = new MaeConfigService(repository);

        Assert.True(await service.ValidateApiKey("Testing-777", CancellationToken.None));

        repository.ApiKey = "New-Key";

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

        public Task<bool> IsRegistrationEnabledAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class MutableStubMaeConfigRepository : IMaeConfigRepository
    {
        public MutableStubMaeConfigRepository(string? apiKey)
        {
            ApiKey = apiKey;
        }

        public string? ApiKey { get; set; }

        public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken) => Task.FromResult(ApiKey);

        public Task<bool> IsRegistrationEnabledAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }
}