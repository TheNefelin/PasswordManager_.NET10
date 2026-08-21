using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Reflection;

namespace WebApiCore.Filters;

public sealed class ApiKeyOperationFilter : IOpenApiOperationTransformer
{
    private const string ApiKeyHeaderName = "ApiKey";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor &&
            RequiresApiKey(controllerActionDescriptor.MethodInfo))
        {
            operation.Parameters ??= [];

            // Evita duplicar el parámetro si otro transformer
            // o metadata ya lo agregó.
            var alreadyExists = operation.Parameters.Any(parameter =>
                parameter is OpenApiParameter openApiParameter &&
                string.Equals(
                    openApiParameter.Name,
                    ApiKeyHeaderName,
                    StringComparison.OrdinalIgnoreCase) &&
                openApiParameter.In == ParameterLocation.Header);

            if (!alreadyExists)
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = ApiKeyHeaderName,
                    In = ParameterLocation.Header,
                    Required = true,
                    Description = "ApiKey requerida para acceder al endpoint.",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String
                    }
                });
            }
        }

        return Task.CompletedTask;
    }

    private static bool RequiresApiKey(MethodInfo methodInfo)
        => methodInfo.GetCustomAttribute<ServiceFilterAttribute>()?.ServiceType == typeof(ApiKeyFilter)
           || methodInfo.DeclaringType?.GetCustomAttribute<ServiceFilterAttribute>()?.ServiceType == typeof(ApiKeyFilter);
}