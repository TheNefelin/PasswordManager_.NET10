# DEVELOPMENT.md — Password Manager .NET 10

Documento de desarrollo del repo `D:\Repo\.NET\PasswordManager_.NET10`. Su propósito es que una sesión nueva de OpenCode recupere el contexto completo del proyecto sin depender de conversaciones previas.

## 1. Visión general

Solución (`PasswordManager_.NET10.slnx`) con **dos proyectos deployables independientes**:

| Proyecto | Qué es | Estado |
|---|---|---|
| `WebApiCore` + capas (`Application`, `Domain`, `Infrastructure`) | **API** (ASP.NET Core, .NET 10) | Port a .NET 10 de la API `WebApiCore` del repo `D:\Repo\.NET\Projects_.NET9`. Reemplazo de la v9. Con mejoras de seguridad/calidad aplicadas (secciones 3 y 7). |
| `PasswordManager_.NET10` | Cliente **MAUI** (net10.0-android/ios/maccatalyst/windows) | Versión v1 existente. `ANALISIS_V1.md` fue eliminado; auditoría v1→v2 pendiente de documentar si se retoma el MAUI. |

Reglas de operación: `AGENTS.md` (mismas reglas generales que en `Projects_.NET9`).

## 2. Arquitectura (Clean Architecture)

| Capa | Contenido |
|---|---|
| `WebApiCore.Domain` | Interfaces de repositorios (`IAuthUserRepository`, `ICoreUserRepository`, `ICoreDataRepository`, `IMaeConfigRepository`), entidades. Sin dependencias. |
| `WebApiCore.Application` | DTOs (incluye `CoreDataResponse`, el DTO de salida de Core), `ApiResponse` (envelope), servicios de aplicación, interfaces (`IApiKeyLockoutService`, `IAuthTokenService`, `IPasswordHasher`, etc.). Referencia solo Domain. |
| `WebApiCore.Infrastructure` | Dapper + `Microsoft.Data.SqlClient`, repositorios, seguridad (`PasswordHasher` PBKDF2, `JwtTokenUtil`, `ApiKeyLockoutService`), `JwtOptions`. Referencia Application. |
| `WebApiCore` (API) | `Program.cs`, controllers (`AuthController`, `CoreController`), `Middleware/GlobalExceptionHandler`, `Filters/` (`ApiKeyFilter`, `ApiKeyOperationFilter`, `AuthorizeOperationFilter`), `Helpers/ClientIpResolver`. |

Dependencias (NuGet) en `WebApiCore`: `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.11`, `Microsoft.AspNetCore.OpenApi 10.0.11`, `Swashbuckle.AspNetCore 10.2.3`. En Infrastructure: `Dapper 2.1.79`, `Microsoft.Data.SqlClient 7.0.2`, `System.IdentityModel.Tokens.Jwt 8.22.0` (el hashing de contraseñas usa `Rfc2898DeriveBytes.Pbkdf2` del BCL, sin paquete externo; se eliminó `Microsoft.AspNetCore.Cryptography.KeyDerivation`). En `WebApiCore.Tests`: xUnit, `Microsoft.NET.Test.Sdk`, coverlet (sin Moq; fakes/stubs manuales).

## 3. Decisiones de diseño clave

- **Envelope uniforme `ApiResponse`**: toda respuesta (éxito y error) usa `ApiResponse<T>` (`isSuccess`, `statusCode`, `message`, `data`, `errors`, `traceId`). Errores centralizados en `GlobalExceptionHandler` (500 genérico sin fuga de detalle) + `InvalidModelStateResponseFactory` (400) + `JwtBearerEvents.OnChallenge` (401) + `OnRejected` del rate limiter (429) + `MapFallback` (404). El `traceId` se propaga desde `HttpContext.TraceIdentifier` en todos los caminos de error.
- **Pipeline HTTP** (orden en `Program.cs`): `UseExceptionHandler` → `UseHttpsRedirection` → `UseRateLimiter` → Swagger → `UseCors` → `UseAuthentication` → `UseAuthorization` → `MapControllers` → `MapFallback` (404 uniforme).
- **Dapper + SQL Server**: el login se resuelve en C# (`GetUserByEmailAsync` + token nuevo), sin SP. El registro usa el SP `Auth_Register` (único SP del stack; `Auth_Login` nunca existió — su `DROP` se eliminó del script). El SP devuelve `IsSuccess`/`StatusCode`/`Message`: `403` si `IsEnableRegister=0`, `400` si el email ya existe, `201` en éxito y `500` en error no controlado (ya NO usa `ERROR_STATE()` como status code).
- **Autenticación doble**: `ApiKey` (header, validada contra `Mae_Config.ApiKey` con **comparación en tiempo constante** `CryptographicOperations.FixedTimeEquals`, filtro `ApiKeyFilter` aplicado a ambos controllers) + `JWT` Bearer.
- **Lockout por IP de la ApiKey** (`ApiKeyLockoutService`, singleton): 5 fallos en ventana de 10 min bloquean la IP durante 1 h. `ApiKeyFilter` devuelve `429` + header `Retry-After` si está bloqueada, registra fallos (401) y resetea en éxito. Logging del bloqueo en el filtro (el servicio no depende de `ILogger` para no requerir el shared framework en Infrastructure). La IP se resuelve con `ClientIpResolver` (compartido con el rate limiter).
- **Vínculo JWT↔SqlToken**: la identidad de los endpoints core se toma del **claim `sub` del JWT** (`ClaimTypes.NameIdentifier`), nunca del `User_Id` enviado por el cliente (que se ignora). El `SqlToken` del request debe pertenecer al mismo usuario del JWT, o se responde `401`. Elimina el cruce "JWT de A + SqlToken de B".
- **DTO de salida `CoreDataResponse`**: la API no expone la entidad de dominio `CoreData`; se mapea en `CoreDataService` con `ToDTO`/`ToEntity` (shape 1:1 → sin cambio de contrato JSON).
- **JWT**: `sub` = `AuthUser.User_Id` (GUID inmutable), `exp` calculado con `DateTime.UtcNow`; `JwtOptions` no tiene `Subject` (era config muerta; el `sub` identifica al usuario, no a la institución).
- **Fail-fast de configuración**: si falta `ConnectionStrings`, `JWT`, o `Cors:AllowedOrigins` vacío → `InvalidOperationException` al arrancar. Es deliberado (no arranca con config inválida).
- **Connection string por entorno**: `Development` → `ConnectionStrings:SqlServer` (local `db_testing`); cualquier otro entorno → `ConnectionStrings:SqlServerWeb` (producción).
- **Rate limiting**: `client_25_per_minute`, 25 req/min por cliente, ventana fija 60 s, `QueueLimit=0`; particionado por `ClientIpResolver` (`X-Forwarded-For` primer valor → fallback `RemoteIpAddress`). Parámetros `RateLimit:PermitLimit`/`WindowSeconds`.
- **CORS configurable** desde `Cors:AllowedOrigins` (sin `SetIsOriginAllowed(_ => true)`).
- **Swagger UI en la raíz** (`RoutePrefix = ""`) con **ruta absoluta** `/swagger/v1/swagger.json` (evita el rewrite relativo de `index.js`).

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

`SqlServer.sql` (raíz del repo) = **esquema consolidado** para la API: tablas `Mae_Config`, `Auth_Profiles`, `Auth_Users`, `PM_CoreData`, seed (`ADMIN`/`USER`, y `Mae_Config` con `ApiKey='Testing-777'`, `IsEnableRegister=1`) y SP `Auth_Register`. Reconstrucción limpia (DROP + CREATE). No incluye `CREATE DATABASE`/`LOGIN` (específicos del entorno). La API de la v9 usaba este mismo esquema.

> **Importante para tests de integración**: los tests `WebApiCore.Tests` requieren la BD local `db_testing` con el esquema del seed (especialmente `Mae_Config` con `Config_Id=1`, `ApiKey='Testing-777'`, `IsEnableRegister=1`). La ApiKey que ve el servidor se lee siempre de `Config_Id=1` (`MaeConfigRepository`); el `Config_Id` NO distingue entornos — la distinción es por BD/connection string. Los tests de integración NO pasan por `ApiKeyFilter`/`ValidateApiKey` (llaman a services/repositorios directo).

## 7. Estado actual

- ✅ API `WebApiCore` (.NET 10) **compila 0 errores / 0 warnings** (`dotnet build WebApiCore.csproj` y `WebApiCore.Tests.csproj`).
- ✅ **Tests unitarios** (`WebApiCore.Tests/Security`, `WebApiCore.Tests/Services`): 21/21 pasan sin BD (`PasswordHasher`, `JwtTokenUtil`, `MaeConfigService`, `ApiKeyLockoutService`). El lockout inyecta `TimeProvider` (BCL, no es un timer) para testear expiración/ventana; en producción usa `TimeProvider.System` y mantiene el *lazy cleanup* (sin timers de limpieza). Ejecutar solo unit: `dotnet test WebApiCore.Tests --filter "FullyQualifiedName~Security|FullyQualifiedName~MaeConfigServiceTests"`.
- ✅ **Tests**: 47/47 en total (unit 21 sin BD + integración con BD local y `Auth_Register` actualizado). Casos borde incluidos: login con usuario inexistente (401), password inválida (401), `IsEnableRegister=0` (403), IV con password errónea (401), lockout (bloqueo a los 5 fallos, expiración, ventana, reset, aislamiento por IP, bloqueo de login por credenciales → 429).
- ✅ **Verificación en runtime** (local): Swagger 200; register 201; login 200/401; core con JWT+SqlToken válidos 200; **vínculo JWT↔SqlToken** (JWT válido + SqlToken ajeno) 401; ApiKey comparada en tiempo constante.
- ✅ Mejoras aplicadas: envelope con `traceId` uniforme; pipeline reordenado (`UseHttpsRedirection` temprano); **lockout dual por IP con `IpLockoutService` genérico** (dos instancias independientes en DI: `api-key` con 5/10min→1h y `login` con 5/15min→15min; sin duplicación de lógica); `JwtOptions.Subject` eliminado + `exp` con `UtcNow`; DTO de salida `CoreDataResponse`; vínculo JWT↔SqlToken; SP `Auth_Register` corregido (`403`/`500`, sin `ERROR_STATE()`) y `DROP` de `Auth_Login` eliminado del script; login responde `401` (antes `400`); security headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'`) aplicados solo a rutas de API (no a `/swagger`).
- ⏳ Pendiente (decisión de contrato): **mover `SqlToken` de query string a un header** (capa 2 de seguridad; requiere cambio en el cliente MAUI).
- ⏳ Pendiente MAUI (v1→v2): auditoría y refactor no iniciados.
- Git: `WebApiCore.Tests/` nuevo (untracked), `PasswordManager_.NET10.slnx` modificado (incluye el proyecto de tests), `ANALISIS_V1.md` eliminado.

## 8. Referencias

- `README.md` — manual de usuario del cliente MAUI.
- `D:\Repo\.NET\Projects_.NET9` — repo fuente de la API v9 (`WebApiCore`); referencia de comparación y paridad.
- `ANALISIS_V1.md` fue **eliminado** del repo (decisión del usuario); si se retoma la auditoría del MAUI v1, recrear el documento con los hallazgos de la conversación.