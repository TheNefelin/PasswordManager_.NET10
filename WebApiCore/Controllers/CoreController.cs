using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Interfaces;
using WebApiCore.Domain.Entities;
using WebApiCore.Filters;

namespace WebApiCore.Controllers;

[Route("api/core")]
[ApiController]
[ServiceFilter(typeof(ApiKeyFilter))]
[Authorize]
[EnableRateLimiting("client_25_per_minute")]
public class CoreController : ControllerBase
{
    private readonly ICoreDataService _coreService;
    private readonly ICoreUserService _coreUserService;

    public CoreController(ICoreDataService coreService, ICoreUserService coreUserService)
    {
        _coreService = coreService;
        _coreUserService = coreUserService;
    }

    [HttpPost("register-password")]
    public async Task<ActionResult<ApiResponse<CoreUserIV>>> RegisterCoreUserPassword(CoreUserPassword coreUserRequest, CancellationToken cancellationToken)
    {
        var apiResult = await _coreUserService.RegisterCoreUserPasswordAsync(coreUserRequest, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }

    [HttpPost("get-iv")]
    public async Task<ActionResult<ApiResponse<CoreUserIV>>> GetCoreUserIV(CoreUserPassword coreUserRequest, CancellationToken cancellationToken)
    {
        var apiResult = await _coreUserService.GetCoreUserIVAsync(coreUserRequest, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<CoreData>>>> GetAllCore([FromQuery] CoreUserRequest coreUserRequest, CancellationToken cancellationToken)
    {
        var apiResult = await _coreService.GetAllAsync(coreUserRequest, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CoreData>>> InsertCore(CoreDataRequest coreDataRequest, CancellationToken cancellationToken)
    {
        var apiResult = await _coreService.InsertAsync(coreDataRequest, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }

    [HttpPut]
    public async Task<ActionResult<ApiResponse<CoreData>>> UpdateCore(CoreDataRequest coreDataRequest, CancellationToken cancellationToken)
    {
        var apiResult = await _coreService.UpdateAsync(coreDataRequest, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }

    [HttpDelete]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCore(CoreDataDelete coreDataDelete, CancellationToken cancellationToken)
    {
        var apiResult = await _coreService.DeleteAsync(coreDataDelete, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }
}