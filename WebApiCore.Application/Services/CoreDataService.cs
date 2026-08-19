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

    public async Task<ApiResponse<IEnumerable<CoreData>>> GetAllAsync(CoreUserRequest coreUserRequest, CancellationToken cancellationToken)
    {
        var coreUser = await GetValidSessionAsync(coreUserRequest, cancellationToken);
        if (coreUser == null)
            return ApiResponse.Failure<IEnumerable<CoreData>>(UnauthorizedStatusCode, UnauthorizedMessage);

        var coreDatas = await _coreDataRepository.GetAllAsync(
            new CoreData { User_Id = coreUser.User_Id },
            cancellationToken);

        return ApiResponse.Success(coreDatas, "Ok");
    }

    public async Task<ApiResponse<CoreData>> InsertAsync(CoreDataRequest coreDataRequest, CancellationToken cancellationToken)
    {
        var coreUser = await GetValidSessionAsync(coreDataRequest.CoreUser, cancellationToken);
        if (coreUser == null)
            return ApiResponse.Failure<CoreData>(UnauthorizedStatusCode, UnauthorizedMessage);

        var coreData = await _coreDataRepository.InsertAsync(
            new CoreData
            {
                Data01 = coreDataRequest.Data01,
                Data02 = coreDataRequest.Data02,
                Data03 = coreDataRequest.Data03,
                User_Id = coreUser.User_Id
            },
            cancellationToken);

        return ApiResponse.Success(coreData, "Se ha creado correctamente", 201);
    }

    public async Task<ApiResponse<CoreData>> UpdateAsync(CoreDataRequest coreDataRequest, CancellationToken cancellationToken)
    {
        var coreUser = await GetValidSessionAsync(coreDataRequest.CoreUser, cancellationToken);
        if (coreUser == null)
            return ApiResponse.Failure<CoreData>(UnauthorizedStatusCode, UnauthorizedMessage);

        var coreData = await _coreDataRepository.UpdateAsync(
            new CoreData
            {
                Data_Id = coreDataRequest.Data_Id,
                Data01 = coreDataRequest.Data01,
                Data02 = coreDataRequest.Data02,
                Data03 = coreDataRequest.Data03,
                User_Id = coreUser.User_Id
            },
            cancellationToken);

        return ApiResponse.Success(coreData, "Ok");
    }

    public async Task<ApiResponse<object>> DeleteAsync(CoreDataDelete coreDataDelete, CancellationToken cancellationToken)
    {
        var coreUser = await GetValidSessionAsync(coreDataDelete.CoreUser, cancellationToken);
        if (coreUser == null)
            return ApiResponse.Failure<object>(UnauthorizedStatusCode, UnauthorizedMessage);

        await _coreDataRepository.DeleteAsync(
            new CoreData
            {
                Data_Id = coreDataDelete.Data_Id,
                User_Id = coreUser.User_Id
            },
            cancellationToken);

        return ApiResponse.Success<object>(null!, "Se ha eliminado correctamente");
    }

    private async Task<CoreUser?> GetValidSessionAsync(CoreUserRequest coreUserRequest, CancellationToken cancellationToken)
    {
        return await _coreUserRepository.GetCoreUserAsync(
            new CoreUser
            {
                User_Id = coreUserRequest.User_Id,
                SqlToken = coreUserRequest.SqlToken
            },
            cancellationToken);
    }
}