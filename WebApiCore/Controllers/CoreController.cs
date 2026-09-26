using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
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
    public async Task<ActionResult<CoreUserIV>> RegisterCoreUserPassword(CoreUserPassword coreUserRequest, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var response = await _coreUserService.RegisterCoreUserPasswordAsync(userId, coreUserRequest, cancellationToken);
        return Ok(response);
    }

    [HttpPost("get-iv")]
    public async Task<ActionResult<CoreUserIV>> GetCoreUserIV(CoreUserPassword coreUserRequest, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var response = await _coreUserService.GetCoreUserIVAsync(userId, coreUserRequest, cancellationToken);
        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CoreDataResponse>>> GetAllCore([FromQuery] CoreUserRequest coreUserRequest, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var response = await _coreService.GetAllAsync(userId, coreUserRequest, cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<CoreDataResponse>> InsertCore(CoreDataRequest coreDataRequest, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var response = await _coreService.InsertAsync(userId, coreDataRequest, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut]
    public async Task<ActionResult<CoreDataResponse>> UpdateCore(CoreDataRequest coreDataRequest, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var response = await _coreService.UpdateAsync(userId, coreDataRequest, cancellationToken);
        return Ok(response);
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteCore(CoreDataDelete coreDataDelete, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        await _coreService.DeleteAsync(userId, coreDataDelete, cancellationToken);
        return NoContent();
    }

    private ActionResult? TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(sub, out userId))
            return null;

        return Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "No autorizado",
            detail: "No autorizado.");
    }
}