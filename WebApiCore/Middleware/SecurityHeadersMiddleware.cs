namespace WebApiCore.Middleware;

public class SecurityHeadersMiddleware
{
    private const string ContentSecurityPolicy = "default-src 'none'";
    private const string ReferrerPolicy = "no-referrer";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        // Swagger UI está montado en la raíz (/).
        // También excluimos los recursos internos que Swagger UI utiliza.
        var isSwaggerRequest =
            path == "/" ||
            path == "/index.html" ||
            path.StartsWithSegments("/swagger");

        // Headers de seguridad generales.
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = ReferrerPolicy;

        // El CSP restrictivo NO se aplica a Swagger UI.
        if (!isSwaggerRequest)
        {
            context.Response.Headers["Content-Security-Policy"] =
                ContentSecurityPolicy;
        }

        await _next(context);
    }
}