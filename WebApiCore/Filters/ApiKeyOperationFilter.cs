using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace WebApiCore.Filters;

public class ApiKeyOperationFilter : IOperationFilter
{
    private const string ApiKeyHeaderName = "ApiKey";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (RequiresApiKey(context.MethodInfo))
        {
            operation.Parameters ??= new List<IOpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = ApiKeyHeaderName,
                In = ParameterLocation.Header,
                Required = true,
                Description = "ApiKey requerida para acceder al endpoint.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
        }
    }

    private static bool RequiresApiKey(MethodInfo methodInfo)
        => methodInfo.GetCustomAttribute<ServiceFilterAttribute>()?.ServiceType == typeof(ApiKeyFilter)
           || methodInfo.DeclaringType?.GetCustomAttribute<ServiceFilterAttribute>()?.ServiceType == typeof(ApiKeyFilter);
}