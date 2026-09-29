using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Interfaces;
using WebApiCore.Filters;
using WebApiCore.Helpers;

namespace WebApiCore.Controllers;

[Route("api/core")]
[ApiController]
[ServiceFilter(typeof(ApiKeyFilter))]
[Authorize]
[EnableRateLimiting("client_25_per_minute")]
public class CoreController : ControllerBase
{
    private const string SqlTokenHeaderName = "SqlToken";

    private readonly ICoreDataService _coreService;
    private readonly ICoreUserService _coreUserService;
    private readonly IIpLockoutService _corePasswordLockout;
    private readonly ILogger<CoreController> _logger;

    public CoreController(
        ICoreDataService coreService,
        ICoreUserService coreUserService,
        IIpLockoutService corePasswordLockout,
        ILogger<CoreController> logger)
    {
        _coreService = coreService;
        _coreUserService = coreUserService;
        _corePasswordLockout = corePasswordLockout;
        _logger = logger;
    }

    [HttpPost("register-password")]
    public async Task<ActionResult<CoreUserIV>> RegisterCoreUserPassword(CoreUserPasswordCreate coreUserRequest, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var response = await _coreUserService.RegisterCoreUserPasswordAsync(userId, coreUserRequest, cancellationToken);
        return Ok(response);
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<CoreUserChangeResponse>> ChangeCoreUserPassword(ChangeCorePasswordRequest coreUserRequest, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var clientIp = ClientIpResolver.Resolve(HttpContext);

        if (_corePasswordLockout.IsBlocked(clientIp))
            throw new TooManyLoginAttemptsException();

        try
        {
            var response = await _coreUserService.ChangeCorePasswordAsync(userId, coreUserRequest, cancellationToken);
            _corePasswordLockout.Reset(clientIp);
            return Ok(response);
        }
        catch (InvalidCredentialsException)
        {
            _corePasswordLockout.RegisterFailure(clientIp);

            if (_corePasswordLockout.IsBlocked(clientIp))
                _logger.LogWarning("IP {Ip} bloqueada por exceso de intentos fallidos de clave maestra.", clientIp);

            throw;
        }
    }

    [HttpPost("get-iv")]
    public async Task<ActionResult<CoreUserIV>> GetCoreUserIV(CoreUserPassword coreUserRequest, CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        var clientIp = ClientIpResolver.Resolve(HttpContext);

        if (_corePasswordLockout.IsBlocked(clientIp))
            throw new TooManyLoginAttemptsException();

        try
        {
            var response = await _coreUserService.GetCoreUserIVAsync(userId, coreUserRequest, cancellationToken);
            _corePasswordLockout.Reset(clientIp);
            return Ok(response);
        }
        catch (InvalidCredentialsException)
        {
            _corePasswordLockout.RegisterFailure(clientIp);

            if (_corePasswordLockout.IsBlocked(clientIp))
                _logger.LogWarning("IP {Ip} bloqueada por exceso de intentos fallidos de clave maestra.", clientIp);

            throw;
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CoreDataResponse>>> GetAllCore(
        [FromHeader(Name = SqlTokenHeaderName)] string? sqlToken,
        CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId) is ActionResult unauthorized)
            return unauthorized;

        // El token de sesión viaja en el header, nunca en la query string: en la
        // URL quedaba expuesto en logs, proxies e historial del navegador.
        if (!Guid.TryParse(sqlToken, out var sqlTokenValue))
            throw new UserSessionInvalidException();

        var response = await _coreService.GetAllAsync(userId, sqlTokenValue, cancellationToken);
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