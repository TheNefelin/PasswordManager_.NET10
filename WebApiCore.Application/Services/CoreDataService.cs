using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Interfaces;
using WebApiCore.Domain.Entities;
using WebApiCore.Domain.Interfaces;

namespace WebApiCore.Application.Services;

public class CoreDataService : ICoreDataService
{
    private const int UnauthorizedStatusCode = 401;
    private const string UnauthorizedMessage = "Debes iniciar sesión.";

    private readonly ICoreDataRepository _coreDataRepository;
    private readonly ICoreUserRepository _coreUserRepository;

    public CoreDataService(ICoreDataRepository coreDataRepository, ICoreUserRepository coreUserRepository)
    {
        _coreDataRepository = coreDataRepository;
        _coreUserRepository = coreUserRepository;
    }

    public async Task<ApiResponse<IEnumerable<CoreDataResponse>>> GetAllAsync(Guid userId, CoreUserRequest coreUserRequest, CancellationToken cancellationToken)
    {
        var coreUser = await GetValidSessionAsync(userId, coreUserRequest.SqlToken, cancellationToken);
        if (coreUser == null)
            return ApiResponse.Failure<IEnumerable<CoreDataResponse>>(UnauthorizedStatusCode, UnauthorizedMessage);

        var coreDatas = await _coreDataRepository.GetAllAsync(
            new CoreData { User_Id = coreUser.User_Id },
            cancellationToken);

        var response = coreDatas.Select(c => new CoreDataResponse
        {
            Data_Id = c.Data_Id,
            Data01 = c.Data01,
            Data02 = c.Data02,
            Data03 = c.Data03,
            User_Id = c.User_Id
        });

        return ApiResponse.Success(response, "Ok");
    }

    public async Task<ApiResponse<CoreDataResponse>> InsertAsync(Guid userId, CoreDataRequest coreDataRequest, CancellationToken cancellationToken)
    {
        var coreUser = await GetValidSessionAsync(userId, coreDataRequest.CoreUser.SqlToken, cancellationToken);
        if (coreUser == null)
            return ApiResponse.Failure<CoreDataResponse>(UnauthorizedStatusCode, UnauthorizedMessage);

        var coreData = await _coreDataRepository.InsertAsync(
            ToEntity(coreDataRequest, coreUser.User_Id),
            cancellationToken);

        return ApiResponse.Success(ToDTO(coreData), "Se ha creado correctamente", 201);
    }

    public async Task<ApiResponse<CoreDataResponse>> UpdateAsync(Guid userId, CoreDataRequest coreDataRequest, CancellationToken cancellationToken)
    {
        var coreUser = await GetValidSessionAsync(userId, coreDataRequest.CoreUser.SqlToken, cancellationToken);
        if (coreUser == null)
            return ApiResponse.Failure<CoreDataResponse>(UnauthorizedStatusCode, UnauthorizedMessage);

        var coreData = await _coreDataRepository.UpdateAsync(
            ToEntity(coreDataRequest, coreUser.User_Id),
            cancellationToken);

        return ApiResponse.Success(ToDTO(coreData), "Ok");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid userId, CoreDataDelete coreDataDelete, CancellationToken cancellationToken)
    {
        var coreUser = await GetValidSessionAsync(userId, coreDataDelete.CoreUser.SqlToken, cancellationToken);
        if (coreUser == null)
            return ApiResponse.Failure<object>(UnauthorizedStatusCode, UnauthorizedMessage);

        await _coreDataRepository.DeleteAsync(
            ToEntity(coreDataDelete, coreUser.User_Id),
            cancellationToken);

        return ApiResponse.Success<object>(null!, "Se ha eliminado correctamente");
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