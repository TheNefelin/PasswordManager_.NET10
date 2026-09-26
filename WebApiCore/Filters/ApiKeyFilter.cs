using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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

            context.Result = CreateProblemResult(
                context.HttpContext,
                StatusCodes.Status429TooManyRequests,
                "Demasiadas solicitudes",
                "Demasiados intentos fallidos de ApiKey. Intenta nuevamente más tarde.");
            return;
        }

        var apiKey = context.HttpContext.Request.Headers[ApiKeyHeaderName].FirstOrDefault();

        if (string.IsNullOrEmpty(apiKey))
        {
            _lockoutService.RegisterFailure(clientIp);
            context.Result = CreateProblemResult(
                context.HttpContext,
                StatusCodes.Status401Unauthorized,
                "No autorizado",
                "ApiKey es requerida.");
            return;
        }

        var isValid = await _maeConfigService.ValidateApiKey(apiKey, context.HttpContext.RequestAborted);

        if (!isValid)
        {
            _lockoutService.RegisterFailure(clientIp);

            if (_lockoutService.IsBlocked(clientIp))
                _logger.LogWarning("IP {Ip} bloqueada por exceso de intentos fallidos de ApiKey.", clientIp);

            context.Result = CreateProblemResult(
                context.HttpContext,
                StatusCodes.Status401Unauthorized,
                "No autorizado",
                "ApiKey no autorizada.");
            return;
        }

        _lockoutService.Reset(clientIp);
        await next();
    }

    private static ObjectResult CreateProblemResult(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }
}