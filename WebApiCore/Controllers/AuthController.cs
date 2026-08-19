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

    public AuthController(IAuthUserService authUserService)
    {
        _authUserService = authUserService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthUserResponse>>> Register(AuthUserRegister authUserRegister, CancellationToken cancellationToken)
    {
        var apiResult = await _authUserService.RegisterAsync(authUserRegister, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthUserLogged>>> Login(AuthUserLogin authUserLogin, CancellationToken cancellationToken)
    {
        var clientIp = ClientIpResolver.Resolve(HttpContext);
        var apiResult = await _authUserService.LoginAsync(authUserLogin, clientIp, cancellationToken);
        return StatusCode(apiResult.StatusCode, apiResult);
    }
}