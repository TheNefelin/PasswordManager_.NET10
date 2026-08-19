# DEVELOPMENT.md — Password Manager .NET 10

Documento de desarrollo del repo `D:\Repo\.NET\PasswordManager_.NET10`. Su propósito es que una sesión nueva de OpenCode recupere el contexto completo del proyecto sin depender de conversaciones previas.

## 1. Visión general

Solución (`PasswordManager_.NET10.slnx`) con **dos proyectos deployables independientes**:

| Proyecto | Qué es | Estado |
|---|---|---|
| `WebApiCore` + capas (`Application`, `Domain`, `Infrastructure`) | **API** (ASP.NET Core, .NET 10) | Port a .NET 10 de la API `WebApiCore` del repo `D:\Repo\.NET\Projects_.NET9`. Debe funcionar igual y ser el **reemplazo** de la v9. |
| `PasswordManager_.NET10` | Cliente **MAUI** (net10.0-android/ios/maccatalyst/windows) | Versión v1 existente. Plan de migración v1→v2 en `ANALISIS_V1.md`. |

Reglas de operación: `AGENTS.md` (mismas reglas generales que en `Projects_.NET9`).

## 2. Arquitectura (Clean Architecture)

| Capa | Contenido |
|---|---|
| `WebApiCore.Domain` | Interfaces de repositorios (`IAuthUserRepository`, `ICoreUserRepository`, `ICoreDataRepository`, `IMaeConfigRepository`), entidades. Sin dependencias. |
| `WebApiCore.Application` | DTOs, servicios de aplicación, `ApiResponse` (envelope), interfaces de servicios. Referencia solo Domain. |
| `WebApiCore.Infrastructure` | Dapper + `Microsoft.Data.SqlClient`, repositorios, `PasswordHasher` (PBKDF2), `JwtTokenUtil`, `JwtOptions`. Referencia Application. |
| `WebApiCore` (API) | `Program.cs`, controllers (`AuthController`, `CoreController`), `Middleware/GlobalExceptionHandler`, `Filters/` (`ApiKeyFilter`, `ApiKeyOperationFilter`, `AuthorizeOperationFilter`). |

Dependencias (NuGet) en `WebApiCore`: `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.11`, `Microsoft.AspNetCore.OpenApi 10.0.11`, `Swashbuckle.AspNetCore 10.2.3`. En Infrastructure: `Dapper 2.1.79`, `Microsoft.Data.SqlClient 7.0.2`, `Microsoft.AspNetCore.Cryptography.KeyDerivation 10.0.11`, `System.IdentityModel.Tokens.Jwt 8.22.0`.

## 3. Decisiones de diseño clave

- **Envelope uniforme `ApiResponse`**: toda respuesta (éxito y error) usa `ApiResponse<T>` (`isSuccess`, `statusCode`, `message`, `data`, `errors`, `traceId`). Errores centralizados en `GlobalExceptionHandler` (500 genérico sin fuga de detalle) + `InvalidModelStateResponseFactory` (400) + `JwtBearerEvents.OnChallenge` (401) + `OnRejected` del rate limiter (429) + `MapFallback` (404).
- **Dapper + SQL Server** con SP `Auth_Register` para el registro. El login se resuelve en C# (`GetUserByEmailAsync` + token nuevo), no con SP.
- **Autenticación doble**: `ApiKey` (header, validada contra `Mae_Config.ApiKey`, filtro `ApiKeyFilter` aplicado a ambos controllers) + `JWT` Bearer.
- **Fail-fast de configuración**: si falta `ConnectionStrings`, `JWT`, o `Cors:AllowedOrigins` vacío → `InvalidOperationException` al arrancar. Es deliberado (no arranca con config inválida).
- **Connection string por entorno**: `Development` → `ConnectionStrings:SqlServer` (local `db_testing`); cualquier otro entorno → `ConnectionStrings:SqlServerWeb` (producción).
- **Rate limiting**: `client_25_per_minute`, 25 req/min por cliente, ventana fija 60 s, `QueueLimit=0`; particionado por `X-Forwarded-For` (primer valor) → fallback `RemoteIpAddress`. Parámetros `RateLimit:PermitLimit`/`WindowSeconds`.
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
  "JWT": { "Key": "...", "Issuer": "...", "Audience": "...", "Subject": "...", "ExpireMin": 60 }
}
```

El usuario gestiona `appsettings.json` (producción) y `appsettings.Development.json` (actualmente solo `Logging`). No escribir config real sin autorización; no tocar `.env`.

## 6. Base de datos

`SqlServer.sql` (raíz del repo) = **esquema consolidado** para la API: tablas `Mae_Config`, `Auth_Profiles`, `Auth_Users`, `PM_CoreData`, seed (`ADMIN`/`USER`, y `Mae_Config` con `ApiKey='Testing-777'`, `IsEnableRegister=1`) y SP `Auth_Register`. Reconstrucción limpia (DROP + CREATE). No incluye `CREATE DATABASE`/`LOGIN` (específicos del entorno). La API de la v9 usaba este mismo esquema.

## 7. Estado actual

- ✅ API `WebApiCore` (.NET 10) **compila 0 errores / 0 warnings** tras los fixes de OpenApi 2.x (sección 4).
- ⏳ Pendiente: `appsettings.json` con config real para arrancar; verificación en runtime; trabajo sobre MAUI (v1→v2 según `ANALISIS_V1.md`).
- Git: los proyectos de la API, `AGENTS.md`, `ANALISIS_V1.md`, `DEVELOPMENT.md` y `SqlServer.sql` están **sin trackear** (nuevos). `PasswordManager_.NET10.slnx` modificado.

## 8. Referencias

- `ANALISIS_V1.md` — auditoría técnica del MAUI v1 (seguridad, arquitectura, MVVM) y plan/checklist para v2.
- `README.md` — manual de usuario del cliente MAUI.
- `D:\Repo\.NET\Projects_.NET9` — repo fuente de la API v9 (`WebApiCore`); referencia de comparación y paridad.