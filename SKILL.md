# SKILL: .NET (C#) — Patrón Senior para APIs REST (transversal)

Guía de referencia para construir APIs REST en **.NET (ASP.NET Core) + Dapper + SQL Server** siguiendo una arquitectura y convenciones senior validadas en producción (`WebApiCore`, Clean Architecture, portada a .NET 10). Es **transversal**: los ejemplos son genéricos (auth, CRUD, manejo de errores, configuración, seguridad, tests) y aplican a cualquier dominio. Cubre también buenas prácticas para clientes **MAUI** (MVVM) porque comparten las mismas reglas de C#, seguridad y testing.

Este archivo es un **skill**: se lee para replicar el patrón en cualquier proyecto .NET nuevo. No es una receta dogmática; es la lista de decisiones que convierten un CRUD simple en un backend mantenible, seguro y desplegable.

> Compatibilidad: .NET 8/9/10. Donde hay diferencias de API o de comportamiento entre versiones, se marca explícitamente (p.ej. `dotnet test` en .NET 10, Swashbuckle 10/OpenApi 2.x).

---

## 1. ¿Por qué este patrón es SENIOR?

Porque resuelve los problemas que matan a las APIs .NET cuando crecen, con decisiones **justificadas**, no por moda:

| Decisión | Problema que resuelve |
|----------|----------------------|
| **Clean Architecture por capas** (`Domain` → `Application` → `Infrastructure` → `API`) | Dependencias en una sola dirección; la API no conoce repositorios, los servicios no conocen Dapper. Cambiar de ORM o de BD no toca la capa de aplicación |
| **Contrato de respuestas consistente** | Por defecto éxito = DTO directo y error = `ProblemDetails` (RFC 9457). Envelope `ApiResponse<T>` solo si hay requisito explícito (contrato único). El error real es mezclar contratos |
| **`GlobalExceptionHandler` → 500 genérico** | El detalle real de la excepción va al log, nunca al cliente. Sin fuga de stack traces ni internos |
| **Fail-fast de configuración** | Config inválida (connection string faltante, CORS vacío, JWT sin sección) → excepción al arrancar, no fallas en runtime difíciles de diagnosticar |
| **Connection string por entorno** (`Development` → local, resto → producción) | El mismo código corre en local y en producción sin tocar el repositorio; la config correcta la decide el entorno |
| **JWT identifica + `ApiKey` global** | Separa "quién puede llamar a la API" (ApiKey del origen, validada contra BD/config) de "quién es el usuario" (JWT) |
| **Rate limiting particionado por IP + políticas por endpoint sensible** | Protección de fuerza bruta que no bloquea a todos los usuarios por igual y endurece los puntos críticos (login/register) |
| **Lockout por IP con `TimeProvider`** | Bloqueo temporal tras N fallos (ventana + duración configurables), testeable sin timers reales |
| **Contraseñas con PBKDF2 (KDF)** | Hash seguro con salt e iteraciones configurables; nunca almacenar texto plano ni MD5/SHA simples |
| **Dapper + decisión explícita sobre dónde vive la lógica** | Consultas de lectura en C#; escrituras críticas (p.ej. registro) vía SP con contrato `IsSuccess/StatusCode/Message`. Dapper es simple y sin magic strings del ORM |
| **JWT con `JsonWebTokenHandler`** | Handler moderno y rendimiento superior; el legado `JwtSecurityTokenHandler` solo aporta deuda |
| **Comparaciones secretas en tiempo constante** | `CryptographicOperations.FixedTimeEquals` para ApiKey y credenciales → inmune a timing attacks |
| **`CancellationToken` propagado en toda la cadena** | Requests cancelados no dejan operaciones colgadas en BD ni hilos ocupados |
| **Auditoría de eventos de seguridad** | Login/register (y fallos) se loguean con IP y email → trazabilidad de ataques |
| **Security headers en respuestas de API** | `nosniff`, `X-Frame-Options`, `Referrer-Policy`, CSP → endurecen el navegador/cliente |
| **Health check** (`/health` con chequeo de BD) | Liveness/readiness sin instrumentación externa |
| **Tests de integración con BD real + tests HTTP con `WebApplicationFactory`** | Validan el flujo completo (DTO → SP → respuesta) contra la base real y el pipeline HTTP completo, no contra mocks que mienten |
| **Sin secretos en el código** | Connection strings, claves JWT y ApiKeys van en configuración/secrets del entorno (user-secrets en dev, variables de entorno en prod), nunca hardcodeadas ni en el repo |

---

## 2. Stack recomendado

| Capa | Tecnología | Nota |
|------|-----------|------|
| API | ASP.NET Core (net8/net9/net10 según contexto) | Web API con Controllers, no minimal API para CRUD corporativo |
| ORM | **Dapper** + `Microsoft.Data.SqlClient` | Ligero, explícito, sin tracking |
| BD | SQL Server | SPs solo donde aportan (escrituras críticas con contrato de salida) |
| Auth | JWT (`Microsoft.AspNetCore.Authentication.JwtBearer`) + ApiKey propio | Filter/attr; generación con `JsonWebTokenHandler` |
| Rate limiting | ASP.NET Core RateLimiter | `RateLimitPartition.GetFixedWindowLimiter` + partición por IP |
| Lockout | Servicio propio con `TimeProvider` | BCL, sin timers ni dependencias |
| Logs | `ILogger` + `GlobalExceptionHandler` | Sin librería de terceros necesaria; auditoría de eventos de seguridad |
| Tests | xUnit v3 + `Microsoft.AspNetCore.Mvc.Testing` | Integración contra BD real + HTTP con `WebApplicationFactory` |
| Serialización | System.Text.Json | CamelCase, sin ciclos |
| Documentación | Swagger/Swashbuckle | Versionado compatible (ver §11) |

---

## 3. Estructura de carpetas (Clean Architecture)

```
MyApi.sln
├── MyApi.Domain/            # Modelos, DTOs, entidades (sin dependencias)
│   └── Models/              # e.g. ApiResponse<T>, User, LoginRequest
├── MyApi.Application/       # Servicios y lógica de negocio
│   ├── Services/            # e.g. UserService, AuthService
│   └── Interfaces/          # Contratos de servicios (ej. IIpLockoutService)
├── MyApi.Infrastructure/    # Acceso a datos (Dapper, context, SPs), seguridad
│   ├── Repositories/
│   ├── Context/             # IDapperContext
│   └── Security/            # PasswordHasher, JwtTokenUtil, IpLockoutService
└── MyApi/                   # API (Program.cs, Controllers, Filters, Middleware)
    ├── Controllers/
    ├── Filters/             # ApiKeyFilter, operaciones de Swagger
    ├── Middleware/          # GlobalExceptionHandler, SecurityHeadersMiddleware
    ├── Helpers/             # ClientIpResolver
    ├── Health/              # SqlHealthCheck
    └── appsettings.json     # + appsettings.{Environment}.json (sin secretos)
```

Reglas de dependencia (una sola dirección):
- `Domain` no conoce a nadie.
- `Application` conoce a `Domain`.
- `Infrastructure` conoce a `Application` (y `Domain`).
- `MyApi` (API) conoce a todos, pero **nadie la conoce a ella**.

**Anti-patrón clásico**: un solo proyecto con `Models/Repositories/Services/Controllers` todos juntos. Funciona el primer año; luego las dependencias se mezclan y migrar de ORM o de BD rompe todo.

---

## 4. Capa de datos (Dapper)

### Contexto (`IDapperContext`)

```csharp
public interface IDapperContext
{
    string ConnectionString { get; }
}
```

Implementación que toma la connection string del entorno activo con **fail-fast**:

```csharp
public class DapperContext : IDapperContext
{
    public string ConnectionString { get; }

    public DapperContext(IConfiguration config, IHostEnvironment env)
    {
        var conn = env.IsDevelopment()
            ? config.GetConnectionString("Local")
            : config.GetConnectionString("Production");

        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("No se encontró la connection string del entorno activo.");
        ConnectionString = conn;
    }
}
```

### Reglas generales de los repositorios

- Reciben **datos ya procesados** (p.ej. `passwordHash` calculado en Application); la capa de datos no aplica lógica de negocio.
- **`CancellationToken` en todos los métodos async** y propagado al `CommandDefinition`.
- Parámetros explícitos con `DynamicParameters` cuando haya tipos/salidas especiales; anónimos solo en lecturas simples.
- Preferir **consultas de lectura en C#** (queries directas) y reservar SPs para **escrituras críticas** con contrato de resultado. Decisión por caso, no por dogma.

### SP de escritura con contrato de salida

```sql
-- Patrón de SP de escritura
CREATE PROCEDURE RegisterUser
    @Email NVARCHAR(100),
    @PasswordHash NVARCHAR(500),
    @IsSuccess BIT OUTPUT,
    @StatusCode INT OUTPUT,
    @Message NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM Users WHERE Email = @Email)
    BEGIN
        SET @IsSuccess = 0; SET @StatusCode = 400;
        SET @Message = N'El email ya existe'; RETURN;
    END
    -- insert...
END
```

### Uso desde repositorio

```csharp
public async Task<CommandResult> RegisterAsync(string email, string passwordHash, CancellationToken ct)
{
    using var connection = new SqlConnection(_context.ConnectionString);
    var p = new DynamicParameters();
    p.Add("@Email", email);
    p.Add("@PasswordHash", passwordHash);
    p.Add("@IsSuccess", dbType: DbType.Boolean, direction: ParameterDirection.Output);
    p.Add("@StatusCode", dbType: DbType.Int32, direction: ParameterDirection.Output);
    p.Add("@Message", dbType: DbType.String, size: 500, direction: ParameterDirection.Output);

    var cmd = new CommandDefinition("RegisterUser", p, commandType: CommandType.StoredProcedure, cancellationToken: ct);
    await connection.ExecuteAsync(cmd);

    return new CommandResult(p.Get<bool>("@IsSuccess"), p.Get<int>("@StatusCode"), p.Get<string>("@Message"));
}
```

**Lectura en C#** (login verificado fuera de SP, sin exponer el hash en SQL):

```csharp
public async Task<User?> GetUserByEmailAsync(string email, CancellationToken ct)
{
    using var connection = new SqlConnection(_context.ConnectionString);
    var cmd = new CommandDefinition(
        "SELECT * FROM Users WHERE Email = @Email",
        new { Email = email },
        cancellationToken: ct);
    return await connection.QueryFirstOrDefaultAsync<User>(cmd);
}
```

---

## 5. Contrato de respuestas y errores

Dos patrones válidos. **Elegir UNO y aplicarlo a todo el API**; el error real es mezclarlos.

### Patrón por defecto (recomendado en proyectos nuevos): DTO + `ProblemDetails`

- **Éxito (2xx)** → el DTO directo: `200 { "id": 1, "email": "ana@x.com" }`.
- **Error** → `ProblemDetails` (RFC 9457, nativo de ASP.NET Core con `AddProblemDetails` + `IProblemDetailsService`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Una o más validaciones fallaron.",
  "status": 400,
  "traceId": "0HNNUBE1867EG:00000001",
  "errors": { "email": ["El email ya existe."] }
}
```

Ventajas: estándar de la industria (RFC 9457), nativo del framework (el `400`/`401`/`404` se producen casi sin código propio), interoperable con clientes y monitores, y el éxito no va envuelto.

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<UserDto>> GetById(int id, CancellationToken ct)
{
    var user = await _userService.GetByIdAsync(id, ct);
    return user is null ? NotFound() : Ok(user);
}
```

### Alternativa: envelope uniforme `ApiResponse<T>` (solo con requisito explícito)

Un solo contrato para éxito y error, con `traceId`:

```csharp
public class ApiResponse<T>
{
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }  // o List<string>
    public string? TraceId { get; set; }
}
```

```json
{ "isSuccess": true, "statusCode": 200, "message": "OK", "data": { ... }, "errors": null, "traceId": "..." }
```

Cuándo elegirlo:
- El cliente requiere **un único esquema de deserialización** (no decidir la forma según el status HTTP).
- Se necesita envolver `data` con metadatos de negocio.
- Frontend legacy que ya parsea un envelope.

Costo: el éxito va envuelto y no es un estándar interoperable.

### Tabla de códigos coherente (aplica a ambos patrones)

| Caso | HTTP | Respuesta |
|------|------|-----------|
| Éxito | 200/201 | DTO directo o `IsSuccess=true` |
| Entrada inválida (validación) | 400 | `ProblemDetails` con `errors` |
| No autenticado | 401 | `ProblemDetails` |
| Sin permiso | 403 | `ProblemDetails` |
| No existe | 404 | `ProblemDetails` |
| Límite de peticiones excedido | 429 | `ProblemDetails` |
| Error interno | 500 | `ProblemDetails`, mensaje genérico |

**Anti-patrón**: mezclar contratos — unos endpoints con DTO directo, otros con `string` de error, otros con `ProblemDetails` crudo. El frontend termina con `if (status === 400) ... else if (typeof res === 'string') ...`.

> **Nota**: `WebApiCore` (repo de referencia) usa el envelope por decisión histórica (contrato ya consumido por su cliente MAUI). Es válido, pero **no** es el patrón por defecto recomendado en proyectos nuevos.

---

## 6. Manejo de errores global

### `GlobalExceptionHandler` (registrado con `AddExceptionHandler`)

```csharp
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex, "Error no controlado");
        var problem = new ProblemDetails
        {
            Status = 500,
            Title = "Ha ocurrido un error interno.",
            Extensions = { ["traceId"] = ctx.TraceIdentifier }
        };
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
```

Registro en `Program.cs`:

```csharp
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(); // puede requerirse por el handler
// pipeline: app.UseExceptionHandler(); (temprano, antes de seguridad/CORS)
```

### Error de modelo (400) uniforme

```csharp
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(opts =>
    {
        opts.InvalidModelStateResponseFactory = ctx =>
            new BadRequestObjectResult(new ValidationProblemDetails(ctx.ModelState)
            {
                Title = "Datos de entrada inválidos.",
                Extensions = { ["traceId"] = ctx.HttpContext.TraceIdentifier }
            });
    });
```

**Regla**: el mensaje de 500 es genérico para el cliente; el detalle real va al log. Nunca `ex.Message` en una respuesta 500.

> Si el proyecto eligió el envelope `ApiResponse<T>` (sección 5), estos handlers devuelven `ApiResponse.Failure`/`Success` en lugar de `ProblemDetails` — como hace `WebApiCore`. El patrón de centralización es el mismo; solo cambia el cuerpo.

---

## 7. Autenticación y autorización

### Contraseñas (PBKDF2 del BCL)

```csharp
public static string HashPassword(string password)
{
    byte[] salt = RandomNumberGenerator.GetBytes(16);
    byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
        password, salt, 100_000, HashAlgorithmName.SHA256, 32);
    return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
}

public static bool Verify(string password, string stored)
{
    var parts = stored.Split(':');
    var salt = Convert.FromBase64String(parts[0]);
    var expected = Convert.FromBase64String(parts[1]);
    var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, expected.Length);
    return CryptographicOperations.FixedTimeEquals(expected, actual); // tiempo constante
}
```

**Nunca**: texto plano, MD5, SHA1, o el mismo hash para todos los usuarios (sin salt).

### JWT — validación

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true,
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JWT:Key"]!)),
            ValidIssuer = config["JWT:Issuer"],
            ValidAudience = config["JWT:Audience"]
        };
        // 401 estándar (ProblemDetails) si falla el token
        opts.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = 401;
                return ctx.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = 401,
                    Title = "No autorizado.",
                    Extensions = { ["traceId"] = ctx.HttpContext.TraceIdentifier }
                });
            }
        };
    });
```

### JWT — generación con `JsonWebTokenHandler` (no el legado)

```csharp
var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
{
    Subject = new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role)
    }),
    Issuer = jwtOptions.Issuer,
    Audience = jwtOptions.Audience,
    Expires = DateTime.UtcNow.AddMinutes(jwtOptions.ExpireMin),
    IssuedAt = DateTime.UtcNow,
    NotBefore = DateTime.UtcNow,
    SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
});
```

> `JsonWebTokenHandler` no soporta `OutboundClaimTypeMap` (los claims se escriben tal cual, incluida la URI larga de `ClaimTypes.Role`). Si quieres control total de fechas, usa `SetDefaultTimesOnTokenCreation = false` y declara `IssuedAt`/`NotBefore`/`Expires` explícitamente.

### ApiKey global (separada del JWT)

El origen (Swagger, Postman, frontend) envía la ApiKey en **header** (nunca en query string). Se valida con comparación en tiempo constante. **Cachea el valor con TTL corto** para no golpear la BD en cada request:

```csharp
public class ApiKeyFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        if (!ctx.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var apiKey))
        {
            ctx.Result = new UnauthorizedObjectResult(ApiResponse.Failure<object>(401, "ApiKey requerida.", ctx.HttpContext.TraceIdentifier));
            return;
        }
        var stored = await _configService.GetApiKeyAsync(ctx.HttpContext.RequestAborted); // cacheado 30 s
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(stored) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(apiKey!), Encoding.UTF8.GetBytes(stored)))
        {
            ctx.Result = new UnauthorizedObjectResult(ApiResponse.Failure<object>(401, "ApiKey inválida.", ctx.HttpContext.TraceIdentifier));
            return;
        }
        await next();
    }
}
```

### Cache de valores de config (patrón transversal)

Cache corto con `TimeProvider` + lock (sin dependencias): primero consulta BD, luego sirve del cache mientras no expire. TTL de 30 s equilibra "no golpear BD" con "propagar rotación de la key rápido". Para el reloj, **siempre `TimeProvider`** (inyectado), nunca `DateTime.Now` directo: es lo que permite testear expiración sin dormir.

---

## 8. Rate limiting y CORS

### Rate limiting moderno (`.NET 7+`)

La API `AddFixedWindowLimiter` con `PartitionKey` está **obsoleta**. Se usa `RateLimitPartition`:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("client_25_per_minute", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientIpResolver.Resolve(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 25,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // política estricta solo para login (anti brute-force)
    options.AddPolicy("login_5_per_minute", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientIpResolver.Resolve(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // 429 con envelope uniforme
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponse.Failure<object>(429, "Demasiadas solicitudes. Intenta nuevamente.", traceId: context.HttpContext.TraceIdentifier), ct);
    };
});
```

En el controlador:

```csharp
[EnableRateLimiting("client_25_per_minute")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("login_5_per_minute")] // la política de acción gana sobre la del controller
    public IActionResult Login(...) { ... }
}
```

**Anti-patrón**: rate limit global sin partición por IP → un solo usuario abusivo bloquea a todos, o la regla no aplica tras un proxy porque todos llegan con la IP del balanceador.

### `ClientIpResolver` (compartido)

Un helper único para resolver la IP del cliente (rate limiter, lockout, auditoría lo usan) — primer valor de `X-Forwarded-For` con fallback a `RemoteIpAddress`:

```csharp
public static class ClientIpResolver
{
    public static string Resolve(HttpContext ctx)
    {
        var fwd = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(fwd)
            ? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : fwd;
    }
}
```

### Lockout por IP (bloqueo tras N fallos)

Rate limit protege el volumen; el **lockout** protege la lógica de credenciales (bloqueo por ventana + duración, con `429` y `Retry-After`). Patrón genérico y testeable:

```csharp
public class IpLockoutService : IIpLockoutService
{
    private readonly IpLockoutOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, LockoutEntry> _entries = new();

    public IpLockoutService(IpLockoutOptions options, TimeProvider? timeProvider = null)
        => (_options, _timeProvider) = (options, timeProvider ?? TimeProvider.System);

    public bool IsBlocked(string ip) { /* ventana expirada => limpiar entrada; bloqueado => true */ }
    public void RegisterFailure(string ip) { /* contador + ventana + activar bloqueo al llegar a MaxFailures */ }
    public void Reset(string ip) { /* quitar entrada (login exitoso) */ }
    public TimeSpan? GetRemainingBlockTime(string ip) { /* para Retry-After */ }
}
```

Reglas de diseño:
- **`TimeProvider` inyectado** (BCL, no es un timer): testear expiración/ventana sin esperar en tiempo real.
- **Lazy cleanup** sin timers: las entradas se limpian al consultar (ventana expirada). Un `System.Timers.Timer` para limpiar es complejidad innecesaria.
- Configuración por **instancia**: `IpLockoutOptions { MaxFailures, FailureWindow, BlockDuration }`. Puedes registrar dos instancias en DI (una para ApiKey, otra para login) sin duplicar lógica — `AddSingleton` + `AddKeyedSingleton` si ambas coexisten.
- El servicio no depende de `ILogger` (testeable y portable sin el shared framework); el logging va en el filter/controller.

### CORS (allow-list explícita)

```csharp
var cors = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy("Default", p =>
    p.WithOrigins(cors).AllowAnyHeader().AllowAnyMethod()));
```

**Regla**: `WithOrigins` con lista explícita. **Nunca** `SetIsOriginAllowed(_ => true)` ni `AllowAnyOrigin()` en producción, salvo API pública documentada.

---

## 9. Seguridad HTTP y operabilidad

### Security headers (middleware)

Headers de endurecimiento aplicados a las rutas de API (**excluir** `/swagger` para no romper la UI):

```csharp
app.UseMiddleware<SecurityHeadersMiddleware>(); // después de UseExceptionHandler, antes de endpoints
```

```csharp
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/swagger"))
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'";
            // en HTTPS de producción, añadir: context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000";
        }
        await _next(context);
    }
}
```

### Health check con chequeo de BD

```csharp
builder.Services.AddHealthChecks().AddCheck<SqlHealthCheck>("sql");
// ...
app.MapHealthChecks("/health"); // después de MapControllers
```

```csharp
public class SqlHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct)
    {
        try { /* SELECT 1 con Dapper/SqlClient */ return HealthCheckResult.Healthy(); }
        catch (Exception ex) { return HealthCheckResult.Unhealthy("SQL no disponible", ex); }
    }
}
```

### Auditoría de eventos de seguridad

En controllers/servicios, loguear los eventos sensibles con **IP + email** (los logs son la pista de ataques):

```csharp
_logger.LogWarning("Login fallido. IP {Ip} - Email {Email}", ip, email);
_logger.LogInformation("Login exitoso. IP {Ip} - Email {Email}", ip, email);
_logger.LogWarning("Login bloqueado por IP. IP {Ip} - Email {Email}", ip, email);
```

---

## 10. Configuración y fail-fast

`appsettings.json` (producción) y `appsettings.Development.json` (local) por separado; **sin secretos**:

```json
{
  "ConnectionStrings": {
    "Local": "Server=LOCAL;Database=...;Integrated Security=True",
    "Production": "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True"
  },
  "JWT": { "Key": "desde user-secrets / env var", "Issuer": "...", "Audience": "...", "ExpireMin": 60 },
  "Cors": { "AllowedOrigins": ["https://origen-frontend"] },
  "RateLimit": { "PermitLimit": 25, "WindowSeconds": 60, "LoginPermitLimit": 5, "LoginWindowSeconds": 60 }
}
```

Fail-fast: validar la config requerida **al arrancar**, no al primer request:

```csharp
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("La sección 'JWT' no está configurada.");
```

**Regla de oro**: un servidor que arranca con config inválida es un bug silencioso; uno que lanza excepción es un bug evidente.

### Secretos (regla estricta)

- **Nunca** commitear `appsettings.json` con secretos reales.
- **Desarrollo**: `dotnet user-secrets set "JWT:Key" "<clave>"` (viven en `%APPDATA%`, no se versionan).
- **Producción**: variables de entorno con la clave por sección (`JWT__Key`); el binding de configuración de .NET da prioridad a env vars sobre appsettings.
- Mantener en el repo una **plantilla `appsettings.Example.json`** con valores de ejemplo y sin datos reales.
- Si una clave de desarrollo quedó expuesta en el historial de git y se usó en algún entorno real, **rotarla** (es pública).
- Nota: una decisión documentada de "clave de testing versionada, producción con appsettings propio" es aceptable en proyectos internos **siempre que se registre explícitamente como riesgo asumido** y producción nunca reutilice esa clave.

---

## 11. Swagger / OpenAPI (compatibilidad de versiones)

La versión de Swashbuckle define la API de `Microsoft.OpenApi`:

| Swashbuckle | Microsoft.OpenApi | Nota |
|-------------|-------------------|------|
| 6.x | 1.x | `OpenApiSchema`, `OpenApiReference` (legado, aún válido) |
| 9.x | 1.x | Igual que 6.x pero más actualizada |
| 10.x | **2.x** | **Breaking**: `OpenApiSchema` no existe, `Type` es `JsonSchemaType?`, `OpenApiReference` eliminado |

### Ejemplo de filter compatible (OpenApi 2.x, Swashbuckle 10+)

```csharp
// en OpenApi 2.x el namespace Microsoft.OpenApi.Models fue reemplazado por Microsoft.OpenApi
var schemeRef = new OpenApiSecuritySchemeReference("Bearer");
operation.Security = [new OpenApiSecurityRequirement { { schemeRef, new List<string>() } }];
// para OpenApi 1.x (6.x/9.x) se usaba:
//   new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
```

Endpoint de Swagger en `Program.cs` — usar **ruta absoluta** para evitar problemas tras proxies/host virtuales:

```csharp
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MyApi v1"));
```

**Gotchas reales detectados**:
- Si `index.html`/`index.js` de Swagger se sirven con `Cache-Control` largo, el navegador cachea la UI vieja y muestra errores como *"does not specify a valid version field"* aunque el JSON sea válido. Probar con **Ctrl+Shift+R** (hard reload) o incógnito antes de concluir que el spec está roto.
- Validar el spec en `/swagger/v1/swagger.json` directamente, no la página HTML.
- Si Swashbuckle 9.x da problemas de render (UI rota en producción), **bajar a 6.6.2** es una solución validada que conserva todo el spec.

---

## 12. MAUI (clientes .NET multiplataforma)

Las mismas reglas de C# y seguridad aplican al frontend MAUI. Anti-patrones que rompen apps reales:

| Anti-patrón | Problema | Solución |
|-------------|----------|----------|
| `NotImplementedException` en métodos de servicio | La app "funciona" hasta que alguien toca ese botón → crash | Implementar o eliminar; si es placeholder, marcarlo explícitamente |
| `Application.Current.Windows[0].Page` para navegar | Navegación acoplada a la ventana, rompe con más de una ventana | `Shell.Current` / inyección de navegación (MVVM) |
| `catch (Exception) { }` vacío | Traga errores; el usuario ve que "no pasa nada" | Log + estado de error visible en UI |
| Operaciones `async void` fire-and-forget | Excepciones sin controlar crashean la app | `async Task`, `Command`, try/catch central |
| Crear `HttpClient` por cada llamada | Agotamiento de sockets | `HttpClient` singleton/inyectado |
| `HttpClientHandler` manual con SSL bypass | MITM → fuga de credenciales | Configuración de trust del SO, nunca `ServerCertificateCustomValidationCallback = (_) => true` |
| `System.Random` para contraseñas/IDs | Predictible, inseguro | `RandomNumberGenerator` |
| Columnas `Data01`/`Data02` en BD | Sin semántica, imposible de mantener | Nombres de dominio reales |
| `System.Timers.Timer` tocando la UI desde otro hilo | Race conditions / crashes de UI | `Dispatcher`/`MainThread.InvokeOnMainThreadAsync` |
| ViewModel Singleton con estado global compartido | Estado corrupto entre páginas | ViewModel por página, servicios como singletons |
| **Asumir el nombre del JSON sin verificar** | El contrato usa `user_Id` y el cliente lee `userId` → siempre `null` | Verificar el JSON real (o `[JsonPropertyName]`) y mapear explícito |

### Reglas MAUI senior
- **MVVM**: ViewModel por página, propiedades `ObservableProperty`, `[RelayCommand]`.
- **Inyección de dependencias** (DI nativa de MAUI): servicios en `MauiProgram`, páginas/VM resueltas por DI.
- **Nunca** lógica de negocio en `code-behind`; solo eventos de UI delegando a comandos.
- **HttpClient singleton + auth** con handlers que agregan JWT/ApiKey.
- **Contrato seguro con la API**: tokens sensibles (`SqlToken`) en **header**, nunca en query string (queda en logs/URLs).
- Tratar la migración/refactor como un **proyecto de auditoría**: leer el análisis previo y corregir los hallazgos uno a uno con aprobación del usuario (regla de issues).

---

## 13. Tests

- **xUnit v3 + `Microsoft.AspNetCore.Mvc.Testing`** (`WebApplicationFactory<T>`) para tests de integración HTTP del pipeline completo (filtros, rate limiting, headers, auth).
- **Fixtures con `IAsyncLifetime` implementan `ValueTask`** en xUnit v3 (no `Task`).
- Probar **contra BD real** (o instancia de prueba) para validar DTO → SP → respuesta completa; **no mockear repositorios** para probar la API.
- **Regla de no tocar datos ajenos**: los tests crean sus propios registros y los limpian **por ID propio** en `DisposeAsync` (SQL directo), nunca `DROP`/truncado de tablas ni borrado de datos existentes.
- **Aislamiento por IP**: cada test HTTP usa una **IP única y secuencial** (`192.0.2.{200 + contador}`) — nunca `Random` — para no colisionar con rate limiter/lockout (comparten el servidor del fixture).
- **Serializar tests que comparten BD**: colección xUnit con `DisableParallelization = true` (`[CollectionDefinition("Database", DisableParallelization = true)]` + `[Collection("Database")]` en las clases) para evitar carreras (p.ej. flags globales tipo `IsEnableRegister=0`).
- Verificar el **envelope**: `IsSuccess`, `StatusCode`, `Message` correctos para éxito, 400, 401, 404, 429 y 500.
- Cubrir con HTTP tests los casos de contrato: vínculo de identidad (JWT de A + token de B → 401), ApiKey ausente/incorrecta (401), rate limit (6º request → 429), 404 uniforme, security headers presentes.

### Comando (`dotnet test` según la versión de SDK)

- **SDK .NET 9 o anterior**: `dotnet test` normal.
- **SDK .NET 10 (modo MTP)**: con `global.json` en la raíz con
  ```json
  { "test": { "runner": "Microsoft.Testing.Platform" } }
  ```
  el comando válido es **`dotnet test --project <csproj>`**. NO usar argumentos posicionales ni `--nologo`/`-v q` (rompen el modo MTP con exit code 5). El runner descubre y ejecuta igual.

> Los comandos de tests no se ejecutan sin autorización explícita del usuario (reglas del repo).

---

## 14. Checklist final

- [ ] Estructura Clean Architecture con dependencias en una sola dirección.
- [ ] Contrato de respuestas consistente: por defecto éxito = DTO + error = `ProblemDetails` (RFC 9457); envelope `ApiResponse<T>` solo con requisito explícito.
- [ ] `GlobalExceptionHandler` central: log + 500 genérico, sin fuga de internos.
- [ ] 400 estándar vía `InvalidModelStateResponseFactory` (`ValidationProblemDetails`).
- [ ] Contraseñas con PBKDF2 + salt + iteraciones; verificación en tiempo constante.
- [ ] JWT con `ClockSkew=0`, issuer/audience validados, generación con `JsonWebTokenHandler`, key desde config.
- [ ] ApiKey en header (nunca en query string) validada contra BD en tiempo constante y **cacheada con TTL corto**.
- [ ] Rate limiting particionado por IP con `X-Forwarded-For` + políticas dedicadas para endpoints sensibles (login/register).
- [ ] Lockout por IP con `TimeProvider` (ventana + duración, `429` + `Retry-After`), sin timers.
- [ ] Security headers en rutas de API (excepto `/swagger`).
- [ ] Health check `/health` con chequeo de BD.
- [ ] Auditoría de eventos de seguridad (IP + email).
- [ ] `CancellationToken` propagado en toda la cadena (controllers → services → repositories → Dapper).
- [ ] CORS con allow-list explícita; sin `AllowAnyOrigin` en producción.
- [ ] Fail-fast de configuración al arrancar.
- [ ] Sin secretos hardcodeados ni en el repo (user-secrets en dev, env vars en prod, `appsettings.Example.json`).
- [ ] Swagger con ruta absoluta y versión de Swashbuckle compatible con OpenApi (6.x/9.x vs 10.x).
- [ ] `dotnet build` sin errores ni warnings.
- [ ] Tests de integración + HTTP cubriendo los códigos del contrato (200/201/400/401/403/404/429/500); limpieza por ID propio; IPs únicas por test.
- [ ] Verificación real en runtime (navegador/Swagger) tras el deploy; no basta que compile.
- [ ] Documentar decisiones relevantes en `DEVELOPMENT.md` del proyecto.

---

## 15. Anti-patrones generales (resumen rápido)

| Anti-patrón | Solución |
|-------------|----------|
| Todo en un solo proyecto API | Clean Architecture por capas |
| Respuestas HTTP inconsistentes | DTO + `ProblemDetails` por defecto; envelope solo si hay requisito; nunca mezclar contratos |
| `ex.Message` al cliente en 500 | Log + mensaje genérico |
| Config inválida detectada en runtime | Fail-fast al arrancar |
| Secretos en el código/repo | Config/secrets del entorno |
| `AllowAnyOrigin` / `SetIsOriginAllowed(_=>true)` | Allow-list explícita |
| Hash sin salt o MD5/SHA para contraseñas | PBKDF2 con salt (KDF) |
| Comparar credenciales con `==` | `CryptographicOperations.FixedTimeEquals` |
| `JwtSecurityTokenHandler` para generar tokens | `JsonWebTokenHandler` |
| Lógica de login en un SP | Consultas de lectura en C#; SPs solo para escrituras críticas con contrato |
| Token sensible en query string (`?token=...`) | Header (X-Api-Key, X-SqlToken...) |
| Rate limit global sin partición por IP | `RateLimitPartition` + partición por IP |
| Timer para limpiar/expirar lockout | `TimeProvider` + lazy cleanup |
| Fire-and-forget / `async void` | `async Task` + manejo central |
| `catch {}` vacío | Log + estado visible en UI |
| `HttpClient` nuevo por llamada | Singleton inyectado |
| SSL bypass en el cliente | Trust del SO; nunca `_ => true` |
| Métodos async sin `CancellationToken` | Propagar `CancellationToken` en toda la cadena |
| `Random` para IPs/IDs de tests | Contador secuencial / `RandomNumberGenerator` |
| Migrar de versión sin revisar breaking changes | Verificar matriz de versiones (p.ej. OpenApi 2.x, MTP en .NET 10) antes del upgrade |

---

## 16. Referencias del patrón validado

- **Repo de referencia**: `WebApiCore` (Clean Architecture + Dapper + JWT + ApiKey + rate limit + lockout + envelope + security headers + health) — portado a .NET 10 y operativo, con 64/64 tests y verificación runtime. Usa el envelope `ApiResponse<T>` por decisión histórica (contrato consumido por su cliente); en proyectos nuevos se recomienda DTO + `ProblemDetails`.
- **Migration .NET 10**: `PasswordManager_.NET10` — incluye `dotnet test --project` (MTP), xUnit v3, tests HTTP con `WebApplicationFactory`.
- **Config de despliegue**: connection string de producción por entorno, fail-fast, CORS por allow-list.
- **Documentación**: ver `DEVELOPMENT.md` del proyecto para decisiones de diseño y alternativas descartadas.