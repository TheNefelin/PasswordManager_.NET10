using Dapper;
using WebApiCore.Domain.Entities;
using WebApiCore.Domain.Interfaces;
using WebApiCore.Infrastructure.Data;
using WebApiCore.Infrastructure.Security;

namespace WebApiCore.Infrastructure.Repositories;

public class CoreUserRepository : ICoreUserRepository
{
    private readonly IDapperContext _dapper;

    public CoreUserRepository(IDapperContext dapper)
    {
        _dapper = dapper;
    }

    public async Task<CoreUser?> GetCoreUserAsync(CoreUser coreUser, CancellationToken cancellationToken)
    {
        var commandDefinition = new CommandDefinition(
            cancellationToken: cancellationToken,
            commandText: "SELECT User_Id, HashPM, SaltPM FROM Auth_Users WHERE User_Id = @User_Id AND SqlTokenHash = @SqlTokenHash",
            parameters: new { coreUser.User_Id, SqlTokenHash = SqlTokenHasher.Hash(coreUser.SqlToken) });

        using var connection = _dapper.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<CoreUser>(commandDefinition);
    }

    public async Task RegisterCoreUserPasswordAsync(CoreUser coreUser, CancellationToken cancellationToken)
    {
        // El WHERE va solo por User_Id a propósito: la sesión ya la validó el
        // servicio con GetCoreUserAsync, y el CoreUser que devuelve ese SELECT
        // ya no trae el token crudo (en BD solo queda su hash), por lo que
        // revalidar el hash aquí nunca coincidiría.
        var commandDefinition = new CommandDefinition(
            cancellationToken: cancellationToken,
            commandText: "UPDATE Auth_Users SET HashPM = @HashPM, SaltPM = @SaltPM WHERE User_Id = @User_Id",
            parameters: new
            {
                coreUser.User_Id,
                coreUser.HashPM,
                coreUser.SaltPM
            });

        using var connection = _dapper.CreateConnection();
        await connection.ExecuteAsync(commandDefinition);
    }

    public async Task ChangeCorePasswordAsync(Guid userId, string hash, string salt, Guid newSqlToken, IEnumerable<CoreData> replacementRecords, CancellationToken cancellationToken)
    {
        // Cambio de clave maestra: rotación de hash/sal/token + reemplazo de
        // los datos re-cifrados, todo en UNA transacción con UNA conexión.
        // Una conexión por transacción evita la promoción a transacción
        // distribuida (TransactionScope multi-conexión exige MSDTC, no
        // disponible en el runner de CI de Linux).
        using var connection = _dapper.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            new CommandDefinition(
                cancellationToken: cancellationToken,
                transaction: transaction,
                commandText: "UPDATE Auth_Users SET HashPM = @HashPM, SaltPM = @SaltPM, SqlTokenHash = @SqlTokenHash WHERE User_Id = @User_Id",
                parameters: new
                {
                    User_Id = userId,
                    HashPM = hash,
                    SaltPM = salt,
                    SqlTokenHash = SqlTokenHasher.Hash(newSqlToken)
                }));

        await connection.ExecuteAsync(
            new CommandDefinition(
                cancellationToken: cancellationToken,
                transaction: transaction,
                commandText: "DELETE FROM PM_CoreData WHERE User_Id = @User_Id",
                parameters: new { User_Id = userId }));

        foreach (var record in replacementRecords)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    cancellationToken: cancellationToken,
                    transaction: transaction,
                    commandText: "INSERT INTO PM_CoreData (Data_Id, Data01, Data02, Data03, User_Id) VALUES (@Data_Id, @Data01, @Data02, @Data03, @User_Id)",
                    parameters: new
                    {
                        record.Data_Id,
                        record.Data01,
                        record.Data02,
                        record.Data03,
                        User_Id = userId
                    }));
        }

        transaction.Commit();
    }
}