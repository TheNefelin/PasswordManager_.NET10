using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebApiCore.Application.Common;
using WebApiCore.Application.Interfaces;

namespace WebApiCore.Filters;

public class ApiKeyFilter : IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "ApiKey";

    private readonly IMaeConfigService _maeConfigService;

    public ApiKeyFilter(IMaeConfigService maeConfigService)
    {
        _maeConfigService = maeConfigService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var apiKey = context.HttpContext.Request.Headers[ApiKeyHeaderName].FirstOrDefault();

        if (string.IsNullOrEmpty(apiKey))
        {
            context.Result = new UnauthorizedObjectResult(ApiResponse.Failure<object>(401, "ApiKey es requerida."));
            return;
        }

        var isValid = await _maeConfigService.ValidateApiKey(apiKey);

        if (!isValid)
        {
            context.Result = new UnauthorizedObjectResult(ApiResponse.Failure<object>(401, "ApiKey no autorizada."));
            return;
        }

        await next();
    }
}