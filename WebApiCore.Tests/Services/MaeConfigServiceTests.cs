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

    private sealed class StubMaeConfigRepository : IMaeConfigRepository
    {
        private readonly string? _apiKey;

        public StubMaeConfigRepository(string? apiKey)
        {
            _apiKey = apiKey;
        }

        public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken) => Task.FromResult(_apiKey);
    }
}