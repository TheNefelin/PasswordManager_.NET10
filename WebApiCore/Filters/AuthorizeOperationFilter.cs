using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace WebApiCore.Filters;

public sealed class AuthorizeOperationFilter : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor controllerActionDescriptor)
            return Task.CompletedTask;

        var methodInfo = controllerActionDescriptor.MethodInfo;

        var hasAuthorize =
            methodInfo.IsDefined(typeof(AuthorizeAttribute), inherit: true);

        var controllerHasAuthorize =
            controllerActionDescriptor.ControllerTypeInfo
                .IsDefined(typeof(AuthorizeAttribute), inherit: true);

        var hasAllowAnonymous =
            methodInfo.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);

        if ((hasAuthorize || controllerHasAuthorize) && !hasAllowAnonymous)
        {
            operation.Security ??= [];

            operation.Security.Add(
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer")] = []
                });

            operation.Responses ??= new OpenApiResponses();

            if (!operation.Responses.ContainsKey("401"))
            {
                operation.Responses.Add(
                    "401",
                    new OpenApiResponse
                    {
                        Description =
                            "Unauthorized - Token requerido o inválido"
                    });
            }

            if (!operation.Responses.ContainsKey("403"))
            {
                operation.Responses.Add(
                    "403",
                    new OpenApiResponse
                    {
                        Description =
                            "Forbidden - Permisos insuficientes"
                    });
            }
        }

        return Task.CompletedTask;
    }
}
