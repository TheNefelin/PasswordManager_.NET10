using Dapper;
using System.Data;
using WebApiCore.Domain.Entities;
using WebApiCore.Domain.Interfaces;
using WebApiCore.Domain.Models;
using WebApiCore.Infrastructure.Data;

namespace WebApiCore.Infrastructure.Repositories;

public class AuthUserRepository : IAuthUserRepository
{
    private readonly IDapperContext _dapper;

    public AuthUserRepository(IDapperContext dapper)
    {
        _dapper = dapper;
    }

    public async Task<SqlResponse?> CreateUserAsync(AuthUser authUser, CancellationToken cancellationToken)
    {
        var commandDefinition = new CommandDefinition(
            commandType: CommandType.StoredProcedure,
            commandText: "Auth_Register",
            parameters: new
            {
                authUser.User_Id,
                authUser.Email,
                authUser.HashLogin,
                authUser.SaltLogin
            },
            transaction: default,
            cancellationToken: cancellationToken);

        using var connection = _dapper.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<SqlResponse>(commandDefinition);
    }

    public async Task<AuthUser?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var commandDefinition = new CommandDefinition(
            commandText: @"
                SELECT
                    a.User_Id,
                    a.Email,
                    a.HashLogin,
                    a.SaltLogin,
                    a.HashPM,
                    a.SaltPM,
                    a.SqlToken,
                    b.Name AS Role
                FROM Auth_Users a
                    INNER JOIN Auth_Profiles b ON a.Profile_Id = b.Profile_Id
                WHERE a.Email = @Email",
            parameters: new { Email = email },
            cancellationToken: cancellationToken);

        using var connection = _dapper.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<AuthUser>(commandDefinition);
    }

    public async Task<Guid> NewSqlToken(string email, CancellationToken cancellationToken)
    {
        var commandDefinition = new CommandDefinition(
            commandText: "UPDATE Auth_Users SET SqlToken = NEWID() OUTPUT inserted.SqlToken WHERE Email = @Email",
            parameters: new { Email = email },
            cancellationToken: cancellationToken);

        using var connection = _dapper.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<Guid>(commandDefinition);
    }
}