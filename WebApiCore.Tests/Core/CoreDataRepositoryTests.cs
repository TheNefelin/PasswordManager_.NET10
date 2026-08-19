using WebApiCore.Domain.Entities;
using WebApiCore.Infrastructure.Repositories;
using WebApiCore.Tests.Helpers;

namespace WebApiCore.Tests.Core;

public class CoreDataRepositoryTests : IntegrationTestBase
{
    [Fact]
    public async Task InsertGetAllUpdateDelete_FullFlow()
    {
        var (userId, _) = await CreateUserDirectAsync(NewEmail());
        var repository = new CoreDataRepository(Context);

        var inserted = await repository.InsertAsync(
            new CoreData { Data01 = "a", Data02 = "b", Data03 = "c", User_Id = userId },
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, inserted.Data_Id);

        var all = (await repository.GetAllAsync(new CoreData { User_Id = userId }, CancellationToken.None)).ToList();
        Assert.Contains(all, x => x.Data_Id == inserted.Data_Id);

        var updated = await repository.UpdateAsync(
            new CoreData { Data_Id = inserted.Data_Id, Data01 = "x", Data02 = "y", Data03 = "z", User_Id = userId },
            CancellationToken.None);

        Assert.Equal("x", updated.Data01);

        var afterUpdate = (await repository.GetAllAsync(new CoreData { User_Id = userId }, CancellationToken.None))
            .Single(x => x.Data_Id == inserted.Data_Id);
        Assert.Equal("x", afterUpdate.Data01);

        await repository.DeleteAsync(
            new CoreData { Data_Id = inserted.Data_Id, User_Id = userId },
            CancellationToken.None);

        var afterDelete = await repository.GetAllAsync(new CoreData { User_Id = userId }, CancellationToken.None);
        Assert.DoesNotContain(afterDelete, x => x.Data_Id == inserted.Data_Id);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_ForUserWithoutData()
    {
        var (userId, _) = await CreateUserDirectAsync(NewEmail());
        var repository = new CoreDataRepository(Context);

        var result = await repository.GetAllAsync(new CoreData { User_Id = userId }, CancellationToken.None);

        Assert.Empty(result);
    }
}