using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
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
        if (exception is RequestValidationException
            or RegistrationDisabledException
            or DuplicateEmailException
            or TooManyLoginAttemptsException
            or InvalidCredentialsException
            or UserSessionInvalidException
            or CorePasswordAlreadyExistsException
            or CorePasswordNotConfiguredException)
            _logger.LogWarning(exception, "Solicitud rechazada.");
        else
            _logger.LogError(exception, "Excepción no controlada.");

        var (statusCode, title, message) = exception switch
        {
            RequestValidationException => (StatusCodes.Status400BadRequest, "Solicitud incorrecta", exception.Message),
            CorePasswordAlreadyExistsException => (StatusCodes.Status400BadRequest, "Solicitud incorrecta", exception.Message),
            RegistrationDisabledException => (StatusCodes.Status403Forbidden, "Acceso denegado", exception.Message),
            DuplicateEmailException => (StatusCodes.Status409Conflict, "Conflicto", exception.Message),
            TooManyLoginAttemptsException => (StatusCodes.Status429TooManyRequests, "Demasiadas solicitudes", exception.Message),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "No autorizado", exception.Message),
            UserSessionInvalidException => (StatusCodes.Status401Unauthorized, "No autorizado", exception.Message),
            CorePasswordNotConfiguredException => (StatusCodes.Status401Unauthorized, "No autorizado", exception.Message),
            ArgumentNullException => (StatusCodes.Status400BadRequest, "Solicitud incorrecta", "Parámetro requerido no proporcionado."),
            ArgumentException => (StatusCodes.Status400BadRequest, "Solicitud incorrecta", "Argumento inválido."),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "No autorizado", "Acceso no autorizado."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado", "Recurso no encontrado."),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Conflicto", "Conflicto en la operación."),
            _ => (StatusCodes.Status500InternalServerError, "Error", "Ocurrió un error inesperado.")
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = message
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            problem,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken: cancellationToken);
        return true;
    }
}