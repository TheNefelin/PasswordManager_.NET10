using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using System.Threading.RateLimiting;
using WebApiCore.Application.Common;
using WebApiCore.Application.Interfaces;
using WebApiCore.Application.Services;
using WebApiCore.Domain.Interfaces;
using WebApiCore.Filters;
using WebApiCore.Health;
using WebApiCore.Helpers;
using WebApiCore.Infrastructure.Data;
using WebApiCore.Infrastructure.Options;
using WebApiCore.Infrastructure.Repositories;
using WebApiCore.Infrastructure.Security;
using WebApiCore.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ======================================================================
// SQL Server (Dapper)
// ======================================================================
builder.Services.AddSingleton<IDapperContext>(_ =>
{
    var connectionString = builder.Environment.IsDevelopment()
        ? builder.Configuration.GetConnectionString("SqlServer")
        : builder.Configuration.GetConnectionString("SqlServerWeb");

    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("La connection string 'SqlServer' (testing) o 'SqlServerWeb' (producción) no está configurada.");

    return new DapperContext(connectionString);
});

// ======================================================================
// JWT Configuration
// ======================================================================
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("La sección 'JWT' no está configurada.");
builder.Services.AddSingleton(jwtOptions);

// ======================================================================
// Security services
// ======================================================================
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IAuthTokenService, JwtTokenUtil>();
builder.Services.AddSingleton<IIpLockoutService>(_ =>
    new IpLockoutService(new IpLockoutOptions
    {
        MaxFailures = 5,
        FailureWindow = TimeSpan.FromMinutes(15),
        BlockDuration = TimeSpan.FromMinutes(15)
    }));
builder.Services.AddKeyedSingleton<IIpLockoutService>("api-key", (_, _) =>
    new IpLockoutService(new IpLockoutOptions
    {
        MaxFailures = 5,
        FailureWindow = TimeSpan.FromMinutes(10),
        BlockDuration = TimeSpan.FromHours(1)
    }));
builder.Services.AddScoped<ApiKeyFilter>();

// ======================================================================
// Health checks (liveness + BD)
// ======================================================================
builder.Services.AddHealthChecks().AddCheck<SqlHealthCheck>("sql");

// ======================================================================
// Repositories
// ======================================================================
builder.Services.AddTransient<IAuthUserRepository, AuthUserRepository>();
builder.Services.AddTransient<IMaeConfigRepository, MaeConfigRepository>();
builder.Services.AddTransient<ICoreUserRepository, CoreUserRepository>();
builder.Services.AddTransient<ICoreDataRepository, CoreDataRepository>();

// ======================================================================
// Application services
// ======================================================================
builder.Services.AddTransient<IAuthUserService, AuthUserService>();
builder.Services.AddTransient<IMaeConfigService>(sp =>
    new MaeConfigService(
        sp.GetRequiredService<IMaeConfigRepository>(),
        TimeSpan.FromSeconds(builder.Configuration.GetValue("ApiKeyCache:ExpirationSeconds", 30))));
builder.Services.AddTransient<ICoreUserService, CoreUserService>();
builder.Services.AddTransient<ICoreDataService, CoreDataService>();

// ======================================================================
// Controllers con errores de validación estandarizados
// ======================================================================
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    x => x.Key,
                    x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            return new BadRequestObjectResult(
                ApiResponse.Failure<object>(400, "Validación fallida.", errors, context.HttpContext.TraceIdentifier));
        };
    });

// ======================================================================
// Exception handler global (respuesta uniforme ApiResponse)
// AddProblemDetails habilita UseExceptionHandler() para invocar los
// IExceptionHandler registrados (GlobalExceptionHandler). No eliminar.
// ======================================================================
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ======================================================================
// JWT Authentication
// ======================================================================
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsJsonAsync(
                    ApiResponse.Failure<object>(401, "No autorizado.", traceId: context.HttpContext.TraceIdentifier));
            }
        };
    });

builder.Services.AddAuthorization();

// ======================================================================
// CORS
// ======================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("_allowedOrigins", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();

        if (allowedOrigins.Length == 0)
            throw new InvalidOperationException("La sección 'Cors:AllowedOrigins' no está configurada.");

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ======================================================================
// Rate limiting (protección contra ataques)
// ======================================================================
var rateLimitPermit = builder.Configuration.GetValue("RateLimit:PermitLimit", 25);
var rateLimitWindow = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimit:WindowSeconds", 60));
var loginRateLimitPermit = builder.Configuration.GetValue("RateLimit:LoginPermitLimit", 5);
var loginRateLimitWindow = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimit:LoginWindowSeconds", 60));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("client_25_per_minute", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientIpResolver.Resolve(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rateLimitPermit,
                Window = rateLimitWindow,
                QueueLimit = 0
            }));

    options.AddPolicy("login_5_per_minute", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientIpResolver.Resolve(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = loginRateLimitPermit,
                Window = loginRateLimitWindow,
                QueueLimit = 0
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponse.Failure<object>(429, "Demasiadas solicitudes. Intenta nuevamente en un minuto.",
                traceId: context.HttpContext.TraceIdentifier),
            cancellationToken);
    };
});

// ======================================================================
// Swagger con JWT
// ======================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WebApiCore API",
        Version = "v1",
        Description = "API CORE + AUTH con autenticación JWT"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando el esquema Bearer. Ejemplo: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    c.OperationFilter<AuthorizeOperationFilter>();
    c.OperationFilter<ApiKeyOperationFilter>();
});

var app = builder.Build();

// ======================================================================
// Pipeline HTTP
// ======================================================================
app.UseExceptionHandler();
app.UseHttpsRedirection();

// ======================================================================
// Security headers (protección básica de respuesta; no aplica a Swagger)
// ======================================================================
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = string.Empty;
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "WebApiCore API v1");
    options.DisplayRequestDuration();
});

app.UseCors("_allowedOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// ======================================================================
// 404 uniforme (ApiResponse)
// ======================================================================
app.MapFallback(async context =>
{
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(
        ApiResponse.Failure<object>(404, "Recurso no encontrado.", traceId: context.TraceIdentifier));
});

app.Run();

public partial class Program { }