using Dapper;
using Microsoft.Data.SqlClient;
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

    public async Task<UserCreationStatus> CreateUserAsync(AuthUser authUser, CancellationToken cancellationToken)
    {
        var commandDefinition = new CommandDefinition(
            commandText: @"
                INSERT INTO Auth_Users
                    (User_Id, Email, HashLogin, SaltLogin, Profile_Id)
                VALUES
                    (@User_Id, @Email, @HashLogin, @SaltLogin, 2)",
            parameters: new
            {
                authUser.User_Id,
                authUser.Email,
                authUser.HashLogin,
                authUser.SaltLogin
            },
            cancellationToken: cancellationToken);

        using var connection = _dapper.CreateConnection();

        try
        {
            await connection.ExecuteAsync(commandDefinition);
            return UserCreationStatus.Created;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return UserCreationStatus.EmailAlreadyExists;
        }
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