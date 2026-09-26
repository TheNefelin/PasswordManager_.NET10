# DEVELOPMENT.md — Password Manager .NET 10

Documento de desarrollo del repo `D:\Repo\.NET\PasswordManager_.NET10`. Su propósito es que una sesión nueva de OpenCode recupere el contexto completo del proyecto sin depender de conversaciones previas.

## 1. Visión general

Solución (`PasswordManager_.NET10.slnx`) con **dos proyectos deployables independientes**:

| Proyecto | Qué es | Estado |
|---|---|---|
| `WebApiCore` + capas (`Application`, `Domain`, `Infrastructure`) | **API** (ASP.NET Core, .NET 10) | Port a .NET 10 de la API `WebApiCore` del repo `D:\Repo\.NET\Projects_.NET9`. Reemplazo de la v9. Con mejoras de seguridad/calidad aplicadas (secciones 3 y 7). |
| `PasswordManager_.NET10` | Cliente **MAUI** (net10.0-android/ios/maccatalyst/windows) | Evolución a nivel "senior" en curso (ítems A/B/C). Auditoría y refactor documentados en la sección 7. |

Reglas de operación: `AGENTS.md` (mismas reglas generales que en `Projects_.NET9`).

## 2. Arquitectura (Clean Architecture)

| Capa | Contenido |
|---|---|
| `WebApiCore.Domain` | Interfaces de repositorios (`IAuthUserRepository`, `ICoreUserRepository`, `ICoreDataRepository`, `IMaeConfigRepository`), entidades. Sin dependencias. |
| `WebApiCore.Application` | DTOs (incluye `CoreDataResponse`, el DTO de salida de Core), servicios de aplicación, interfaces (`IIpLockoutService`, `IAuthTokenService`, `IPasswordHasher`, etc.), excepciones de Application (`WebApiCore.Application/Common`). **`ApiResponse` y `ServiceResult` eliminados** — los servicios devuelven DTOs directos y los errores se signalizan con excepciones (contrato v2, sección 3). Referencia solo Domain. |
| `WebApiCore.Infrastructure` | Dapper + `Microsoft.Data.SqlClient`, repositorios, seguridad (`PasswordHasher` PBKDF2, `JwtTokenUtil`, `IpLockoutService`, `IpLockoutOptions`, `SqlTokenHasher` SHA-256 del token de sesión), `JwtOptions`. Referencia Application. |
| `WebApiCore` (API) | `Program.cs`, controllers (`AuthController`, `CoreController`), `Middleware/GlobalExceptionHandler`, `Filters/` (`ApiKeyFilter`, `ApiKeyOperationFilter`, `AuthorizeOperationFilter`), `Helpers/ClientIpResolver`. |

Dependencias (NuGet) **gestionadas por Central Package Management (CPM)** — las versiones se definen una sola vez en `Directory.Packages.props` (raíz del repo; ver decisión en la sección 3). `WebApiCore` usa `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.OpenApi`, `Swashbuckle.AspNetCore.SwaggerUI`. Infrastructure: `Dapper`, `Microsoft.Data.SqlClient`, `System.IdentityModel.Tokens.Jwt` (el hashing de contraseñas usa `Rfc2898DeriveBytes.Pbkdf2` del BCL, sin paquete externo; se eliminó `Microsoft.AspNetCore.Cryptography.KeyDerivation`). `WebApiCore.Tests`: `xunit.v3` + `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing`, coverlet (sin Moq; fakes/stubs manuales).

## 3. Decisiones de diseño clave

- **Contrato v1 (envelopes `ApiResponse` y `ServiceResult`) — ELIMINADOS**: toda respuesta (éxito y error) usaba `ApiResponse<T>` (`isSuccess`, `statusCode`, `message`, `data`, `errors`, `traceId`) y los servicios de Application devolvían `ServiceResult<T>` (`IsSuccess`/`ErrorMessage`/`StatusCode`) con `ToProblemResult()` en los controllers. Ambos envoltorios se eliminaron: los servicios devuelven el DTO directamente y lanzan excepciones de Application (`RequestValidationException`, `RegistrationDisabledException`, `DuplicateEmailException`, `TooManyLoginAttemptsException`, `InvalidCredentialsException`, `UserSessionInvalidException`, `CorePasswordAlreadyExistsException`, `CorePasswordNotConfiguredException`) y `KeyNotFoundException` (BCL) cuando un `UPDATE`/`DELETE` no afecta ninguna fila → `404`.
- **Contrato v2 (`ProblemDetails`, RFC 9457) — ACTIVO**: todo error sale como `application/problem+json` con `status`/`title`/`detail`/`traceId` (de `HttpContext.TraceIdentifier`) por cinco caminos: `GlobalExceptionHandler` (excepciones de Application + `400`/`401`/`404`/`409`/`500` genéricos), `InvalidModelStateResponseFactory` (400), `JwtBearerEvents.OnChallenge` (401), `OnRejected` del rate limiter (429) y `MapFallback` (404). Los status codes del contrato anterior se conservan; solo cambia la forma del cuerpo.
- **Pipeline HTTP** (orden en `Program.cs`): `UseExceptionHandler` → `UseHttpsRedirection` → `UseRateLimiter` → Swagger → `UseCors` → `UseAuthentication` → `UseAuthorization` → `MapControllers` → `MapFallback` (404 uniforme).
- **Dapper + SQL Server sin stored procedures**: el login se resuelve en C# (`GetUserByEmailAsync` + verificación del hash + token nuevo). El registro usa un `INSERT` parametrizado en `AuthUserRepository.CreateUserAsync`, que devuelve el enum `UserCreationStatus` (`Created`, `EmailAlreadyExists`); `AuthUserService.RegisterAsync` lo traduce a `AuthUserResponse` o a la excepción correspondiente. El stack no usa SP: `SqlServer.sql` solo define tablas y seed.
- **Autenticación doble**: `ApiKey` (header, validada contra `Mae_Config.ApiKey` con **comparación en tiempo constante** `CryptographicOperations.FixedTimeEquals`, filtro `ApiKeyFilter` aplicado a ambos controllers) + `JWT` Bearer.
- **Lockout dual por IP** (`IpLockoutService` genérico con `IpLockoutOptions` + `TimeProvider`, singleton): dos instancias independientes en DI — `api-key` (5 fallos / 10 min → bloqueo 1 h, usada por `ApiKeyFilter` vía `[FromKeyedServices]`) y `login` (5 fallos / 15 min → bloqueo 15 min, usada por `AuthUserService.LoginAsync`). Respuesta `429` con header `Retry-After`. Logging del bloqueo en el filtro y en el controller (los servicios no dependen de `ILogger` para no requerir el shared framework en Infrastructure/Application). La IP se resuelve con `ClientIpResolver` (compartido con el rate limiter).
- **Vínculo JWT↔SqlToken**: la identidad de los endpoints core se toma del **claim `sub` del JWT** (`ClaimTypes.NameIdentifier`), nunca del `User_Id` enviado por el cliente (que se ignora y ya no se envía). El `SqlToken` viaja en el **header `SqlToken`**, nunca en la query string. Debe pertenecer al mismo usuario del JWT, o se responde `401`. Elimina el cruce "JWT de A + SqlToken de B".
- **Token de sesión hasheado en BD**: `Auth_Users.SqlTokenHash VARCHAR(64)` = SHA-256 en hexadecimal minúscula del GUID canónico `Guid.ToString("D")` (`WebApiCore.Infrastructure/Security/SqlTokenHasher.cs`). El token crudo se genera en el login y se devuelve al cliente, pero **solo su hash se persiste**; la comparación es por hash. Al no estar en la URL, un `SqlToken` robado ya no queda registrado en logs de servidor, proxies ni historial.
- **DTO de salida `CoreDataResponse`**: la API no expone la entidad de dominio `CoreData`; se mapea en `CoreDataService` con `ToDTO`/`ToEntity` (shape 1:1 → sin cambio de contrato JSON).
- **JWT**: `sub` = `AuthUser.User_Id` (GUID inmutable), `exp` calculado con `DateTime.UtcNow`; `JwtOptions` no tiene `Subject` (era config muerta; el `sub` identifica al usuario, no a la institución).
- **Fail-fast de configuración**: si falta `ConnectionStrings`, `JWT`, o `Cors:AllowedOrigins` vacío → `InvalidOperationException` al arrancar. Es deliberado (no arranca con config inválida).
- **Connection string por entorno**: `Development` → `ConnectionStrings:SqlServer` (local `db_testing`); cualquier otro entorno → `ConnectionStrings:SqlServerWeb` (producción).
- **Rate limiting**: `client_25_per_minute`, 25 req/min por cliente, ventana fija 60 s, `QueueLimit=0` (aplica a core); **política dedicada `login_5_per_minute`** (5 req/min) en `AuthController.Login` y **`register_5_per_minute`** (5 req/min) en `AuthController.Register` para endurecer brute-force y spam de cuentas. Todas particionadas por `ClientIpResolver` (`X-Forwarded-For` primer valor → fallback `RemoteIpAddress`). Parámetros `RateLimit:PermitLimit`/`WindowSeconds`, `RateLimit:LoginPermitLimit`/`LoginWindowSeconds` y `RateLimit:RegisterPermitLimit`/`RegisterWindowSeconds`. Respuesta `429` con `ProblemDetails` vía `OnRejected`.
- **Sin caché de ApiKey**: `MaeConfigService` valida contra `Mae_Config` en **cada request**. Antes cacheaba 30 s, por lo que una ApiKey filtrada seguía siendo válida durante esa ventana; con el rate limit actual (25 req/min por cliente) la consulta de una fila por PK es despreciable. La rotación de `ApiKey` surte efecto en el siguiente request. Ya no se usa `ApiKeyCache:ExpirationSeconds` (puede quedar en `appsettings.json`, está inerte).
- **CORS configurable** desde `Cors:AllowedOrigins` (sin `SetIsOriginAllowed(_ => true)`).
- **Swagger UI en la raíz** (`RoutePrefix = ""`) documentando `/openapi/v1.json` (documento nativo de ASP.NET Core, ruta absoluta; evita el rewrite relativo de `index.js`). **Solo se mapea con `app.Environment.IsDevelopment()`**: en producción el mapa completo de la API sería público (no requiere `ApiKey`) y le sirve al atacante la lista de endpoints, headers y esquemas.
- **Central Package Management (CPM)**: `Directory.Packages.props` en la raíz del repo con `ManagePackageVersionsCentrally=true`. Los 20 `PackageReference` de los 5 proyectos quedaron **sin `Version`**; la versión única vive como `PackageVersion` en la central (16 paquetes organizados en 3 `ItemGroup` por área: `Maui`, `Api`, `Test` — los labels son solo organizativos, NuGet resuelve cualquier `PackageVersion` contra cualquier `PackageReference` del mismo id). Beneficio: bumps atómicos desde un solo archivo y cero drift entre proyectos (p. ej. `xunit.v3`, `Microsoft.NET.Test.Sdk` y `coverlet.collector` estaban duplicados en las dos suites de tests). Al instalar/actualizar, el asistente NuGet de VS (17.2+) y el CLI .NET 10 (`dotnet package add/update`) escriben la versión directamente en la central, no en el csproj. `CentralPackageTransitivePinningEnabled` **no está activado** (sin pinning transitivo). Nota: con 2 sources NuGet activos (nuget.org + "Microsoft Visual Studio Offline Packages") NuGet puede emitir **NU1507**; hoy no aparece (build 0/0); si apareciera, resolver con *package source mapping* en un `nuget.config` o dejando un solo source.

## 4. Swashbuckle 10 / Microsoft.OpenApi 2.x — breaking changes aplicados (IMPORTANTE)

Swashbuckle 10.2.3 usa **Microsoft.OpenApi 2.x**, que rompe con el patrón v1. Si se revierte algo aquí, el build falla. Fixes ya aplicados:

1. `using Microsoft.OpenApi.Models` **ya no existe** → usar `using Microsoft.OpenApi;` (los modelos viven en el namespace raíz).
2. `OpenApiSchema.Type` ya no es `string` → es el enum flag `JsonSchemaType?`. Ej: `Schema = new OpenApiSchema { Type = JsonSchemaType.String }`.
3. `OpenApiReference` eliminado y `OpenApiSecurityScheme.Reference` eliminado → usar el proxy por id: `new OpenApiSecuritySchemeReference("Bearer")`.
4. `OpenApiSecurityRequirement` ahora es `Dictionary<OpenApiSecuritySchemeReference, List<string>>` → el valor debe ser `new List<string>()`, no `Array.Empty<string>()`.
5. **Colecciones nullable** (ya no se inicializan): hay que inicializarlas antes de usar:
   - `operation.Parameters ??= new List<IOpenApiParameter>();`
   - `operation.Responses ??= new OpenApiResponses();`

Referencia: [Microsoft.OpenAPI.NET v2 upgrade guide](https://github.com/microsoft/OpenAPI.NET/blob/main/docs/upgrade-guide-2.md) y [Swashbuckle migración a v10](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/migrating-to-v10.md).

## 5. Configuración requerida para arrancar

`appsettings.json` debe incluir (fail-fast si falta):

```jsonc
{
  "ConnectionStrings": {
    "SqlServer": "...",      // solo Development (local db_testing)
    "SqlServerWeb": "..."    // producción
  },
  "Cors": { "AllowedOrigins": ["..."] },
  "RateLimit": { "PermitLimit": 25, "WindowSeconds": 60 },
  "JWT": { "Key": "...", "Issuer": "...", "Audience": "...", "ExpireMin": 60 }
}
```

El usuario gestiona `appsettings.json` (producción) y `appsettings.Development.json` (actualmente solo `Logging`). No escribir config real sin autorización; no tocar `.env`.

## 6. Base de datos

`SqlServer.sql` (raíz del repo) = **esquema consolidado** para la API: tablas `Mae_Config`, `Auth_Profiles`, `Auth_Users` (con `SqlTokenHash VARCHAR(64)`, sin columna `SqlToken`), `PM_CoreData`, seed (`ADMIN`/`USER`, y `Mae_Config` con `ApiKey='Testing-777'`, `IsEnableRegister=1`). Sin procedimientos almacenados. Reconstrucción limpia (DROP + CREATE), solo para instalación limpia. No incluye `CREATE DATABASE`/`LOGIN` (específicos del entorno). La API de la v9 usaba este mismo esquema salvo el nombre de la columna de token de sesión.

> La migración de las bases ya existentes (`Auth_Users.SqlToken` → `SqlTokenHash`) la ejecutó el usuario; ya no queda script de migración en el repo porque no quedan bases pendientes de actualizar. Si algún día aparece una base con el esquema viejo, hay que reponerlo (agregar la columna, hashear con `LOWER(CONVERT(VARCHAR(36), SqlToken))` + `HASHBYTES('SHA2_256', ...)`, eliminar `SqlToken`). La API nueva requiere `SqlTokenHash` y la antigua `SqlToken`: no son coexistentes, así que el cambio exige detener la API, migrar y desplegar.

> **Importante para tests de integración**: los tests `WebApiCore.Tests` requieren la BD local `db_testing` con el esquema del seed (especialmente `Mae_Config` con `Config_Id=1`, `ApiKey='Testing-777'`, `IsEnableRegister=1`). La ApiKey que ve el servidor se lee siempre de `Config_Id=1` (`MaeConfigRepository`); el `Config_Id` NO distingue entornos — la distinción es por BD/connection string. Los tests de integración NO pasan por `ApiKeyFilter`/`ValidateApiKey` (llaman a services/repositorios directo).

## 7. Estado actual

- ✅ API `WebApiCore` (.NET 10) **compila 0 errores / 0 warnings** (`dotnet build WebApiCore.csproj` y `WebApiCore.Tests.csproj`).
- ✅ **Lote de seguridad verificado** (`SqlToken` hasheado + header `SqlToken`, sin caché de `ApiKey`, Swagger solo en Development): build 0/0 y suites en verde tras la migración de la BD ejecutada por el usuario. Dos regresiones introducidas por el lote se detectaron y corrigieron aquí (ver nota al pie).
- ✅ **Tests unitarios** (`WebApiCore.Tests/Security`, `WebApiCore.Tests/Services`): 22/22 pasan sin BD (`PasswordHasher` 4, `JwtTokenUtil` 5, `MaeConfigService` 4, `IpLockoutService` 9). El lockout inyecta `TimeProvider` (BCL, no es un timer) para testear expiración/ventana; en producción usa `TimeProvider.System` y mantiene el *lazy cleanup* (sin timers de limpieza).
- ✅ **Tests**: 71/71 en total (22 unit sin BD + 31 de integración contra la BD local + **18 tests HTTP con `WebApplicationFactory`**). Casos borde incluidos: login con usuario inexistente (401), password inválida (401), `IsEnableRegister=0` (403), registro con email duplicado (409) y con contraseñas que no coinciden (400), IV con password errónea (401), clave de encriptación ya creada (400) y no configurada (401), lockout (bloqueo a los 5 fallos, expiración, ventana, reset, aislamiento por IP, bloqueo de login por credenciales → 429), rate limit de login y de register (6º request → 429), vínculo JWT↔SqlToken (401), ApiKey ausente/incorrecta (401), `traceId` en el 401 del challenge JWT, update/delete con `Data_Id` inexistente (404) en repositorio y servicio, 404 con `ProblemDetails` uniforme, security headers en respuestas de API, **`SqlToken` solo como header** (401 si falta, 401 si es inválido, 401 si se manda en la query string), **`SqlToken` hasheado en BD** (el token crudo no se persiste) y **rotación de `ApiKey` con efecto inmediato** (sin caché).
- ✅ **Verificación en runtime** (local, `http://localhost:5080`, 24 comprobaciones, ejecutadas **antes** del lote de seguridad): Swagger 200; `/health` 200; register 201; login 200/401; core con JWT+SqlToken válidos 200; **vínculo JWT↔SqlToken** (JWT válido + SqlToken ajeno) 401; ApiKey ausente/incorrecta 401; **401 del challenge JWT ahora con `traceId`**; CRUD de Core usando el `Data_Id` devuelto por la API (201 → 200 → 204 → `GET []`); **update/delete con `Data_Id` inexistente → 404** (antes devolvían 200/204 sin modificar nada); `ProblemDetails` uniforme con `traceId` en 400/401/404/429; ApiKey comparada en tiempo constante. **Pendiente**: re-ejecutar contra la versión con header `SqlToken`, hash y Swagger solo en Development.
- ⚠️ **Regresiones del lote de hash, detectadas por los tests y corregidas** (lección para futuros cambios de `SqlToken`):
  1. `CoreUserRepository.RegisterCoreUserPasswordAsync` filtraba por `SqlTokenHash = @SqlTokenHash` calculado desde el `CoreUser` devuelto por `GetCoreUserAsync`. Como ese `SELECT` ya no trae el token crudo (en BD solo queda el hash), el `SqlToken` del objeto era `Guid.Empty`, el hash nunca coincidía y **la contraseña no se guardaba**: el servicio devolvía 200 con `IV` sin haber persistido nada. El `WHERE` quedó solo por `User_Id`, porque la sesión ya la valida el servicio con `GetCoreUserAsync`.
  2. `AuthUserRepository.GetUserByEmailAsync` dejó de seleccionar la columna de token al migrarla, así que `AuthUser.SqlTokenHash` llegaba `null` a los consumidores. Se agregó `a.SqlTokenHash` al `SELECT` para que el mapeo de la entidad sea honesto.
- ✅ **Flujo verificado por el usuario** con la app MAUI + API + BD migrada: login y CRUD de Core operantes con el `SqlToken` por header y hasheado en BD. El agente **no** ejecutó esta verificación en runtime. ⏳ Sigue sin confirmar: que `/` (Swagger UI) y `/openapi/v1.json` devuelvan 404 con `ASPNETCORE_ENVIRONMENT=Production` (hoy solo se comprobó el código), y la revisión del `appsettings.json` de producción, donde `ApiKeyCache:ExpirationSeconds` quedó inerte.
- ⚠️ `PasswordManager_.NET10/Helpers/Constants.cs` está en `.gitignore` (contiene `API_KEY`/`BIOMETRIC_KEY`/`BIOMETRIC_IV` reales). La constante `SQL_TOKEN_HEADER` se agregó también a `Constants_demo.cs`, que es la plantilla versionada: sin eso, un clone limpio no compila hasta copiar la demo a `Constants.cs`.
- ✅ Mejoras aplicadas: `ProblemDetails` con `traceId` uniforme (contrato v2); pipeline reordenado (`UseHttpsRedirection` temprano); **lockout dual por IP con `IpLockoutService` genérico**; **auditoría** de eventos de auth en `AuthController` (login/register con IP y email); **`CancellationToken` propagado** en toda la cadena incl. `MaeConfigService/Repository`; **`JsonWebTokenHandler`** moderno (formato de claims idéntico, `JwtSecurityTokenHandler` legado eliminado del código); **`iss`/`aud` reales**; **health check `/health`**; `JwtOptions.Subject` eliminado + `exp` con `UtcNow`; DTO de salida `CoreDataResponse`; vínculo JWT↔SqlToken; **registro con `INSERT` parametrizado** (`UserCreationStatus` + excepciones, sin SP: `dbo.Auth_Register` eliminado del código y del script); envoltores `ApiResponse`/`ServiceResult` eliminados en backend y cliente; login responde `401` (antes `400`); security headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'`) aplicados solo a rutas de API; **rate limit dedicado en `/login`** (5/min por IP); **`SqlToken` movido de query string al header `SqlToken`** y **hasheado en BD** (`SqlTokenHash`); **`SqlToken` y `ApiKey` fuera del JWT** (el JWT solo lleva identidad; ninguno de los dos se pone en cookie ni storage legible por JS, por eso mantienen su valor como llaves independientes); **caché de `ApiKey` eliminada** (validación contra BD en cada request → revocación inmediata).
- ✅ **Tests HTTP (`WebApiCore.Tests/Http`)**: `ApiFactory` (deriva de `WebApplicationFactory<Program>`, inyecta header `ApiKey` y `X-Forwarded-For` por test), `ApiIntegrationTestBase` (registra/login vía HTTP, limpia los usuarios creados por `user_id` con SQL directo en `DisposeAsync` — solo borra lo que el test crea, respeta la regla del usuario de no tocar datos ajenos) y `ApiIntegrationTests` (18 tests). **Cada test usa una IP única** (`192.0.2.{200 + contador}` secuencial) para aislar rate limiter y lockout — evitar el `Random` que causaba colisiones/flakiness. Los tests de integración/HTTP comparten BD y corren en la colección `Database` con `DisableParallelization = true` (`DatabaseCollection` + `[Collection("Database")]`) para evitar carreras (p.ej. `IsEnableRegister=0` global).
- ✅ **Migración a xUnit v3 + MTP**: el proyecto de tests usa `xunit.v3` (con `UseMicrosoftTestingPlatformRunner=true`), `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing` — versiones gestionadas en `Directory.Packages.props` (decisión CPM, sección 3). `IAsyncLifetime` implementa `ValueTask` (xUnit v3).
- ⚠️ **Comando de tests (SDK .NET 10, modo MTP)**: con el `global.json` de la raíz (`"test": { "runner": "Microsoft.Testing.Platform" }`), **`dotnet test --project WebApiCore.Tests\WebApiCore.Tests.csproj`** es la única sintaxis válida. NO usar argumentos posicionales ni `--nologo`/`-v q` (rompen el modo MTP con exit code 5 "No se ejecutaron pruebas"). El runner descubre 71 tests.
- ✅ Decisión asumida (documentada): la `JWT:Key` versionada en `appsettings.json` del repo es **SOLO de testing** (valor `TheSecretKey...`); en producción el despliegue usa su propio `appsettings.json` con clave real. Riesgo asumido y aceptado por el usuario: si esa key de testing se usara por descuido en un entorno real, debe rotarse (es pública). No se implementó user-secrets/env var por decisión explícita.
- ✅ **Capa 2 de seguridad completada**: `SqlToken` viaja en el header `SqlToken` (cliente MAUI: `IApiService.GetAsync` acepta headers; `Constants.SQL_TOKEN_HEADER`), el token se persiste solo hasheado y la API no acepta el token en la query string. Sigue pendiente, por decisión del usuario, renombrar el header `ApiKey` → `X-ApiKey` (no aporta seguridad real, solo convención). Nota de criterio: la `ApiKey` está compilada en el APK y se puede extraer decompilando, por lo que es *fricción* frente a escáneres genéricos, no un secreto; la llave fuerte es `SqlToken`, que nunca está en el binario, es por usuario, rota en cada login y validada contra el `sub` del JWT.
- ⏳ Pendiente (contrato de comportamiento): **política de contraseña** en register (hoy solo `MinLength(6)`); reforzarla puede rechazar registros que el MAUI permita → requiere coordinar la validación del cliente.
- ✅ Descartado (falso positivo): tests unitarios de controllers — la cobertura HTTP con `WebApplicationFactory` ya ejercita los controllers por el pipeline completo; añadir `FrameworkReference` ASP.NET Core + mocks de `HttpContext` sería duplicación (sobreingeniería).
- ⏳ Pendiente MAUI (budget deuda): ver sección "9. Estado del cliente MAUI" más abajo.
- Git: `WebApiCore.Tests/` y `PasswordManager_.NET10.Tests/` ya trackeados (ambos incluidos en `PasswordManager_.NET10.slnx`); `ANALISIS_V1.md` eliminado.

## 8. Estado del cliente MAUI (`PasswordManager_.NET10`)

### 8.1 Contexto y objetivo

MAUI multi-target (net10.0-android/ios/maccatalyst/windows). Objetivo en curso: eliminar deuda técnica para llegar a nivel "senior". El contrato con la API ya está migrado en el cliente: `ApiResponse<T>` se eliminó, `ApiService` consume DTOs planos y deserialize `ProblemDetails` en `Models/ApiProblemDetails.cs` (`title`/`detail`/`status`/`errors`/`traceId`), y los errores se lanzan como `ApiException` (`StatusCode` + `TraceId` + `ValidationErrors`).

### 8.2 Decisiones de diseño ya aplicadas (ítems A/B/C)

- **SessionManager consolidado** (interfaz slim): `LoginAsync`, `Logout(bool)`, `PerformFullLogoutAsync(string? message)`, `GetRemainingTimeAsync()`, `IsSessionExpiredAsync()`. **Sin evento `SessionExpired`** (decisión del usuario: evita duplicar timers). Fuente única de verdad: `AuthService.GetCurrentUserAsync()` con caché en memoria (sin leer SecureStorage por segundo).
- **HttpClient en DI**: se registra un único `HttpClient` configurado (BaseAddress `Constants.API_BASE_URL`, headers `ApiKey`/`User-Agent`, timeout 30 s, **bypass SSL SOLO en DEBUG**). `ApiService` consume el inyectado (antes creaba el suyo propio). Factory: `MauiProgram.CreateApiHttpClient()`.
- **Navegación centralizada**: nuevo `INavigationService`/`NavigationService` (`GoToAppShellAsync`, `GoToLoginAsync`, `PushModalAsync<T>`, `PushModalAsync(Page)`, `PopModalAsync`, `PopAsync`), registrado singleton. Nuevo `IDialogService`/`DialogService` (`ShowErrorAsync`, `ShowInfoAsync`, `ShowConfirmAsync`) que reemplaza a `Exceptions/AlertExtensions.cs` (eliminado, estaba sin uso). **Todos los ViewModels** (Login, Settings, SessionManager, Help, Register, PasswordPromptCreate, PasswordForm, PasswordDetails) usan hoy solo estos servicios; `IServiceProvider` eliminado de los VMs (p.ej. `PasswordDetailsViewModel` ahora inyecta `PasswordFormViewModel` resolviendo registros transient del DI). `Application.Current.Windows[0].Page` queda solo en `NavigationService`, `DialogService` y el caso defensivo de `SessionManager`. **Aplica a todos los VMs; la migración está completa.**
- **Tema sin "Auto"**: solo Light/Dark. `DEFAULT_THEME = "Dark"`, `ApplyTheme` mapea todo lo que no sea `"Light"` a Dark. La primera ejecución queda en dark sin flash blanco (`App` aplica `UserAppTheme = Dark` síncrono y carga el guardado una sola vez vía `_ = LoadSavedThemeAsync()`).
- **Timeouts/sesiones**: `SettingsViewModel` usa un timer de 1 s **solo para refrescar la UI**; la expiración real la decide `ISessionManager`. Tras logout manual, `RefreshSessionTimeAsync` verifica `IsAuthenticatedAsync()` antes de mostrar el diálogo de expiración (evita diálogo espurio).
- **Idioma/estado**: strings de UI en español; `Constants.APP_VERSION = "Beta 1.0.1"` mostrado en Ajustes.
- **Warnings corregidos**: `ExpandedToArrowConverter` (`object?`, CS8767), catch con log en `LoginViewModel.OpenUrl` (CS0168), `ILogger<PasswordFormViewModel>` tipado correcto, `AppShell.xaml.cs` sin bloque comentado muerto, `MauiProgram` sin doble `;;`.

### 8.3 Verificación

- `dotnet build` del cliente MAUI: **0 advertencias, 0 errores** (verificado tras cada ítem A/B/C y tras agregar el proyecto de tests).
- **Tests del cliente MAUI**: `dotnet test --project PasswordManager_.NET10.Tests\PasswordManager_.NET10.Tests.csproj` → **20/20 correctos** (sin BD, sin UI).
- Sin tests funcionales ejecutados por el agente (política de seguridad de datos); **el usuario confirma que la API y la app MAUI conectan y operan sin problemas**. Queda sin verificar por el agente: logout, navegación a Register/Help y biometría real.

### 8.4 Deuda técnica pendiente (orden de ejecución)

1. ✅ **Navegación restante**: completada. Todos los VMs usan `INavigationService`/`IDialogService`; `Application.Current.Windows[0].Page` solo en los servicios centralizados y el caso defensivo de `SessionManager`.
2. ✅ **Limpiar muerto**: completada. Eliminados comentarios "NUEVOS MÉTODOS..." en `ISecureStorageService.cs`, `//Message = "Login exitoso";` y línea comentada en `PasswordFormViewModel`; eliminado `Exceptions/AlertExtensions.cs` (sin uso).
3. ✅ **Tests**: completados — proyecto `PasswordManager_.NET10.Tests` creado (xunit v3 + MTP, fakes manuales sin Moq), 20/20 tests correctos. Detalle en la sección 8.6.
4. **Secretos en `Constants.cs`** (`BIOMETRIC_KEY`, `BIOMETRIC_IV`, `API_KEY`, `API_BASE_URL`): migrar a KeyChain/SecureStorage. **Dejado deliberadamente para el final** (decisión del usuario).
5. **Testing en producción**: `TestingViewModel`/`TestingPage` y pestaña "Testing" en `AppShell.xaml` se mantienen tal cual (decisión explícita del usuario).

### 8.5 Notas de entorno

- `API_BASE_URL = "https://10.0.2.2:7286"` (correcto para emulador Android). El bypass SSL en DEBUG es necesario porque el certificado local no es de confianza.
- El error "no se conecta" reportado por el usuario era la **BD SQL Server apagada**, no el código.
- `README.md`: sección Docker con comandos del usuario + referencia a `SqlServer.sql`.

### 8.6 Tests del cliente MAUI (implementados)

Cobertura de la lógica de VMs y servicios sin tocar UI, sin BD y sin dependencias nuevas. Patrón de fakes manuales (sin Moq), consistente con `WebApiCore.Tests`.

**Proyecto**: `PasswordManager_.NET10.Tests` (net10.0, `xunit.v3` 4.0.1 + runner MTP, coverlet; mismas versiones que `WebApiCore.Tests`). Referencia el proyecto MAUI (por eso el csproj del MAUI tiene el target `net10.0` con `NoWarn CA1416` condicional). Agregado a `PasswordManager_.NET10.slnx`.

**Fakes**: `FakeAuthService`, `FakeNavigationService`, `FakeThemeService`, `FakeBiometricService`, `FakeDialogService`, `FakeSessionManager`, `FakeHttpMessageHandler` — implementan las interfaces reales con estado interno para asserts (sin Moq).

**Casos** (20/20 correctos):
- `SessionManagerTests` (12): tiempo restante (sin usuario / token futura / token expirada → `Zero`), expiración (null → true / futura → false / pasada → true), login con flag de guardar (guarda y limpia el flag / no guarda / no falla si el guardado lanza), logout (manual y por expiración: limpia sesión y navega a login; si `LogoutAsync` lanza, no propaga).
- `SettingsViewModelTests` (2): `LoadSessionDataAsync` con sesión expirada (estado "Sesión expirada" + datos poblados) y sin usuario (sin populate, sin excepción).
- `ApiServiceTests` (6): DTO plano en `200 Created` y en `GET` sin envoltura, `204 No Content` sin deserializar cuerpo, `ProblemDetails` en error → `ApiException` con `detail` del servidor, cuerpo de error no JSON → `ApiException` sin `JsonException` y con el status code real (p. ej. `502`), **`GET` con headers: se envían en el request y el `SqlToken` no aparece en la query string**.

**Fuera de alcance (documentado)**:
- `NavigationService`: no testeable sin host MAUI (todo delega en `Application.Current.Windows[0].Page` y páginas del DI); cubrirlo sería un test vacío.
- `SettingsViewModel`: flujos con timer real de 1 s y `MainThread.BeginInvokeOnMainThread` no son deterministas en unit tests (el guard de `IsAuthenticatedAsync` tras logout queda pendiente de verificación funcional).
- UI tests, biometría real, SecureStorage real e integración con la API (ya cubierto por `WebApiCore.Tests`).

## 9. Referencias

- `README.md` — manual de usuario del cliente MAUI.
- `D:\Repo\.NET\Projects_.NET9` — repo fuente de la API v9 (`WebApiCore`); referencia de comparación y paridad.
- `ANALISIS_V1.md` fue **eliminado** del repo (decisión del usuario); si se retoma la auditoría del MAUI v1, recrear el documento con los hallazgos de la conversación.