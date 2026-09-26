using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Interfaces;
using WebApiCore.Domain.Entities;
using WebApiCore.Domain.Interfaces;

namespace WebApiCore.Application.Services;

public class CoreDataService : ICoreDataService
{
    private readonly ICoreDataRepository _coreDataRepository;
    private readonly ICoreUserRepository _coreUserRepository;

    public CoreDataService(ICoreDataRepository coreDataRepository, ICoreUserRepository coreUserRepository)
    {
        _coreDataRepository = coreDataRepository;
        _coreUserRepository = coreUserRepository;
    }

    public async Task<IEnumerable<CoreDataResponse>> GetAllAsync(Guid userId, Guid sqlToken, CancellationToken cancellationToken)
    {
        var coreUser = await GetValidSessionAsync(userId, sqlToken, cancellationToken);
        if (coreUser == null)
            throw new UserSessionInvalidException();

        var coreDatas = await _coreDataRepository.GetAllAsync(
            new CoreData { User_Id = coreUser.User_Id },
            cancellationToken);

        return coreDatas.Select(ToDTO);
    }

    public async Task<CoreDataResponse> InsertAsync(Guid userId, CoreDataRequest coreDataRequest, CancellationToken cancellationToken)
    {
        var coreUser = await GetValidSessionAsync(userId, coreDataRequest.CoreUser.SqlToken, cancellationToken);
        if (coreUser == null)
            throw new UserSessionInvalidException();

        var coreData = await _coreDataRepository.InsertAsync(
            ToEntity(coreDataRequest, coreUser.User_Id),
            cancellationToken);

        return ToDTO(coreData);
    }

    public async Task<CoreDataResponse> UpdateAsync(Guid userId, CoreDataRequest coreDataRequest, CancellationToken cancellationToken)
    {
        var coreUser = await GetValidSessionAsync(userId, coreDataRequest.CoreUser.SqlToken, cancellationToken);
        if (coreUser == null)
            throw new UserSessionInvalidException();

        var coreData = ToEntity(coreDataRequest, coreUser.User_Id);

        if (!await _coreDataRepository.UpdateAsync(coreData, cancellationToken))
            throw new KeyNotFoundException();

        return ToDTO(coreData);
    }

    public async Task DeleteAsync(Guid userId, CoreDataDelete coreDataDelete, CancellationToken cancellationToken)
    {
        var coreUser = await GetValidSessionAsync(userId, coreDataDelete.CoreUser.SqlToken, cancellationToken);
        if (coreUser == null)
            throw new UserSessionInvalidException();

        if (!await _coreDataRepository.DeleteAsync(ToEntity(coreDataDelete, coreUser.User_Id), cancellationToken))
            throw new KeyNotFoundException();
    }

    private async Task<CoreUser?> GetValidSessionAsync(Guid userId, Guid sqlToken, CancellationToken cancellationToken)
    {
        return await _coreUserRepository.GetCoreUserAsync(
            new CoreUser
            {
                User_Id = userId,
                SqlToken = sqlToken
            },
            cancellationToken);
    }

    private static CoreDataResponse ToDTO(CoreData coreData)
    {
        return new CoreDataResponse
        {
            Data_Id = coreData.Data_Id,
            Data01 = coreData.Data01,
            Data02 = coreData.Data02,
            Data03 = coreData.Data03,
            User_Id = coreData.User_Id
        };
    }

    private static CoreData ToEntity(CoreDataRequest coreDataRequest, Guid userId)
    {
        return new CoreData
        {
            Data_Id = coreDataRequest.Data_Id,
            Data01 = coreDataRequest.Data01,
            Data02 = coreDataRequest.Data02,
            Data03 = coreDataRequest.Data03,
            User_Id = userId
        };
    }

    private static CoreData ToEntity(CoreDataDelete coreDataDelete, Guid userId)
    {
        return new CoreData
        {
            Data_Id = coreDataDelete.Data_Id,
            User_Id = userId
        };
    }
}