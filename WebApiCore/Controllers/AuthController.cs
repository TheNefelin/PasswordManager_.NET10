using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Interfaces;
using WebApiCore.Filters;
using WebApiCore.Helpers;

namespace WebApiCore.Controllers;

[Route("api/auth")]
[ApiController]
[ServiceFilter(typeof(ApiKeyFilter))]
[EnableRateLimiting("client_25_per_minute")]
public class AuthController : ControllerBase
{
    private readonly IAuthUserService _authUserService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthUserService authUserService, ILogger<AuthController> logger)
    {
        _authUserService = authUserService;
        _logger = logger;
    }

    [HttpPost("register")]
    [EnableRateLimiting("register_5_per_minute")]
    public async Task<ActionResult<ApiResponse<AuthUserResponse>>> Register(AuthUserRegister authUserRegister, CancellationToken cancellationToken)
    {
        var apiResult = await _authUserService.RegisterAsync(authUserRegister, cancellationToken);

        if (apiResult.IsSuccess)
            _logger.LogInformation("Registro exitoso. IP {Ip} - Email {Email}", ClientIpResolver.Resolve(HttpContext), authUserRegister.Email);
        else
            _logger.LogWarning("Registro fallido. IP {Ip} - Email {Email} - Status {StatusCode} - Motivo {Message}",
                ClientIpResolver.Resolve(HttpContext), authUserRegister.Email, apiResult.StatusCode, apiResult.Message);

        return StatusCode(apiResult.StatusCode, apiResult);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login_5_per_minute")]
    public async Task<ActionResult<ApiResponse<AuthUserLogged>>> Login(AuthUserLogin authUserLogin, CancellationToken cancellationToken)
    {
        var clientIp = ClientIpResolver.Resolve(HttpContext);
        var apiResult = await _authUserService.LoginAsync(authUserLogin, clientIp, cancellationToken);

        if (apiResult.IsSuccess)
            _logger.LogInformation("Login exitoso. IP {Ip} - Email {Email}", clientIp, authUserLogin.Email);
        else if (apiResult.StatusCode == StatusCodes.Status429TooManyRequests)
            _logger.LogWarning("Login bloqueado por IP. IP {Ip} - Email {Email}", clientIp, authUserLogin.Email);
        else
            _logger.LogWarning("Login fallido. IP {Ip} - Email {Email}", clientIp, authUserLogin.Email);

        return StatusCode(apiResult.StatusCode, apiResult);
    }
}