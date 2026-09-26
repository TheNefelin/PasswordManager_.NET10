# SKILL: .NET (C#) — Patrón Senior (transversal)

Guía de referencia para construir APIs REST en **.NET (ASP.NET Core) + Dapper + SQL Server** siguiendo una arquitectura y convenciones senior validadas en producción (`WebApiCore`, Clean Architecture). Es **transversal**: los ejemplos son genéricos (auth, CRUD, manejo de errores, configuración) y aplican a cualquier dominio. Cubre también buenas prácticas para clientes **MAUI** (MVVM) porque comparten las mismas reglas de C#, seguridad y testing.

Este archivo es un **skill**: se lee para replicar el patrón en cualquier proyecto .NET nuevo. No es una receta dogmática; es la lista de decisiones que convierten un CRUD simple en un backend mantenible, seguro y desplegable.

---

## 1. ¿Por qué este patrón es SENIOR?

Porque resuelve los problemas que matan a las APIs .NET cuando crecen, con decisiones **justificadas**, no por moda:

| Decisión | Problema que resuelve |
|----------|----------------------|
| **Clean Architecture por capas** (`Domain` → `Application` → `Infrastructure` → `API`) | Dependencias en una sola dirección; la API no conoce repositorios, los servicios no conocen Dapper. Cambiar de ORM o de BD no toca la capa de aplicación |
| **Contrato v2: DTO plano + `ProblemDetails`** | Los `2xx` devuelven el DTO sin envoltorio y los errores salen como `ProblemDetails` (RFC 9457) con `traceId`. Un solo patrón de consumo en el cliente, sin duplicar el status HTTP dentro del body |
| **`GlobalExceptionHandler` → 500 genérico + `ProblemDetails`** | El detalle real de la excepción va al log, nunca al cliente. Sin fuga de stack traces ni internos |
| **Fail-fast de configuración** | Config inválida (connection string faltante, CORS vacío, JWT sin sección) → excepción al arrancar, no fallas en runtime difíciles de diagnosticar |
| **Connection string por entorno** (`Development` → `SqlServer`, resto → `SqlServerWeb`) | El mismo código corre en local y en producción sin tocar el repositorio; la config correcta la decide el entorno |
| **JWT identifica + `ApiKey` global** | Separa "quién puede llamar a la API" (ApiKey del origen, validada contra BD) de "quién es el usuario" (JWT) |
| **Rate limiting por cliente (`X-Forwarded-For` → IP)** | Protección de fuerza bruta que no bloquea a todos los usuarios por igual |
| **Contraseñas con PBKDF2 (KDF)** | Hash seguro con salt e iteraciones configurables; nunca almacenar texto plano ni MD5/SHA simples |
| **Dapper + SQL parametrizado (sin SP)** | La lógica de negocio vive en Application y el repositorio solo ejecuta SQL parametrizado: auditable, testeable y sin el acoplamiento de un contrato de SP |
| **Tests de integración con BD real** | Validan el flujo completo (DTO → SQL → respuesta) contra la base real, no contra mocks que mienten |
| **Sin secretos en el código** | Connection strings, claves JWT y ApiKeys van en configuración/secrets del entorno, nunca hardcodeadas ni en el repo |

---

## 2. Stack recomendado

| Capa | Tecnología | Nota |
|------|-----------|------|
| API | ASP.NET Core (net8/net9/net10 según contexto) | Web API con Controllers, no minimal API para CRUD corporativo |
| ORM | **Dapper** + `Microsoft.Data.SqlClient` | Ligero, explícito, sin tracking |
| BD | SQL Server (sin SP) | Tablas, índices y seed; la lógica de negocio va en Application |
| Auth | JWT (Microsoft.AspNetCore.Authentication.JwtBearer) + ApiKey propio | Filter/attr |
| Rate limiting | ASP.NET Core RateLimiter | Fixed window + partition por IP |
| Logs | ILogger + GlobalExceptionHandler | Sin librería de terceros necesaria |
| Tests | xUnit + WebApplicationFactory | Integración contra BD real |
| Serialización | System.Text.Json | CamelCase, sin ciclos |
| Documentación | Swagger/Swashbuckle | Versionado compatible (ver §9) |

---

## 3. Estructura de carpetas (Clean Architecture)

```
WebApiCore.sln
├── WebApiCore.Domain/          # Modelos, DTOs, entidades (sin dependencias)
│   └── Models/                 # e.g. User, LoginRequest (DTOs planos, sin envelope)
├── WebApiCore.Application/     # Servicios y lógica de negocio
│   └── Services/               # e.g. UserService, AuthService
├── WebApiCore.Infrastructure/  # Acceso a datos (Dapper, context)
│   ├── Repositories/
│   ├── Context/                # IDapperContext
│   └── Mappings/
└── WebApiCore/                 # API (Program.cs, Controllers, Filters)
    ├── Controllers/
    ├── Filters/                # ApiKeyOperationFilter, AuthorizeOperationFilter, GlobalExceptionHandler
    ├── Middleware/
    ├── Models/
    └── appsettings.json        # + appsettings.{Environment}.json
```

Reglas de dependencia (una sola dirección):
- `Domain` no conoce a nadie.
- `Application` conoce a `Domain`.
- `Infrastructure` conoce a `Application` (y `Domain`).
- `WebApiCore` (API) conoce a todos, pero **nadie la conoce a ella**.

**Anti-patrón clásico**: un solo proyecto `WebApiCore` con `Models/Repositories/Services/Controllers` todos juntos. Funciona el primer año; luego las dependencias se mezclan y migrar de ORM o de BD rompe todo.

---

## 4. Capa de datos (Dapper + SQL parametrizado)

### Contexto (IDapperContext)

```csharp
public interface IDapperContext
{
    string ConnectionString { get; }
}
```

Implementación que toma la connection string del entorno activo:

```csharp
public class DapperContext : IDapperContext
{
    public string ConnectionString { get; }

    public DapperContext(IConfiguration config, IHostEnvironment env)
    {
        // Fail-fast: si falta, la API no arranca.
        var conn = env.IsDevelopment()
            ? config.GetConnectionString("SqlServer")
            : config.GetConnectionString("SqlServerWeb");

        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("No se encontró ConnectionStrings:SqlServer/SqlServerWeb");
        ConnectionString = conn;
    }
}
```

### SQL parametrizado en el repositorio (sin stored procedures)

El proyecto **no usa SP**. El repositorio ejecuta SQL parametrizado con Dapper y devuelve un **enum de resultado** cuando la operación puede fallar por una razón de negocio conocida (por ejemplo, un email duplicado). Los errores que no dependen de la BD (validación, credenciales, sesión) nunca se resuelven en esta capa: los lanza Application.

```csharp
public enum UserCreationStatus
{
    Created,
    EmailAlreadyExists
}
```

```csharp
public async Task<UserCreationStatus> CreateUserAsync(AuthUser authUser, CancellationToken cancellationToken)
{
    var commandDefinition = new CommandDefinition(
        commandText: @"
            INSERT INTO Auth_Users
                (User_Id, Email, HashLogin, SaltLogin, Profile_Id)
            VALUES
                (@User_Id, @Email, @HashLogin, @SaltLogin, 2)",
        parameters: new
        {
            authUser.User_Id,
            authUser.Email,
            authUser.HashLogin,
            authUser.SaltLogin
        },
        cancellationToken: cancellationToken);

    using var connection = _dapper.CreateConnection();

    try
    {
        await connection.ExecuteAsync(commandDefinition);
        return UserCreationStatus.Created;
    }
    catch (SqlException exception) when (exception.Number is 2601 or 2627)
    {
        // 2601 = índice único violado, 2627 = restricción UNIQUE
        return UserCreationStatus.EmailAlreadyExists;
    }
}
```

Y el servicio de Application traduce ese resultado en la excepción que el middleware sabe mapear:

```csharp
var result = await _authUserRepository.CreateUserAsync(authUser, cancellationToken);
if (result == UserCreationStatus.EmailAlreadyExists)
    throw new DuplicateEmailException();

return new AuthUserResponse { User_Id = authUser.User_Id };
```

**Reglas**:
- Los repositorios reciben datos ya procesados (por ejemplo, el hash calculado en Application); la capa de datos no aplica lógica de negocio.
- El repositorio **no lanza excepciones de negocio**: devuelve un enum/valor para los casos esperados y deja que Application decida el contrato HTTP.
- `SqlException` se filtra por número de error (`when (exception.Number is 2601 or 2627)`), nunca se captura de forma genérica para inventar un error de usuario.
- Siempre `CommandDefinition` con `cancellationToken`, para que la cancelación viaje hasta SQL.
- Nunca concatenar valores en el `commandText`: todo por nombre (`@Email`).

---

## 5. Contrato uniforme de respuesta (contrato v2)

**Regla del proyecto: los `2xx` devuelven el DTO plano y los errores salen siempre como `ProblemDetails` (RFC 9457).** No existe ningún envelope propio (`ApiResponse<T>`, `ServiceResult<T>`): el status HTTP ya viaja en el status line, y duplicarlo dentro del body solo crea dos fuentes de verdad que se desincronizan.

Controladores: sin envoltura, sin `if (!result.IsSuccess)`.

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
{
    var user = await _userService.GetByIdAsync(id, cancellationToken);

    return user is null
        ? NotFound()
        : Ok(user);
}
```

Los errores no se construyen en el controller: la capa de Application lanza una excepción y el `GlobalExceptionHandler` la traduce.

```csharp
[HttpPost]
public async Task<IActionResult> Create(UserRequest request, CancellationToken cancellationToken)
{
    var response = await _userService.CreateAsync(request, cancellationToken);

    return StatusCode(StatusCodes.Status201Created, response);
}
```

### Forma del cuerpo de error

```json
{
  "type": null,
  "title": "Conflicto",
  "status": 409,
  "detail": "El correo ya está registrado.",
  "traceId": "0HN7A1B2C3D4E5F6G7H8"
}
```

- `Content-Type: application/problem+json`.
- `traceId` va siempre en `extensions` (desde `HttpContext.TraceIdentifier`) para correlacionar el error con el log del servidor.
- El `detail` de un 500 **nunca** es `ex.Message`: es genérico y el detalle real va al log.

### Tabla de códigos coherente

| Caso | HTTP | Dónde se produce |
|------|------|------------------|
| Éxito | 200/201/204 | Controller (`Ok`, `Created`, `NoContent`) |
| Entrada inválida (modelo) | 400 | `InvalidModelStateResponseFactory` → `ValidationProblemDetails` |
| Entrada inválida (negocio) | 400 | Excepción de Application (`RequestValidationException`, `CorePasswordAlreadyExistsException`) |
| No autenticado / sesión inválida | 401 | `InvalidCredentialsException`, `UserSessionInvalidException`, `CorePasswordNotConfiguredException`, `JwtBearerEvents.OnChallenge`, `ApiKeyFilter` |
| Sin permiso (registro deshabilitado) | 403 | `RegistrationDisabledException` |
| No existe | 404 | `KeyNotFoundException`, `MapFallback` |
| Conflicto | 409 | `DuplicateEmailException` |
| Demasiadas solicitudes | 429 | `TooManyLoginAttemptsException`, `OnRejected` del rate limiter, `ApiKeyFilter` |
| Error interno | 500 | `GlobalExceptionHandler` (genérico) |

**Anti-patrón**: inventar un envelope propio con `isSuccess`/`statusCode`/`data` cuando el status HTTP ya lo dice, o devolver en unos endpoints DTO plano y en otros un `string` de error. El cliente termina con `if (status === 400) ... else if (typeof res === 'string') ...`.

---

## 6. Manejo de errores global

### `GlobalExceptionHandler`

Un solo manejador central mapea las **excepciones de Application a su status code** y responde `ProblemDetails`. Las excepciones de negocio se loguean como warning (son rechazos esperados, no fallas); el resto como error. El 500 nunca expone `ex.Message`:

```csharp
public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        if (ex is RequestValidationException
            or RegistrationDisabledException
            or DuplicateEmailException
            or TooManyLoginAttemptsException
            or InvalidCredentialsException
            or UserSessionInvalidException
            or CorePasswordAlreadyExistsException
            or CorePasswordNotConfiguredException)
            _logger.LogWarning(ex, "Solicitud rechazada.");
        else
            _logger.LogError(ex, "Excepción no controlada.");

        var (statusCode, title, message) = ex switch
        {
            RequestValidationException => (StatusCodes.Status400BadRequest, "Solicitud incorrecta", ex.Message),
            RegistrationDisabledException => (StatusCodes.Status403Forbidden, "Acceso denegado", ex.Message),
            DuplicateEmailException => (StatusCodes.Status409Conflict, "Conflicto", ex.Message),
            TooManyLoginAttemptsException => (StatusCodes.Status429TooManyRequests, "Demasiadas solicitudes", ex.Message),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "No autorizado", ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "Error", "Ocurrió un error inesperado.")
        };

        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails { Status = statusCode, Title = title, Detail = message };
        problem.Extensions["traceId"] = ctx.TraceIdentifier;

        await JsonSerializer.SerializeAsync(
            ctx.Response.Body,
            problem,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken: ct);

        return true;
    }
}
```

Registro (**`AddProblemDetails()` no es opcional**: sin él, `ProblemDetails` no se formatea con el content type correcto):

```csharp
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
// ...
app.UseExceptionHandler();
```

**Regla**: el mensaje de 500 es genérico para el cliente; el detalle real va al log. Nunca `ex.Message` en una respuesta 500.

**Por qué excepciones y no un resultado con error**: un `ServiceResult` obliga a que cada controller recuerda desenvolver (`if (!result.IsSuccess)`) y ese olvido no rompe el build — falla en runtime. La excepción hace que el camino de éxito sea el único camino posible y que el mapeo a HTTP esté en un solo archivo.

### Error de modelo (400) uniforme

```csharp
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(opts =>
    {
        opts.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(
                context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Solicitud incorrecta",
                Detail = "Validación fallida."
            };

            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

            return new BadRequestObjectResult(problem)
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    });
```

`ValidationProblemDetails` (no `ProblemDetails` plano) para que los errores por campo queden en `errors`, que es lo que el cliente muestra junto al input.

---

## 7. Autenticación y autorización

### Contraseñas (PBKDF2)

```csharp
public static string HashPassword(string password)
{
    byte[] salt = RandomNumberGenerator.GetBytes(16);
    byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
        password, salt, 10000, HashAlgorithmName.SHA256, 32);
    return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
}
```

**Nunca**: texto plano, MD5, SHA1, o el mismo hash para todos los usuarios (sin salt).

### JWT

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true,
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(config["JWT:Key"]!)),
            ValidIssuer = config["JWT:Issuer"],
            ValidAudience = config["JWT:Audience"]
        };
        // 401 uniforme si falla el token
        opts.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/problem+json";
                return JsonSerializer.SerializeAsync(
                    ctx.Response.Body,
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "No autorizado",
                        Detail = "No autorizado."
                    },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web),
                    cancellationToken: ctx.HttpContext.RequestAborted);
            }
        };
    });
```

### ApiKey global (separada del JWT)

El origen (Swagger, Postman, frontend) envía la ApiKey configurada en BD (`Mae_Config`). Se valida en un filtro de acción:

```csharp
public class ApiKeyFilter : IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "ApiKey";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var clientIp = ClientIpResolver.Resolve(context.HttpContext);

        // 1. Lockout primero: una IP quemada no llega ni a comparar la ApiKey
        if (_lockoutService.IsBlocked(clientIp))
        {
            var remaining = _lockoutService.GetRemainingBlockTime(clientIp);
            if (remaining is TimeSpan remainingTime)
                context.HttpContext.Response.Headers.RetryAfter = ((int)remainingTime.TotalSeconds).ToString();

            context.Result = CreateProblemResult(context.HttpContext, StatusCodes.Status429TooManyRequests, "Demasiadas solicitudes", "Demasiados intentos fallidos de ApiKey. Intenta nuevamente más tarde.");
            return;
        }

        // 2. Header presente
        var apiKey = context.HttpContext.Request.Headers[ApiKeyHeaderName].FirstOrDefault();

        if (string.IsNullOrEmpty(apiKey))
        {
            _lockoutService.RegisterFailure(clientIp);
            context.Result = CreateProblemResult(context.HttpContext, StatusCodes.Status401Unauthorized, "No autorizado", "ApiKey es requerida.");
            return;
        }

        // 3. Validación en tiempo constante contra el valor de Mae_Config
        var isValid = await _maeConfigService.ValidateApiKey(apiKey, context.HttpContext.RequestAborted);

        if (!isValid)
        {
            _lockoutService.RegisterFailure(clientIp);
            context.Result = CreateProblemResult(context.HttpContext, StatusCodes.Status401Unauthorized, "No autorizado", "ApiKey no autorizada.");
            return;
        }

        _lockoutService.Reset(clientIp);
        await next();
    }

    private static ObjectResult CreateProblemResult(HttpContext httpContext, int statusCode, string title, string detail)
    {
        var problem = new ProblemDetails { Status = statusCode, Title = title, Detail = detail };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }
}
```

**Reglas**:
- La ApiKey viaja en header, **nunca en la query string** (queda en logs del servidor y del proxy).
- La comparación es en **tiempo constante** (`CryptographicOperations.FixedTimeEquals`): comparar con `==` filtra información por temporización.
- El lockout se aplica **por IP**, no global: una ApiKey robada no debe tumbar a todos los clientes. Se consulta **antes** de validar y se resetea al primer éxito.
- Cada respuesta del filtro (401 y 429) es `ProblemDetails` con `traceId`, igual que el resto de la API.

---

## 8. Rate limiting y CORS

### Rate limiting por cliente

```csharp
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = 429;
    opts.AddFixedWindowLimiter("fixed", lim =>
    {
        lim.PermitLimit = 10;
        lim.Window = TimeSpan.FromMinutes(1);
        lim.QueueLimit = 0;
        lim.PartitionKey = PartitionKey.Get<HttpContext>(ctx =>
        {
            // detrás de proxy/load balancer
            var fwd = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(fwd)
                ? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                : fwd;
        });
    });
});
// ...
app.UseRateLimiter();
// en controladores sensibles:
[EnableRateLimiting("fixed")]
```

**Anti-patrón**: rate limit global sin partición por IP → un solo usuario abusivo bloquea a todos, o la regla no aplica tras un proxy porque todos llegan con la IP del balanceador.

### CORS (allow-list explícita)

```csharp
var cors = config.GetSection("Cors").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy("Default", p =>
    p.WithOrigins(cors).AllowAnyHeader().AllowAnyMethod()));
```

**Regla**: `WithOrigins` con lista explícita. **Nunca** `SetIsOriginAllowed(_ => true)` ni `AllowAnyOrigin()` en producción, salvo API pública documentada.

---

## 9. Configuración y fail-fast

`appsettings.json` (producción) y `appsettings.Development.json` (local) por separado:

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=LOCAL;Database=...;Integrated Security=True",
    "SqlServerWeb": "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True"
  },
  "JWT": { "Key": "...", "Issuer": "...", "Audience": "...", "ExpireMinutes": 60 },
  "Cors": [ "https://origen-frontend" ],
  "RateLimit": { "PermitLimit": 10, "WindowMinutes": 1 }
}
```

Fail-fast: validar la config requerida **al arrancar**, no al primer request:

```csharp
if (string.IsNullOrWhiteSpace(config["JWT:Key"])) throw new InvalidOperationException("JWT:Key no configurada");
```

**Regla de oro**: un servidor que arranca con config inválida es un bug silencioso; uno que lanza excepción es un bug evidente.

**Nunca** commitear `appsettings.json` con secretos reales. Usar `.env`/secrets del proveedor o variables de entorno y mantener en el repo una plantilla `appsettings.Example.json`.

---

## 10. Swagger / OpenAPI (compatibilidad de versiones)

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
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebApiCore v1"));
```

**Gotchas reales detectados**:
- Si `index.html`/`index.js` de Swagger se sirven con `Cache-Control` largo, el navegador cachea la UI vieja y muestra errores como *"does not specify a valid version field"* aunque el JSON sea válido. Probar con **Ctrl+Shift+R** (hard reload) o incógnito antes de concluir que el spec está roto.
- Validar el spec en `/swagger/v1/swagger.json` directamente, no la página HTML.
- Si Swashbuckle 9.x da problemas de render (UI rota en producción), **bajar a 6.6.2** es una solución validada que conserva todo el spec.

---

## 11. MAUI (clientes .NET multiplataforma)

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
| `System.Timers.Timer` tocando la UI desde otro hilo | Race conditions / crashes de UI | `PeriodicTimer` (async) o `MainThread.InvokeOnMainThreadAsync` |
| ViewModel Singleton con estado global compartido | Estado corrupto entre páginas | ViewModel Transiente, servicios como singletons |
| DataTriggers para estado visual binario | No revierten estilo base en MAUI | `IValueConverter` con Binding directo |
| Clicks rápidos en navegación crean múltiples instancias | Re-entrancy en `PushAsync` | Guard `IsBusy` + `InvertedBoolConverter` en `IsEnabled` de botones |
| `async void OnNavigatedTo` con `await` de sensores | Crash al navegar fuera durante el `await` | `OnNavigatedTo` síncrono + fire-and-forget seguro con try/catch |
| Suscripciones duplicadas a eventos de sensores Singleton | Event handlers apuntando a VMs destruidas → crash | Unsubscribe antes de Subscribe + antes de Stop() en cleanup |

### Reglas MAUI senior
- **MVVM**: ViewModel por página, `partial properties` con `[ObservableProperty]` (requiere `<LangVersion>preview</LangVersion>` en csproj), `[RelayCommand]`.
- **Inyección de dependencias** (DI nativa de MAUI): servicios en `MauiProgram`, páginas/VM resueltas por DI.
- **Nunca** lógica de negocio en `code-behind`; solo eventos de UI delegando a comandos.
- **HttpClient singleton + auth** con handlers que agregan JWT/ApiKey.
- Tratar la migración/refactor como un **proyecto de auditoría**: leer el análisis previo (por ejemplo `ANALISIS_V1.md`) y corregir los hallazgos uno a uno con aprobación del usuario (regla de issues).
- Localización por resx + markup `{extensions:Translate}` (§11.9), versionado solo en `.csproj` (§11.10), audio elegido por caso de uso (§11.11), colores con `AppThemeBinding` (§11.12), permisos Android por mínimo privilegio con APIs sin permisos protegidos (§11.13) y empaquetado Android por `RuntimeIdentifiers` (§11.14).

### 11.1 Lifecycle de MAUI Shell

El orden de vida de una Page en MAUI Shell es:

```
Constructor → OnNavigatedTo → OnAppearing
        ↑                         ↓
        |                   (page visible)
        |                         ↓
        ←←←←←←←← OnDisappearing (page hidden)
```

**Regla crítica**: `OnAppearing` se ejecuta DESPUÉS de `OnNavigatedTo`. Cualquier lógica que dependa del BindingContext debe ir en `OnNavigatedTo`, no en `OnAppearing`.

```csharp
// CORRECTO
protected override void OnNavigatedTo(NavigatedToEventArgs args)
{
    base.OnNavigatedTo(args);
    BindingContext = _serviceProvider.GetRequiredService<MyViewModel>();
    // Inicializar servicios aquí, donde BindingContext ya existe
}

// INCORRECTO — BindingContext puede ser null
protected override void OnAppearing()
{
    base.OnAppearing();
    if (BindingContext is MyViewModel vm)
    {
        vm.Initialize(); // BindingContext no seteado aún
    }
}
```

### 11.2 Singleton vs Transient en DI

| Componente | Lifetime correcto | Justificación |
|------------|-------------------|---------------|
| **Services stateless** (wrappers de APIs de plataforma, sensores, idioma) | Singleton | Sin estado mutable persistente; seguros de compartir |
| **Services con estado por instancia** (audio de instrumentos, metrónomo) | Transient | Estado aislado por ViewModel/página; evita estado residual entre navegaciones |
| **ViewModels** | Transient | Fresh instance en cada navegación, sin estado residual |
| **Pages** | Singleton (Shell) | Shell cachea las ShellContent pages |
| **AppShell** | Singleton | Shell infrastructure |

```csharp
// En MauiProgram.cs
builder.Services
    // Services sin estado — Singleton
    .AddSingleton<ILanguageService, LanguageService>()
    .AddSingleton<IThemeService, ThemeService>()
    .AddSingleton<IAppInfoService, AppInfoService>()
    .AddSingleton<ILauncherService, LauncherService>()
    // Services con estado por instancia — Transient
    .AddTransient<IMetronomeService, MetronomeService>()
    // ViewModels — Transient
    .AddTransient<HomeViewModel>()
    .AddTransient<SettingsViewModel>()
    // Pages — Singleton (Shell)
    .AddSingleton<HomePage>()
    .AddSingleton<SettingsPage>();
```

**Por qué Singleton en Pages**: Shell crea y cachea las ShellContent pages. Si la Page fuera Transiente, Shell la crearía de nuevo en cada navegación, lo cual es innecesario y rompe el estado de la UI.

**Por qué Transient en ViewModels**: Un ViewModel Singleton mantiene estado entre navegaciones (valores de sliders, campos de formulario, lecturas de sensores). Transient garantiza estado limpio cada vez que el usuario navega a una página.

### 11.3 Shell navigation: ShellContent vs Pushed pages

MAUI Shell tiene dos tipos de navegación con comportamientos diferentes:

**ShellContent pages** (tabs, páginas raíz):
```xml
<!-- En AppShell.xaml -->
<TabBar>
    <ShellContent
        ContentTemplate="{DataTemplate pages:HomePage}"
        Route="HomePage" />
    <ShellContent
        ContentTemplate="{DataTemplate pages:SettingsPage}"
        Route="SettingsPage" />
</TabBar>
```
- Shell las crea una vez y las cachea
- `OnNavigatedTo` solo fires la primera vez
- VM se resuelve en el constructor

**Pushed pages** (navegación detallada):
```csharp
// Desde un ViewModel o code-behind
var page = _serviceProvider.GetRequiredService<DetailPage>();
await Shell.Current.Navigation.PushAsync(page);
```
- Shell crea una nueva instancia en cada PushAsync
- `OnNavigatedTo` fires en cada navegación
- VM se resuelve en `OnNavigatedTo`

```csharp
// ShellContent page — VM en constructor
public partial class HomePage : ContentPage
{
    public HomePage(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        BindingContext = serviceProvider.GetRequiredService<HomeViewModel>();
    }
}

// Pushed page — VM en OnNavigatedTo
public partial class DetailPage : ContentPage
{
    private readonly IServiceProvider _serviceProvider;

    public DetailPage(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        BindingContext = _serviceProvider.GetRequiredService<DetailViewModel>();
        // Inicializar servicios aquí
    }
}
```

### 11.4 DI en Pages: IServiceProvider pattern vs Constructor DI

**Opción A — IServiceProvider pattern** (cuando el VM es Transient y la Page es Singleton):

```csharp
public partial class MyPage : ContentPage
{
    private readonly IServiceProvider _serviceProvider;

    public MyPage(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        BindingContext = _serviceProvider.GetRequiredService<MyViewModel>();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is MyViewModel vm)
        {
            vm.Cleanup();
        }
    }
}
```

**Opción B — Constructor DI** (cuando el VM se crea una vez y se reusa, o cuando el VM usa state services que persisten):

```csharp
public partial class MyPage : ContentPage
{
    public MyPage(MyViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
```

**Opción B es válida cuando** el VM delega estado a un servicio Singleton (como `IStateService`) que persiste entre recreaciones de VM. El VM sigue siendo Transient, pero su estado sobrevive en el servicio. El estado mutable de dominio que debe sobrevivir a la navegación (p. ej. marcas de cronómetro, lecturas de sensores) vive en el servicio Singleton; el VM Transient lo refleja en su `Initialize()`.

**Regla**: si el VM necesita Cleanup() al salir, usar Opción A. Si el estado vive en un servicio Singleton, Opción B es suficiente.

### 11.5 BindableProperty para components

Cuando un `ContentView` necesita dependencias (servicios), no puede usar DI por constructor. Usar `BindableProperty`:

```csharp
public partial class MyComponent : ContentView
{
    private IMyService _service;

    public static readonly BindableProperty ServiceProperty =
        BindableProperty.Create(nameof(Service), typeof(IMyService),
            typeof(MyComponent), null, propertyChanged: OnServiceChanged);

    public IMyService Service
    {
        get => (IMyService)GetValue(ServiceProperty);
        set => SetValue(ServiceProperty, value);
    }

    private static void OnServiceChanged(BindableObject bindable,
        object oldValue, object newValue)
    {
        if (bindable is MyComponent component && newValue is IMyService service)
        {
            component._service = service;
        }
    }
}
```

En XAML, bindear desde el Page:
```xml
<components:MyComponent
    Service="{Binding Source={x:Reference MyPage}, Path=BindingContext.MyService}" />
```

**Regla**: nunca usar `IPlatformApplication.Current.Services.GetService<>()` directamente en un ContentView. Siempre BindableProperty + binding desde el Page.

### 11.6 Core + MAUI: separación de responsabilidades (transversal)

Aplica a CUALQUIER app MAUI nueva — una app de notas, un password manager, un toolkit. Dividir siempre en dos proyectos:

```
MiApp.Core/                   # Class Library (net10.0, sin MAUI)
├── Interfaces/               # Contratos de servicios (testables)
├── Models/                   # Modelos de dominio
├── Services/                 # Servicios puros (sin APIs de plataforma)
└── ViewModels/               # Todas las VMs (testables con xUnit)

MiApp/                        # Proyecto MAUI
├── Services/
│   └── Implementations/      # Implementaciones con APIs de plataforma
│       ├── LanguageService.cs
│       ├── ThemeService.cs
│       ├── AppInfoService.cs
│       └── ... (una por interfaz; SOLO estas conocen Microsoft.Maui)
└── Pages/                    # Páginas y Views
```

**Va a Core**: interfaces, modelos, `BaseViewModel` y servicios que solo usan `System.*` y tipos standard.
**Se queda en MAUI**: servicios que usan `Flashlight.Default`, `Compass.Default`, `MediaElement`, `Window.Attributes`, `AppInfo`, `Launcher`, `Preferences` u otras APIs de plataforma.

**Regla**: si un servicio importa `Microsoft.Maui` o `CommunityToolkit.Maui`, se queda en MAUI. Si solo usa `System.*`, va a Core. La decisión no la da el tema de la app sino **la dependencia**.

**Por qué dos proyectos**: las interfaces y VMs en Core se testean con xUnit sin inicializar MAUI (sin UI thread ni handlers de plataforma); el proyecto MAUI queda como adaptador de plataforma. La regla es idéntica en cualquier app.

### 11.7 Timer moderno en MAUI

`System.Timers.Timer` ejecuta en thread pool y requiere marshaling manual. Usar `PeriodicTimer` (.NET 6+):

```csharp
// MAL — thread pool, requiere MainThread.BeginInvokeOnMainThread
private System.Timers.Timer _timer;
_timer = new System.Timers.Timer(1000);
_timer.Elapsed += (s, e) => UpdateUI(); // Crash: no es UI thread

// BIEN — async, aware del lifecycle
private CancellationTokenSource _cts;

public void Start()
{
    _cts = new CancellationTokenSource();
    _ = RunTimerAsync(_cts.Token);
}

private async Task RunTimerAsync(CancellationToken ct)
{
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
    try
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            MainThread.BeginInvokeOnMainThread(() => UpdateUI());
        }
    }
    catch (OperationCanceledException) { }
}

public void Stop()
{
    _cts?.Cancel();
    _cts?.Dispose();
    _cts = null;
}
```

**Ventajas de `PeriodicTimer`**: async-aware, cancellation nativo, sin threads adicionales, se detiene limpiamente con `CancellationToken`.

**Matiz de precisión — NO compensa deriva**: `PeriodicTimer` espera el intervalo **desde que terminó el tick anterior**, por lo que la deriva se acumula (el delay se mide desde el fin de procesar cada tick, incluyendo marshaling). Para secuencias rítmicas que deben ser exactas (metrónomo, secuenciadores) no basta: programar contra **tiempos absolutos** con reloj monotónico:

```csharp
var sw = Stopwatch.StartNew();
long intervalTicks = TimeSpan.FromMilliseconds(60000.0 / bpm).Ticks;
long next = sw.Elapsed.Ticks;

while (!ct.IsCancellationRequested)
{
    next += intervalTicks;
    var remaining = TimeSpan.FromTicks(next - sw.Elapsed.Ticks);
    if (remaining > TimeSpan.Zero)
        await Task.Delay(remaining, ct);

    // ejecutar el beat — SIN esperar la duración de la reproducción
    PlayBeat();
}
```

Cada tick se programa contra el inicio absoluto (`sw.Elapsed`), eliminando deriva acumulada. El jitter por tick restante depende del motor de playback: ver §11.11.

### 11.8 Convertidores para estado visual

Para estado visual binario (on/off, active/inactive), usar `IValueConverter` con Binding directo en vez de DataTriggers:

```xml
<!-- INCORRECTO — DataTriggers no revierten estilo base en MAUI -->
<Button.Triggers>
    <DataTrigger TargetType="Button" Binding="{Binding IsOn}" Value="True">
        <Setter Property="BackgroundColor" Value="{StaticResource MyAccent}" />
    </DataTrigger>
</Button.Triggers>

<!-- CORRECTO — Binding directo con converter -->
<Button BackgroundColor="{Binding IsOn, Converter={StaticResource OnOffConverter}}" />
```

**Regla**: los DataTriggers de MAUI tienen un bug conocido donde no revierten el estilo base correctamente al desactivarse. Usar `IValueConverter` con Binding directo es más confiable.

### 11.9 Localización multi-idioma (resx)

Patrón validado en el repo (ES/EN/SV):

- **Recursos**: `AppResources.resx` (idioma base) + `AppResources.{culture}.resx` (p. ej. `.en`, `.sv`).
- **Manager**: `LocalizationResourceManager` (singleton) expone un indexer por clave `[Clave]`; al cambiar de idioma notifica para re-enlazar los Bindings.
- **Markup**: `TranslateExtension` (namespace propio, `IMarkupExtension<BindingBase>`) usado como `{extensions:Translate Clave}`.

```csharp
// Devuelve un Binding enlazado al indexer del manager.
public BindingBase ProvideValue(IServiceProvider serviceProvider)
{
    return new Binding
    {
        Mode = BindingMode.OneWay,
        Path = $"[{Name}]",
        Source = LocalizationResourceManager.Instance
    };
}
```

**Gotcha verificada**: `TranslateExtension` devuelve un **`Binding`**. Usarlo SOLO en propiedades bindables (`Text`, `Title`, `ToolTip`, ...). En propiedades no enlazables (p. ej. valores estáticos, `Source`, colecciones) no aplica o falla silenciosamente.

**Reglas**:
- Un `View`/`Page` que muestre cadenas debe consumir las claves vía markup; nunca hardcodear textos visibles.
- El cambio de idioma persiste (`Preferences`) y se aplica con `CultureInfo`; las claves nuevas se agregan a TODOS los idiomas a la vez.
- Toda clave usada en XAML debe existir en el `.resx` base, o compilar con recursos conectados fallará la búsqueda en runtime.

### 11.10 Versionado de app (.csproj)

- **`ApplicationDisplayVersion`**: versión visible/marketing (`1.0.1`). Texto libre; Android/iOS aceptan `major.minor.patch`.
- **`ApplicationVersion`**: número interno de build. Las tiendas lo usan para detectar actualizaciones; **subir en CADA publicación** (debe incrementar, nunca bajar/reciclar).
- **Única fuente de verdad**: el `.csproj`. Leer en runtime con `AppInfo` (`AppInfo.Current.VersionString`/`BuildString`) vía un servicio wrapper (`IAppInfoService`) en Core + implementación en MAUI.
- **Nunca** duplicar la versión en constantes o hardcoded en la UI (p. ej. "Beta: v1.0.0"): diverge del `.csproj`.

### 11.11 Audio: efectos cortos vs música (baja latencia)

`MediaElement` (ExoPlayer/AVPlayer) y plugins como `Plugin.Maui.Audio` son correctos para música/larga duración, pero tienen **latencia relevante para clics** (el issue jfversluis/Plugin.Maui.Audio#89 documenta 150-200 ms incluso con player precargado). Regla:

| Caso de uso | Motor recomendado |
|-------------|-------------------|
| Música, reproducción larga, notas de instrumento | `MediaElement` |
| Clics/efectos cortos precisos (metrónomo) | APIs nativas de baja latencia |

APIs de baja latencia disponibles sin dependencias nuevas:

- **Android**: `Android.Media.SoundPool` — decodifica PCM a memoria al cargar (sin CPU/latencia de decompresión por reproducción); `Play()` dispara rápido. Pensado para efectos cortos.
- **iOS**: `AudioToolbox.SystemSound` — "sound plays immediately"; PCM/IMA4 `.wav` ≤ 30 s; para efectos de sonido.
- **Windows**: `MediaElement` como fallback aceptable.

**Patrón de implementación** (sin plugins):

```csharp
// Core
public interface IMetronomeClickService
{
    void PlayClick(bool accent);
}

// MAUI (implementación única por TFM con #if)
public partial class MetronomeClickService : IMetronomeClickService
{
    public void PlayClick(bool accent)
    {
#if ANDROID
        _soundPool?.Play(accent ? _accentSoundId : _normalSoundId, 1f, 1f, 1, 0, 1f);
#elif IOS || MACCATALYST
        (accent ? _accentSound : _normalSound)?.PlaySystemSound();
#else
        // fallback MediaElement
#endif
    }
}
```

**Reglas**: precargar la muestra UNA vez (no recargar por cada tick); el playback nunca debe esperarse dentro del loop del scheduler (§11.7); si el proyecto usa Core puro, la interfaz vive en Core y la implementación por plataforma en el proyecto MAUI.

### 11.12 Reuso y convenciones para biblioteca de componentes

- **Colores/estilos con `AppThemeBinding`** desde el origen: todo recurso visual declara variante claro/oscuro; nunca un color fijo para ambos temas.
- **Converters centralizados** (`BoolToColorConverter`, `BoolToLocalizedStringConverter`, `InvertedBoolConverter`): estado visual binario por binding, no por DataTriggers (§11.8).
- **Componentes autocontenidos** (1 control = 1 archivo + partial class si requiere código) con inyección por `BindableProperty` (§11.5), nunca Service Locator.
- Si hay 3+ apps MAUI que comparten estilos/converters/localización/servicios wrapper, extraerlos a una **librería compartida** (`Toolkit.Core` maUI-free + `Toolkit.Maui`) consumida por referencia de proyecto; recién evaluar NuGet cuando la distribución lo justifique.

### 11.13 Permisos Android: mínimo privilegio y APIs sin permisos protegidos

- Declarar en el manifest **solo lo mínimo**; verificar siempre el manifest **fusionado** (`obj/.../AndroidManifest.xml`) porque NuGets inyectan permisos por su cuenta (p. ej. `CommunityToolkit.Maui` agrega `INTERNET`). Para permisos *normal* (no proteger la vida del usuario ni datos), no vale la pena pelear el merge de Gradle.
- **Gotcha**: `Battery.Default` de MAUI exige en Android el permiso **`BATTERY_STATS`** (protegido `signature|privileged`, red flag en el review de Play y rechazado en políticas). No se re-agrega el permiso: se lee la API de plataforma que no lo requiere.
- Patrón de lectura sin permisos (síncrono, API 21+, `#if ANDROID` + fallback MAUI en otras plataformas):

```csharp
public static int GetBatteryLevel(Android.Content.Context ctx) =>
    ctx.GetSystemService(Android.Content.Context.BatteryService) is Android.OS.BatteryManager bm
        ? bm.GetIntProperty((int)Android.OS.BatteryProperty.Capacity)
        : -1;
```

  Compatibilidad: contra API obsoletas, el compilador suele sugerir el reemplazo (p. ej. `BatteryProperty` enum en vez de `BatteryManager.BatteryPropertyCapacity`).

### 11.14 Empaquetado Android: ABIs con `RuntimeIdentifiers`

- `AndroidSupportedAbis` quedó **obsoleta** en .NET 10 / Android SDK 36 (warning XA0036): no aplica los ABIs. Reemplazo: `RuntimeIdentifiers` (RID → ABI) limitados al target Android para no afectar Windows/MacCatalyst.

```xml
<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">
  <RuntimeIdentifiers>android-arm;android-arm64;android-x64</RuntimeIdentifiers>
</PropertyGroup>
```

- RID → ABI: `android-arm` = `armeabi-v7a` (32-bit), `android-arm64` = `arm64-v8a`, `android-x64` = `x86_64`. Un single APK cubre todos los dispositivos; Play entrega AAB y genera el APK por dispositivo.
- Validar en dispositivo real: `adb shell getprop ro.product.cpu.abi`. Dispositivos budget pueden correr **solo 32-bit** (caso real: Galaxy A11 `SM-A115M`, Android 12, `armeabi-v7a`); un APK sin ese ABI falla con "app no compatible" (`INSTALL_FAILED_NO_MATCHING_ABIS`).
- Los builds **Debug** de MAUI apuntan a `x86_64` (emulador): no instalar en teléfonos reales; firmar y probar un APK **Release**.

### 11.15 Firma, licencias y Release para Play Store

**Firma de paquete (keystore)**:
- Los secretos de firma **nunca van en el código ni en el `.csproj`**. Usar **Properties → Android Firma de Paquete** escribe `AndroidSigningKeyPass`/`AndroidSigningKeyStore` en el `.csproj`, que **se versiona** → contraseñas públicas. Preferir el flujo **Archive → Ad Hoc** de Visual Studio (perfil de firma fuera del proyecto) o pasar las properties por línea de comandos/CI (`-p:AndroidKeyStore=true -p:AndroidSigningKeyStore=... -p:AndroidSigningKeyAlias=... -p:AndroidSigningKeyPass=... -p:AndroidSigningStorePass=...`).
- El keystore (`.keystore`/`.jks`/`.p12`) se guarda **fuera del repositorio**, con backup en otro disco o gestor de contraseñas. Perderlo = no poder actualizar la app en Play. `.gitignore` debe excluir `*.keystore`, `*.jks`, `*.p12`, `*.key`.
- Validez: **mínimo 25 años** en `keytool` (`-validity 9125`); se recomienda 100 años (`36500`). El diálogo de VS pregunta validez en días.
- **Contrato**: el keystore del APK de prueba debe ser el **mismo** que el del AAB final (si no, Play rechaza la actualización).
- Play **no acepta APK**: se sube un **AAB** firmado (Release con `AndroidPackageFormat` aab por defecto; APK de prueba con `-p:AndroidPackageFormat=apk`). Activar **Play App Signing**: el keystore propio es solo la *upload key*; Google firma los APK finales.

**Licencias de componentes comerciales (ej. Syncfusion)**:
- La clave se **inyecta en build como `AssemblyMetadata`** (csproj `-p:SyncfusionLicenseKey=...` o variable de entorno de la máquina `SYNC_FUSION_LICENSE_KEY`) y se lee por reflexión en `MauiProgram.cs` solo si trae valor. **Nunca hardcodear ni versionar la clave**. Separar de la CI/CD cuando corresponda.
- Diferenciar **Trial** (30 días, genera aviso en runtime) de la **Community License** gratuita definitiva (sin expirar si se cumplen condiciones: <US$1M ingresos, ≤5 desarrolladores, ≤10 empleados). Verificar el tipo en el panel de cuentas de Syncfusion; no publicar en producción con clave trial.

---

## 12. Tests

- **xUnit + `WebApplicationFactory<T>`** para tests de integración de la API.
- Probar **contra BD real** (o instancia de prueba) para validar DTO → SQL → respuesta completa.
- Verificar el contrato: en `2xx`, el DTO plano sin envoltura; en error, `ProblemDetails` con `status`/`title`/`detail`/`traceId` y `Content-Type: application/problem+json` para 400, 401, 403, 404, 409, 429 y 500.
- Los servicios se testean por **excepción esperada** (`await Assert.ThrowsAsync<InvalidCredentialsException>(...)`) cuando el camino de error es una excepción de Application; por enum/valor devuelto cuando el caso es del repositorio.
- No mockear repositorios para probar la API: el valor está en el flujo real.
- Comando: `dotnet test` (no ejecutar sin autorización del usuario según las reglas del repo).

---

## 13. Checklist final

- [ ] Estructura Clean Architecture con dependencias en una sola dirección.
- [ ] Contrato v2 I/O: **2xx → DTO plano** (sin envelope) y **errores → `ProblemDetails`** RFC 9457 (`application/problem+json`) con `traceId` en `extensions`.
- [ ] `GlobalExceptionHandler` central: log + 500 genérico, sin fuga de internos.
- [ ] 400 uniforme vía `InvalidModelStateResponseFactory`.
- [ ] Contraseñas con PBKDF2 + salt + iteraciones configurables.
- [ ] JWT con `ClockSkew=0`, issuer/audience validados, key desde config.
- [ ] ApiKey en header (nunca en query string) validada contra BD.
- [ ] Rate limiting particionado por IP con `X-Forwarded-For`.
- [ ] CORS con allow-list explícita; sin `AllowAnyOrigin` en producción.
- [ ] Fail-fast de configuración al arrancar.
- [ ] No secretos hardcodeados ni en el repo.
- [ ] Swagger con ruta absoluta y versión de Swashbuckle compatible con OpenApi (6.x/9.x vs 10.x).
- [ ] `dotnet build` sin errores ni warnings.
- [ ] Tests de integración cubriendo los status y la forma de `ProblemDetails` (incluido `traceId`).
- [ ] Verificación real en runtime (navegador/Swagger) tras el deploy; no basta que compile.
- [ ] Documentar decisiones relevantes en `DEVELOPMENT.md` del proyecto.
- [ ] MAUI: ViewModels Transientes, Pages Singleton, VM resuelto en `OnNavigatedTo` o constructor DI con state service.
- [ ] MAUI: Lógica de inicialización en `OnNavigatedTo`, no en `OnAppearing`.
- [ ] MAUI: Components con BindableProperty, nunca Service Locator.
- [ ] MAUI: `PeriodicTimer` en vez de `System.Timers.Timer`.
- [ ] MAUI: `IValueConverter` con Binding directo en vez de DataTriggers para estado visual.
- [ ] MAUI: Guard `IsBusy` en comandos de navegación para prevenir re-entrancy.
- [ ] MAUI: `OnNavigatedTo` síncrono; fire-and-forget con try/catch si hay async, nunca `async void`.
- [ ] MAUI: Unsubscribe de eventos de sensores antes de Stop() y en cleanup para evitar callbacks post-destrucción.
- [ ] MAUI: Localización con resx + markup; una clave nueva se agrega en TODOS los idiomas a la vez.
- [ ] MAUI: Versión solo en `.csproj` (Display + Build); leída con `AppInfo`; subir `ApplicationVersion` en cada publicación.
- [ ] MAUI: Audio — efectos/clics por APIs de baja latencia (SoundPool/SystemSound); música por `MediaElement`.
- [ ] MAUI: Colores y estilos con `AppThemeBinding` (claro/oscuro desde el origen).
- [ ] MAUI: Permisos Android mínimos; usar APIs de plataforma sin permisos protegidos (batería con `BatteryManager`/`BatteryProperty`, no con `Battery.Default` + `BATTERY_STATS`); revisar el manifest fusionado.
- [ ] MAUI: Empaquetado Android con `RuntimeIdentifiers` (`AndroidSupportedAbis` obsoleta en .NET 10); validar ABI en dispositivo real con `ro.product.cpu.abi` (cuidado con 32-bit).

---

## 14. Anti-patrones generales (resumen rápido)

| Anti-patrón | Solución |
|-------------|----------|
| Todo en un solo proyecto API | Clean Architecture por capas |
| Respuestas HTTP inconsistentes | `ProblemDetails` RFC 9457 (`application/problem+json` + `traceId`) |
| Envelope propio (`ApiResponse`/`ServiceResult`) con `isSuccess`/`statusCode` duplicados | Contrato v2: DTO plano en `2xx` + `ProblemDetails` en error |
| Servicio que devuelve `ServiceResult` y el controller que "//" olvida desenvolver | Excepciones de Application + un único `GlobalExceptionHandler` |
| `ex.Message` al cliente en 500 | Log + mensaje genérico |
| Config inválida detectada en runtime | Fail-fast al arrancar |
| Secretos en el código/repo | Config/secrets del entorno |
| `AllowAnyOrigin` / `SetIsOriginAllowed(_=>true)` | Allow-list explícita |
| Hash sin salt o MD5/SHA para contraseñas | PBKDF2 con salt (KDF) |
| Fire-and-forget / `async void` | `async Task` + manejo central |
| `async void OnNavigatedTo` con await de sensores | Síncrono + fire-and-forget con try/catch |
| `catch {}` vacío | Log + estado visible en UI |
| `HttpClient` nuevo por llamada | Singleton inyectado |
| SSL bypass en el cliente | Trust del SO; nunca `_ => true` |
| Migrar de versión sin revisar breaking changes | Verificar matriz de versiones (p.ej. OpenApi 2.x) antes del upgrade |

---

## 15. Referencias del patrón validado

- **Repo de referencia**: `WebApiCore` (Clean Architecture + Dapper + JWT + ApiKey + rate limit + contrato v2 DTO plano/`ProblemDetails`) — deployado y operativo.
- **Migration .NET 10**: `PasswordManager_.NET10` — port del mismo patrón con OpenApi 2.x (Swashbuckle 10.x).
- **Config de despliegue**: connection string de producción por entorno, fail-fast, CORS por allow-list.
- **Documentación**: ver `DEVELOPMENT.md` del proyecto para decisiones de diseño y alternativas descartadas.
