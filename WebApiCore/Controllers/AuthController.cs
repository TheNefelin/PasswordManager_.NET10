using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    public async Task<ActionResult<AuthUserResponse>> Register(AuthUserRegister authUserRegister, CancellationToken cancellationToken)
    {
        var response = await _authUserService.RegisterAsync(authUserRegister, cancellationToken);
        _logger.LogInformation("Registro exitoso. IP {Ip} - Email {Email}", ClientIpResolver.Resolve(HttpContext), authUserRegister.Email);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login_5_per_minute")]
    public async Task<ActionResult<AuthUserLogged>> Login(AuthUserLogin authUserLogin, CancellationToken cancellationToken)
    {
        var clientIp = ClientIpResolver.Resolve(HttpContext);
        var response = await _authUserService.LoginAsync(authUserLogin, clientIp, cancellationToken);
        _logger.LogInformation("Login exitoso. IP {Ip} - Email {Email}", clientIp, authUserLogin.Email);
        return Ok(response);
    }
}