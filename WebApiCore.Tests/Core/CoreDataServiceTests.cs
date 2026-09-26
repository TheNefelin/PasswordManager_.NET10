using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Services;
using WebApiCore.Infrastructure.Repositories;
using WebApiCore.Tests.Helpers;

namespace WebApiCore.Tests.Core;

[Collection("Database")]
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

        var insertResult = await service.InsertAsync(userId, new CoreDataRequest
        {
            Data01 = "a",
            Data02 = "b",
            Data03 = "c",
            CoreUser = coreUser
        }, CancellationToken.None);

        var getAllResult = await service.GetAllAsync(userId, coreUser, CancellationToken.None);

        Assert.Contains(getAllResult, x => x.Data_Id == insertResult.Data_Id);
    }

    [Fact]
    public async Task GetAllAsync_WithInvalidSession_ThrowsUserSessionInvalidException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<UserSessionInvalidException>(() => service.GetAllAsync(
            Guid.NewGuid(),
            new CoreUserRequest { User_Id = Guid.NewGuid(), SqlToken = Guid.NewGuid() },
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WithValidSession_UpdatesData()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        var insertResult = await service.InsertAsync(userId, new CoreDataRequest
        {
            Data01 = "a",
            Data02 = "b",
            Data03 = "c",
            CoreUser = coreUser
        }, CancellationToken.None);

        var updateResult = await service.UpdateAsync(userId, new CoreDataRequest
        {
            Data_Id = insertResult.Data_Id,
            Data01 = "x",
            Data02 = "y",
            Data03 = "z",
            CoreUser = coreUser
        }, CancellationToken.None);

        Assert.Equal(insertResult.Data_Id, updateResult.Data_Id);

        var getAllResult = await service.GetAllAsync(userId, coreUser, CancellationToken.None);
        Assert.Contains(getAllResult, x => x.Data_Id == insertResult.Data_Id && x.Data01 == "x");
    }

    [Fact]
    public async Task DeleteAsync_WithValidSession_DeletesData()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        var insertResult = await service.InsertAsync(userId, new CoreDataRequest
        {
            Data01 = "a",
            Data02 = "b",
            Data03 = "c",
            CoreUser = coreUser
        }, CancellationToken.None);

        await service.DeleteAsync(userId, new CoreDataDelete
        {
            Data_Id = insertResult.Data_Id,
            CoreUser = coreUser
        }, CancellationToken.None);

        var getAllResult = await service.GetAllAsync(userId, coreUser, CancellationToken.None);
        Assert.DoesNotContain(getAllResult, x => x.Data_Id == insertResult.Data_Id);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentDataId_ThrowsKeyNotFoundException()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsync(userId, new CoreDataRequest
        {
            Data_Id = Guid.NewGuid(),
            Data01 = "x",
            Data02 = "y",
            Data03 = "z",
            CoreUser = coreUser
        }, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentDataId_ThrowsKeyNotFoundException()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteAsync(userId, new CoreDataDelete
        {
            Data_Id = Guid.NewGuid(),
            CoreUser = coreUser
        }, CancellationToken.None));
    }
}