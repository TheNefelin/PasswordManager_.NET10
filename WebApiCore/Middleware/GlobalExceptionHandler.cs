using Microsoft.AspNetCore.Diagnostics;
using WebApiCore.Application.Common;

namespace WebApiCore.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Excepción no controlada.");

        var (statusCode, message) = exception switch
        {
            ArgumentNullException => (StatusCodes.Status400BadRequest, "Parámetro requerido no proporcionado."),
            ArgumentException => (StatusCodes.Status400BadRequest, "Argumento inválido."),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Acceso no autorizado."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado."),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Conflicto en la operación."),
            _ => (StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado.")
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var response = ApiResponse.Failure<object>(statusCode, message);
        response.TraceId = httpContext.TraceIdentifier;

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}