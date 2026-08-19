using Dapper;
using WebApiCore.Domain.Interfaces;
using WebApiCore.Infrastructure.Data;

namespace WebApiCore.Infrastructure.Repositories;

public class MaeConfigRepository : IMaeConfigRepository
{
    private readonly IDapperContext _dapper;

    public MaeConfigRepository(IDapperContext dapper)
    {
        _dapper = dapper;
    }

    public async Task<string?> GetApiKeyAsync()
    {
        var commandDefinition = new CommandDefinition(
            commandText: "SELECT ApiKey FROM Mae_Config WHERE Config_Id = @Config_Id",
            parameters: new { Config_Id = 1 });

        using var connection = _dapper.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<string>(commandDefinition);
    }
}