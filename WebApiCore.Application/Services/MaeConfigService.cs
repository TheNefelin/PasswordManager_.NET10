using WebApiCore.Application.Interfaces;
using WebApiCore.Domain.Interfaces;

namespace WebApiCore.Application.Services;

public class MaeConfigService : IMaeConfigService
{
    private readonly IMaeConfigRepository _maeConfigRepository;

    public MaeConfigService(IMaeConfigRepository maeConfigRepository)
    {
        _maeConfigRepository = maeConfigRepository;
    }

    public async Task<bool> ValidateApiKey(string apiKey)
    {
        var sqlApiKey = await _maeConfigRepository.GetApiKeyAsync();
        return !string.IsNullOrEmpty(sqlApiKey) && apiKey.Equals(sqlApiKey);
    }
}