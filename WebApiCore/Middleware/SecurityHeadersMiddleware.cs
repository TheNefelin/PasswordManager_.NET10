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
        if (!context.Request.Path.StartsWithSegments("/swagger"))
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = ReferrerPolicy;
            context.Response.Headers["Content-Security-Policy"] = ContentSecurityPolicy;
        }

        await _next(context);
    }
}