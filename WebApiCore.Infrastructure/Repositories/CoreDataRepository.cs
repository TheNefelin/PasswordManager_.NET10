using Dapper;
using WebApiCore.Domain.Entities;
using WebApiCore.Domain.Interfaces;
using WebApiCore.Infrastructure.Data;

namespace WebApiCore.Infrastructure.Repositories;

public class CoreDataRepository : ICoreDataRepository
{
    private readonly IDapperContext _dapper;

    public CoreDataRepository(IDapperContext dapper)
    {
        _dapper = dapper;
    }

    public async Task<IEnumerable<CoreData>> GetAllAsync(CoreData coreData, CancellationToken cancellationToken)
    {
        var commandDefinition = new CommandDefinition(
            cancellationToken: cancellationToken,
            commandText: "SELECT Data_Id, Data01, Data02, Data03, User_Id FROM PM_CoreData WHERE User_Id = @User_Id",
            parameters: new { coreData.User_Id });

        using var connection = _dapper.CreateConnection();
        return await connection.QueryAsync<CoreData>(commandDefinition);
    }

    public async Task<CoreData> InsertAsync(CoreData coreData, CancellationToken cancellationToken)
    {
        var commandDefinition = new CommandDefinition(
            cancellationToken: cancellationToken,
            commandText: "INSERT INTO PM_CoreData (Data01, Data02, Data03, User_Id) OUTPUT inserted.Data_Id VALUES (@Data01, @Data02, @Data03, @User_Id)",
            parameters: new
            {
                coreData.Data01,
                coreData.Data02,
                coreData.Data03,
                coreData.User_Id
            });

        using var connection = _dapper.CreateConnection();
        coreData.Data_Id = await connection.QueryFirstAsync<Guid>(commandDefinition);

        return coreData;
    }

    public async Task<CoreData> UpdateAsync(CoreData coreData, CancellationToken cancellationToken)
    {
        var commandDefinition = new CommandDefinition(
            cancellationToken: cancellationToken,
            commandText: "UPDATE PM_CoreData SET Data01 = @Data01, Data02 = @Data02, Data03 = @Data03 WHERE Data_Id = @Data_Id AND User_Id = @User_Id",
            parameters: new
            {
                coreData.Data_Id,
                coreData.Data01,
                coreData.Data02,
                coreData.Data03,
                coreData.User_Id
            });

        using var connection = _dapper.CreateConnection();
        await connection.ExecuteAsync(commandDefinition);

        return coreData;
    }

    public async Task DeleteAsync(CoreData coreData, CancellationToken cancellationToken)
    {
        var commandDefinition = new CommandDefinition(
            cancellationToken: cancellationToken,
            commandText: "DELETE FROM PM_CoreData WHERE Data_Id = @Data_Id AND User_Id = @User_Id",
            parameters: new
            {
                coreData.Data_Id,
                coreData.User_Id
            });

        using var connection = _dapper.CreateConnection();
        await connection.ExecuteAsync(commandDefinition);
    }
}