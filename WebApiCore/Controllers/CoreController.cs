using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Interfaces;
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
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var apiResult = await _coreUserService.RegisterCoreUserPasswordAsync(userId, coreUserRequest, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }

    [HttpPost("get-iv")]
    public async Task<ActionResult<ApiResponse<CoreUserIV>>> GetCoreUserIV(CoreUserPassword coreUserRequest, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var apiResult = await _coreUserService.GetCoreUserIVAsync(userId, coreUserRequest, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<CoreDataResponse>>>> GetAllCore([FromQuery] CoreUserRequest coreUserRequest, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var apiResult = await _coreService.GetAllAsync(userId, coreUserRequest, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CoreDataResponse>>> InsertCore(CoreDataRequest coreDataRequest, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var apiResult = await _coreService.InsertAsync(userId, coreDataRequest, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }

    [HttpPut]
    public async Task<ActionResult<ApiResponse<CoreDataResponse>>> UpdateCore(CoreDataRequest coreDataRequest, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var apiResult = await _coreService.UpdateAsync(userId, coreDataRequest, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }

    [HttpDelete]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCore(CoreDataDelete coreDataDelete, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var apiResult = await _coreService.DeleteAsync(userId, coreDataDelete, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }

    private ActionResult? TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(sub, out userId))
            return null;

        return Unauthorized(ApiResponse.Failure<object>(401, "No autorizado."));
    }
}