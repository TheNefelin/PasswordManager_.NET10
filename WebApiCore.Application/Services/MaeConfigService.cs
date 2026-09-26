using System.Security.Cryptography;
using System.Text;
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

    public async Task<bool> ValidateApiKey(string apiKey, CancellationToken cancellationToken)
    {
        // Sin caché a propósito: rotar la ApiKey en Mae_Config surte efecto en
        // el siguiente request. Con una caché de 30 s, una ApiKey filtrada
        // seguía siendo válida durante la ventana de expiración, y el costo
        // de esta consulta (una fila por PK) es despreciable.
        var sqlApiKey = await _maeConfigRepository.GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(sqlApiKey))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(apiKey),
            Encoding.UTF8.GetBytes(sqlApiKey));
    }
}
