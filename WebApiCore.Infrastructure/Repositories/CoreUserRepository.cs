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
}