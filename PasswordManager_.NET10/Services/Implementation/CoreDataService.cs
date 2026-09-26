using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.DTOs.Request;
using PasswordManager_.NET10.Helpers;
using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Services.Implementation;

public class CoreDataService : ICoreDataService
{
    private readonly ILogger<CoreDataService> _logger;
    private readonly IApiService _apiService;
    private readonly ISecureStorageService _secureStorageService;

    public CoreDataService(
        ILogger<CoreDataService> logger, 
        IApiService apiService, 
        ISecureStorageService secureStorageService)
    {
        _logger = logger;
        _apiService = apiService;
        _secureStorageService = secureStorageService;
    }

    private async Task<CoreUserRequest> GetCoreUserData()
    {
        var userId = Guid.TryParse(await _secureStorageService.GetUserIdAsync(), out var uid) ? uid : Guid.Empty;
        var sqlToken = Guid.TryParse(await _secureStorageService.GetSqlTokenAsync(), out var stoken) ? stoken : Guid.Empty;

        return new CoreUserRequest
        {
            User_Id = userId,
            SqlToken = sqlToken
        };
    }

    public async Task<CoreUserIV> RegisterCorePasswordAsync(string password)
    {
        try
        {
            _logger.LogInformation("[CoreDataService-RegisterCorePasswordAsync] Registering core password.");

            var coreUserRequest = await GetCoreUserData();
            var coreUserIVRequest = new CoreUserIVRequest
            {
                Password = password,
                CoreUser = coreUserRequest
            };
            return await _apiService.PostAsync<CoreUserIV>(Constants.CORE_REGISTER_PASSWORD_ENDPOINT, coreUserIVRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CoreDataService-RegisterCorePasswordAsync] Error registering core password.");
            throw;
        }
    }

    public async Task<CoreUserIV> GetCoreUserIVAsync(string password)
    {
        try
        {
            _logger.LogInformation("[CoreDataService-GetCoreUserIVAsync] Retrieving core user IV.");

            var coreUserRequest = await GetCoreUserData();
            var coreUserIVRequest = new CoreUserIVRequest
            {
                Password = password,
                CoreUser = coreUserRequest
            };
            return await _apiService.PostAsync<CoreUserIV>(Constants.CORE_GET_IV_ENDPOINT, coreUserIVRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CoreDataService-GetCoreUserIVAsync] Error retrieving core user IV.");
            throw;
        }
    }

    public async Task<IEnumerable<CoreSecretData>> GetAllCoreDataAsync()
    {
        try
        {
            _logger.LogInformation("[CoreDataService-GetAllCoreDataAsync] Retrieving core secret data.");
     
            var coreUserRequest =  await GetCoreUserData();            
            return await _apiService.GetAsync<IEnumerable<CoreSecretData>>($"{Constants.CORE_CRUD_ENDPOINT}?User_Id={coreUserRequest.User_Id}&SqlToken={coreUserRequest.SqlToken}");
        } 
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CoreDataService-GetAllCoreDataAsync] Error retrieving core secret data.");
            throw;
        }
    }

    public async Task<CoreSecretData> CreateCoreDataAsync(CoreSecretData coreSecretData)
    {
        try
        {
            _logger.LogInformation("[CoreDataService-CreateCoreDataAsync] Creating core secret data.");

            var coreUserRequest = await GetCoreUserData();
            var coreDataRequest = new CoreDataRequest
            {
                Data_Id = coreSecretData.Data_Id,
                Data01 = coreSecretData.Data01,
                Data02 =  coreSecretData.Data02,
                Data03 =  coreSecretData.Data03,
                CoreUser = coreUserRequest
            };
            return await _apiService.PostAsync<CoreSecretData>(Constants.CORE_CRUD_ENDPOINT, coreDataRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CoreDataService-CreateCoreDataAsync] Error creating core secret data.");
            throw;
        }
    }

    public async Task<CoreSecretData> UpdateCoreDataAsync(CoreSecretData coreSecretData)
    {
        try
        {
            _logger.LogInformation("[CoreDataService-UpdateCoreDataAsync] Updating core secret data.");

            var coreUserRequest = await GetCoreUserData();
            var coreDataRequest = new CoreDataRequest
            {
                Data_Id = coreSecretData.Data_Id,
                Data01 = coreSecretData.Data01,
                Data02 = coreSecretData.Data02,
                Data03 = coreSecretData.Data03,
                CoreUser = coreUserRequest
            };
            return await _apiService.PutAsync<CoreSecretData>(Constants.CORE_CRUD_ENDPOINT, coreDataRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CoreDataService-UpdateCoreDataAsync] Error updating core secret data.");
            throw;
        }
    }

    public async Task DeleteCoreDataAsync(Guid dataId)
    {
        try
        {
            _logger.LogInformation("[CoreDataService-DeleteCoreDataAsync] Deleting core secret data.");

            var coreUserRequest = await GetCoreUserData();
            var coreDataDeleteRequest = new CoreDataDeleteRequest
            {
                Data_Id = dataId,
                CoreUser = coreUserRequest
            };
            await _apiService.DeleteAsync(Constants.CORE_CRUD_ENDPOINT, coreDataDeleteRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CoreDataService-DeleteCoreDataAsync] Error deleting core secret data.");
            throw;
        }
    }
}