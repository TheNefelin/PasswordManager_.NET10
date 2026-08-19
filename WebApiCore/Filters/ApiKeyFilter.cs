using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebApiCore.Application.Common;
using WebApiCore.Application.Interfaces;
using WebApiCore.Helpers;

namespace WebApiCore.Filters;

public class ApiKeyFilter : IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "ApiKey";

    private readonly IMaeConfigService _maeConfigService;
    private readonly IIpLockoutService _lockoutService;
    private readonly ILogger<ApiKeyFilter> _logger;

    public ApiKeyFilter(
        IMaeConfigService maeConfigService,
        [FromKeyedServices("api-key")] IIpLockoutService lockoutService,
        ILogger<ApiKeyFilter> logger)
    {
        _maeConfigService = maeConfigService;
        _lockoutService = lockoutService;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var clientIp = ClientIpResolver.Resolve(context.HttpContext);

        if (_lockoutService.IsBlocked(clientIp))
        {
            var remaining = _lockoutService.GetRemainingBlockTime(clientIp);
            if (remaining is TimeSpan remainingTime)
                context.HttpContext.Response.Headers.RetryAfter = ((int)remainingTime.TotalSeconds).ToString();

            context.Result = new ObjectResult(
                ApiResponse.Failure<object>(429, "Demasiados intentos fallidos de ApiKey. Intenta nuevamente más tarde."))
            {
                StatusCode = StatusCodes.Status429TooManyRequests
            };
            return;
        }

        var apiKey = context.HttpContext.Request.Headers[ApiKeyHeaderName].FirstOrDefault();

        if (string.IsNullOrEmpty(apiKey))
        {
            _lockoutService.RegisterFailure(clientIp);
            context.Result = new UnauthorizedObjectResult(ApiResponse.Failure<object>(401, "ApiKey es requerida."));
            return;
        }

        var isValid = await _maeConfigService.ValidateApiKey(apiKey);

        if (!isValid)
        {
            _lockoutService.RegisterFailure(clientIp);

            if (_lockoutService.IsBlocked(clientIp))
                _logger.LogWarning("IP {Ip} bloqueada por exceso de intentos fallidos de ApiKey.", clientIp);

            context.Result = new UnauthorizedObjectResult(ApiResponse.Failure<object>(401, "ApiKey no autorizada."));
            return;
        }

        _lockoutService.Reset(clientIp);
        await next();
    }
}