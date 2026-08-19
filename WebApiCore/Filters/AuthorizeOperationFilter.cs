using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace WebApiCore.Filters;

public class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAuthorize = context.MethodInfo.GetCustomAttribute<AuthorizeAttribute>() != null;
        var controllerHasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttribute<AuthorizeAttribute>() != null;
        var hasAllowAnonymous = context.MethodInfo.GetCustomAttribute<AllowAnonymousAttribute>() != null;

        if ((hasAuthorize || controllerHasAuthorize) && !hasAllowAnonymous)
        {
            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecuritySchemeReference("Bearer"),
                        new List<string>()
                    }
                }
            };

            operation.Responses ??= new OpenApiResponses();
            if (!operation.Responses.ContainsKey("401"))
            {
                operation.Responses.Add("401", new OpenApiResponse
                {
                    Description = "Unauthorized - Token requerido o inválido"
                });
            }

            if (!operation.Responses.ContainsKey("403"))
            {
                operation.Responses.Add("403", new OpenApiResponse
                {
                    Description = "Forbidden - Permisos insuficientes"
                });
            }
        }
    }
}