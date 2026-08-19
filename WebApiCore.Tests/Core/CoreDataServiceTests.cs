using WebApiCore.Application.DTOs;
using WebApiCore.Application.Services;
using WebApiCore.Infrastructure.Repositories;
using WebApiCore.Tests.Helpers;

namespace WebApiCore.Tests.Core;

public class CoreDataServiceTests : IntegrationTestBase
{
    private static CoreDataService CreateService() => new(
        new CoreDataRepository(TestDb.CreateContext()),
        new CoreUserRepository(TestDb.CreateContext()));

    [Fact]
    public async Task InsertThenGetAll_WithValidSession()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        var insertResult = await service.InsertAsync(new CoreDataRequest
        {
            Data01 = "a",
            Data02 = "b",
            Data03 = "c",
            CoreUser = coreUser
        }, CancellationToken.None);

        Assert.True(insertResult.IsSuccess);
        Assert.Equal(201, insertResult.StatusCode);

        var getAllResult = await service.GetAllAsync(coreUser, CancellationToken.None);

        Assert.True(getAllResult.IsSuccess);
        Assert.Contains(getAllResult.Data!, x => x.Data_Id == insertResult.Data!.Data_Id);
    }

    [Fact]
    public async Task GetAllAsync_WithInvalidSession_ReturnsUnauthorized()
    {
        var service = CreateService();

        var result = await service.GetAllAsync(
            new CoreUserRequest { User_Id = Guid.NewGuid(), SqlToken = Guid.NewGuid() },
            CancellationToken.None);

        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task UpdateAsync_WithValidSession_UpdatesData()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        var insertResult = await service.InsertAsync(new CoreDataRequest
        {
            Data01 = "a",
            Data02 = "b",
            Data03 = "c",
            CoreUser = coreUser
        }, CancellationToken.None);

        var updateResult = await service.UpdateAsync(new CoreDataRequest
        {
            Data_Id = insertResult.Data!.Data_Id,
            Data01 = "x",
            Data02 = "y",
            Data03 = "z",
            CoreUser = coreUser
        }, CancellationToken.None);

        Assert.True(updateResult.IsSuccess);

        var getAllResult = await service.GetAllAsync(coreUser, CancellationToken.None);
        Assert.Contains(getAllResult.Data!, x => x.Data_Id == insertResult.Data.Data_Id && x.Data01 == "x");
    }

    [Fact]
    public async Task DeleteAsync_WithValidSession_DeletesData()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        var insertResult = await service.InsertAsync(new CoreDataRequest
        {
            Data01 = "a",
            Data02 = "b",
            Data03 = "c",
            CoreUser = coreUser
        }, CancellationToken.None);

        var deleteResult = await service.DeleteAsync(new CoreDataDelete
        {
            Data_Id = insertResult.Data!.Data_Id,
            CoreUser = coreUser
        }, CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);

        var getAllResult = await service.GetAllAsync(coreUser, CancellationToken.None);
        Assert.DoesNotContain(getAllResult.Data!, x => x.Data_Id == insertResult.Data.Data_Id);
    }
}