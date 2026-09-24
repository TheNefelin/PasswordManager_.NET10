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
| `WebApiCore.Application` | DTOs (incluye `CoreDataResponse`, el DTO de salida de Core), `ApiResponse` (envelope), servicios de aplicación, interfaces (`IIpLockoutService`, `IAuthTokenService`, `IPasswordHasher`, etc.). Referencia solo Domain. |
| `WebApiCore.Infrastructure` | Dapper + `Microsoft.Data.SqlClient`, repositorios, seguridad (`PasswordHasher` PBKDF2, `JwtTokenUtil`, `IpLockoutService`, `IpLockoutOptions`), `JwtOptions`. Referencia Application. |
| `WebApiCore` (API) | `Program.cs`, controllers (`AuthController`, `CoreController`), `Middleware/GlobalExceptionHandler`, `Filters/` (`ApiKeyFilter`, `ApiKeyOperationFilter`, `AuthorizeOperationFilter`), `Helpers/ClientIpResolver`. |

Dependencias (NuGet) **gestionadas por Central Package Management (CPM)** — las versiones se definen una sola vez en `Directory.Packages.props` (raíz del repo; ver decisión en la sección 3). `WebApiCore` usa `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.OpenApi`, `Swashbuckle.AspNetCore.SwaggerUI`. Infrastructure: `Dapper`, `Microsoft.Data.SqlClient`, `System.IdentityModel.Tokens.Jwt` (el hashing de contraseñas usa `Rfc2898DeriveBytes.Pbkdf2` del BCL, sin paquete externo; se eliminó `Microsoft.AspNetCore.Cryptography.KeyDerivation`). `WebApiCore.Tests`: `xunit.v3` + `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing`, coverlet (sin Moq; fakes/stubs manuales).

## 3. Decisiones de diseño clave

- **Envelope uniforme `ApiResponse`**: toda respuesta (éxito y error) usa `ApiResponse<T>` (`isSuccess`, `statusCode`, `message`, `data`, `errors`, `traceId`). Errores centralizados en `GlobalExceptionHandler` (500 genérico sin fuga de detalle) + `InvalidModelStateResponseFactory` (400) + `JwtBearerEvents.OnChallenge` (401) + `OnRejected` del rate limiter (429) + `MapFallback` (404). El `traceId` se propaga desde `HttpContext.TraceIdentifier` en todos los caminos de error.
- **Pipeline HTTP** (orden en `Program.cs`): `UseExceptionHandler` → `UseHttpsRedirection` → `UseRateLimiter` → Swagger → `UseCors` → `UseAuthentication` → `UseAuthorization` → `MapControllers` → `MapFallback` (404 uniforme).
- **Dapper + SQL Server**: el login se resuelve en C# (`GetUserByEmailAsync` + token nuevo), sin SP. El registro usa el SP `Auth_Register` (único SP del stack; `Auth_Login` nunca existió — su `DROP` se eliminó del script). El SP devuelve `IsSuccess`/`StatusCode`/`Message`: `403` si `IsEnableRegister=0`, `400` si el email ya existe, `201` en éxito y `500` en error no controlado (ya NO usa `ERROR_STATE()` como status code).
- **Autenticación doble**: `ApiKey` (header, validada contra `Mae_Config.ApiKey` con **comparación en tiempo constante** `CryptographicOperations.FixedTimeEquals`, filtro `ApiKeyFilter` aplicado a ambos controllers) + `JWT` Bearer.
- **Lockout dual por IP** (`IpLockoutService` genérico con `IpLockoutOptions` + `TimeProvider`, singleton): dos instancias independientes en DI — `api-key` (5 fallos / 10 min → bloqueo 1 h, usada por `ApiKeyFilter` vía `[FromKeyedServices]`) y `login` (5 fallos / 15 min → bloqueo 15 min, usada por `AuthUserService.LoginAsync`). Respuesta `429` con header `Retry-After`. Logging del bloqueo en el filtro y en el controller (los servicios no dependen de `ILogger` para no requerir el shared framework en Infrastructure/Application). La IP se resuelve con `ClientIpResolver` (compartido con el rate limiter).
- **Vínculo JWT↔SqlToken**: la identidad de los endpoints core se toma del **claim `sub` del JWT** (`ClaimTypes.NameIdentifier`), nunca del `User_Id` enviado por el cliente (que se ignora). El `SqlToken` del request debe pertenecer al mismo usuario del JWT, o se responde `401`. Elimina el cruce "JWT de A + SqlToken de B".
- **DTO de salida `CoreDataResponse`**: la API no expone la entidad de dominio `CoreData`; se mapea en `CoreDataService` con `ToDTO`/`ToEntity` (shape 1:1 → sin cambio de contrato JSON).
- **JWT**: `sub` = `AuthUser.User_Id` (GUID inmutable), `exp` calculado con `DateTime.UtcNow`; `JwtOptions` no tiene `Subject` (era config muerta; el `sub` identifica al usuario, no a la institución).
- **Fail-fast de configuración**: si falta `ConnectionStrings`, `JWT`, o `Cors:AllowedOrigins` vacío → `InvalidOperationException` al arrancar. Es deliberado (no arranca con config inválida).
- **Connection string por entorno**: `Development` → `ConnectionStrings:SqlServer` (local `db_testing`); cualquier otro entorno → `ConnectionStrings:SqlServerWeb` (producción).
- **Rate limiting**: `client_25_per_minute`, 25 req/min por cliente, ventana fija 60 s, `QueueLimit=0` (aplica a core); **política dedicada `login_5_per_minute`** (5 req/min) en `AuthController.Login` y **`register_5_per_minute`** (5 req/min) en `AuthController.Register` para endurecer brute-force y spam de cuentas. Todas particionadas por `ClientIpResolver` (`X-Forwarded-For` primer valor → fallback `RemoteIpAddress`). Parámetros `RateLimit:PermitLimit`/`WindowSeconds`, `RateLimit:LoginPermitLimit`/`LoginWindowSeconds` y `RateLimit:RegisterPermitLimit`/`RegisterWindowSeconds`. Respuesta `429` con envelope `ApiResponse` vía `OnRejected`.
- **Cache de ApiKey**: `MaeConfigService` cachea la ApiKey de `Mae_Config` en memoria con TTL corto (30 s por defecto, configurable vía `ApiKeyCache:ExpirationSeconds`), con `TimeProvider` y lock (sin dependencias nuevas). Evita una consulta a BD por request y propaga la rotación de la ApiKey en máx. 30 s.
- **CORS configurable** desde `Cors:AllowedOrigins` (sin `SetIsOriginAllowed(_ => true)`).
- **Swagger UI en la raíz** (`RoutePrefix = ""`) con **ruta absoluta** `/swagger/v1/swagger.json` (evita el rewrite relativo de `index.js`).
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

`SqlServer.sql` (raíz del repo) = **esquema consolidado** para la API: tablas `Mae_Config`, `Auth_Profiles`, `Auth_Users`, `PM_CoreData`, seed (`ADMIN`/`USER`, y `Mae_Config` con `ApiKey='Testing-777'`, `IsEnableRegister=1`) y SP `Auth_Register`. Reconstrucción limpia (DROP + CREATE). No incluye `CREATE DATABASE`/`LOGIN` (específicos del entorno). La API de la v9 usaba este mismo esquema.

> **Importante para tests de integración**: los tests `WebApiCore.Tests` requieren la BD local `db_testing` con el esquema del seed (especialmente `Mae_Config` con `Config_Id=1`, `ApiKey='Testing-777'`, `IsEnableRegister=1`). La ApiKey que ve el servidor se lee siempre de `Config_Id=1` (`MaeConfigRepository`); el `Config_Id` NO distingue entornos — la distinción es por BD/connection string. Los tests de integración NO pasan por `ApiKeyFilter`/`ValidateApiKey` (llaman a services/repositorios directo).

## 7. Estado actual

- ✅ API `WebApiCore` (.NET 10) **compila 0 errores / 0 warnings** (`dotnet build WebApiCore.csproj` y `WebApiCore.Tests.csproj`).
- ✅ **Tests unitarios** (`WebApiCore.Tests/Security`, `WebApiCore.Tests/Services`): 21/21 pasan sin BD (`PasswordHasher`, `JwtTokenUtil`, `MaeConfigService`, `IpLockoutService`). El lockout inyecta `TimeProvider` (BCL, no es un timer) para testear expiración/ventana; en producción usa `TimeProvider.System` y mantiene el *lazy cleanup* (sin timers de limpieza).
- ✅ **Tests**: 64/64 en total (unit 21 sin BD + integración con BD local + **14 tests HTTP con `WebApplicationFactory`**). Casos borde incluidos: login con usuario inexistente (401), password inválida (401), `IsEnableRegister=0` (403), IV con password errónea (401), lockout (bloqueo a los 5 fallos, expiración, ventana, reset, aislamiento por IP, bloqueo de login por credenciales → 429), rate limit de login y de register (6º request → 429), vínculo JWT↔SqlToken (401), ApiKey ausente/incorrecta (401), 404 con envelope uniforme, security headers en respuestas de API.
- ✅ **Verificación en runtime** (local): Swagger 200; register 201; login 200/401; core con JWT+SqlToken válidos 200; **vínculo JWT↔SqlToken** (JWT válido + SqlToken ajeno) 401; ApiKey comparada en tiempo constante; `/health` 200.
- ✅ Mejoras aplicadas: envelope con `traceId` uniforme; pipeline reordenado (`UseHttpsRedirection` temprano); **lockout dual por IP con `IpLockoutService` genérico**; **auditoría** de eventos de auth en `AuthController` (login/register con IP y email); **`CancellationToken` propagado** en toda la cadena incl. `MaeConfigService/Repository`; **`JsonWebTokenHandler`** moderno (formato de claims idéntico, `JwtSecurityTokenHandler` legado eliminado del código); **`iss`/`aud` reales**; **health check `/health`**; `JwtOptions.Subject` eliminado + `exp` con `UtcNow`; DTO de salida `CoreDataResponse`; vínculo JWT↔SqlToken; SP `Auth_Register` corregido (`403`/`500`, sin `ERROR_STATE()`) y `DROP` de `Auth_Login` eliminado del script; login responde `401` (antes `400`); security headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'`) aplicados solo a rutas de API (no a `/swagger`); **cache de ApiKey** (30 s configurable); **rate limit dedicado en `/login`** (5/min por IP).
- ✅ **Tests HTTP (`WebApiCore.Tests/Http`)**: `ApiFactory` (deriva de `WebApplicationFactory<Program>`, inyecta header `ApiKey` y `X-Forwarded-For` por test), `ApiIntegrationTestBase` (registra/login vía HTTP, limpia los usuarios creados por `user_id` con SQL directo en `DisposeAsync` — solo borra lo que el test crea, respeta la regla del usuario de no tocar datos ajenos) y `ApiIntegrationTests` (14 tests). **Cada test usa una IP única** (`192.0.2.{200 + contador}` secuencial) para aislar rate limiter y lockout — evitar el `Random` que causaba colisiones/flakiness. Los tests de integración/HTTP comparten BD y corren en la colección `Database` con `DisableParallelization = true` (`DatabaseCollection` + `[Collection("Database")]`) para evitar carreras (p.ej. `IsEnableRegister=0` global).
- ✅ **Migración a xUnit v3 + MTP**: el proyecto de tests usa `xunit.v3` (con `UseMicrosoftTestingPlatformRunner=true`), `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing` — versiones gestionadas en `Directory.Packages.props` (decisión CPM, sección 3). `IAsyncLifetime` implementa `ValueTask` (xUnit v3).
- ⚠️ **Comando de tests (SDK .NET 10, modo MTP)**: con el `global.json` de la raíz (`"test": { "runner": "Microsoft.Testing.Platform" }`), **`dotnet test --project WebApiCore.Tests\WebApiCore.Tests.csproj`** es la única sintaxis válida. NO usar argumentos posicionales ni `--nologo`/`-v q` (rompen el modo MTP con exit code 5 "No se ejecutaron pruebas"). El runner descubre 64 tests.
- ✅ Decisión asumida (documentada): la `JWT:Key` versionada en `appsettings.json` del repo es **SOLO de testing** (valor `TheSecretKey...`); en producción el despliegue usa su propio `appsettings.json` con clave real. Riesgo asumido y aceptado por el usuario: si esa key de testing se usara por descuido en un entorno real, debe rotarse (es pública). No se implementó user-secrets/env var por decisión explícita.
- ⏳ Pendiente (decisión de contrato): **mover `SqlToken` de query string a un header** y renombrar `ApiKey`→`X-ApiKey` (capa 2 de seguridad; requiere cambio en el cliente MAUI). Dejado deliberadamente para el final de la solución.
- ⏳ Pendiente (contrato de comportamiento): **política de contraseña** en register (hoy solo `MinLength(6)`); reforzarla puede rechazar registros que el MAUI permita → requiere coordinar la validación del cliente.
- ✅ Descartado (falso positivo): tests unitarios de controllers — la cobertura HTTP con `WebApplicationFactory` ya ejercita los controllers por el pipeline completo; añadir `FrameworkReference` ASP.NET Core + mocks de `HttpContext` sería duplicación (sobreingeniería).
- ⏳ Pendiente MAUI (budget deuda): ver sección "9. Estado del cliente MAUI" más abajo.
- Git: `WebApiCore.Tests/` nuevo (untracked), `PasswordManager_.NET10.slnx` modificado (incluye el proyecto de tests), `ANALISIS_V1.md` eliminado.

## 8. Estado del cliente MAUI (`PasswordManager_.NET10`)

### 8.1 Contexto y objetivo

MAUI multi-target (net10.0-android/ios/maccatalyst/windows). Objetivo en curso: eliminar deuda técnica para llegar a nivel "senior", tocando **solo el frontend** — el contrato con la API (`ApiResponse`, endpoints) no se modifica. `ApiResponse` (envelope) ya está alineado con el backend.

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
- **Tests del cliente MAUI**: `dotnet test --project PasswordManager_.NET10.Tests\PasswordManager_.NET10.Tests.csproj` → **14/14 correctos** (sin BD, sin UI).
- Sin tests funcionales ejecutados (política de seguridad de datos); verificación runtime en emulador pendiente (logout, login, navegación a Register/Help).

### 8.4 Deuda técnica pendiente (orden de ejecución)

1. ✅ **Navegación restante**: completada. Todos los VMs usan `INavigationService`/`IDialogService`; `Application.Current.Windows[0].Page` solo en los servicios centralizados y el caso defensivo de `SessionManager`.
2. ✅ **Limpiar muerto**: completada. Eliminados comentarios "NUEVOS MÉTODOS..." en `ISecureStorageService.cs`, `//Message = "Login exitoso";` y línea comentada en `PasswordFormViewModel`; eliminado `Exceptions/AlertExtensions.cs` (sin uso).
3. ✅ **Tests**: completados — proyecto `PasswordManager_.NET10.Tests` creado (xunit v3 + MTP, fakes manuales sin Moq), 14/14 tests correctos. Detalle en la sección 8.6.
4. **Secretos en `Constants.cs`** (`BIOMETRIC_KEY`, `BIOMETRIC_IV`, `API_KEY`, `API_BASE_URL`): migrar a KeyChain/SecureStorage. **Dejado deliberadamente para el final** (decisión del usuario).
5. **Testing en producción**: `TestingViewModel`/`TestingPage` y pestaña "Testing" en `AppShell.xaml` se mantienen tal cual (decisión explícita del usuario).

### 8.5 Notas de entorno

- `API_BASE_URL = "https://10.0.2.2:7286"` (correcto para emulador Android). El bypass SSL en DEBUG es necesario porque el certificado local no es de confianza.
- El error "no se conecta" reportado por el usuario era la **BD SQL Server apagada**, no el código.
- `README.md`: sección Docker con comandos del usuario + referencia a `SqlServer.sql`.

### 8.6 Tests del cliente MAUI (implementados)

Cobertura de la lógica de VMs y servicios sin tocar UI, sin BD y sin dependencias nuevas. Patrón de fakes manuales (sin Moq), consistente con `WebApiCore.Tests`.

**Proyecto**: `PasswordManager_.NET10.Tests` (net10.0, `xunit.v3` 4.0.1 + runner MTP, coverlet; mismas versiones que `WebApiCore.Tests`). Referencia el proyecto MAUI (por eso el csproj del MAUI tiene el target `net10.0` con `NoWarn CA1416` condicional). Agregado a `PasswordManager_.NET10.slnx`.

**Fakes**: `FakeAuthService`, `FakeNavigationService`, `FakeThemeService`, `FakeBiometricService`, `FakeDialogService`, `FakeSessionManager` — implementan las interfaces reales con estado interno para asserts (sin Moq).

**Casos** (14/14 correctos):
- `SessionManagerTests` (12): tiempo restante (sin usuario / token futura / token expirada → `Zero`), expiración (null → true / futura → false / pasada → true), login con flag de guardar (guarda y limpia el flag / no guarda / no falla si el guardado lanza), logout (manual y por expiración: limpia sesión y navega a login; si `LogoutAsync` lanza, no propaga).
- `SettingsViewModelTests` (2): `LoadSessionDataAsync` con sesión expirada (estado "Sesión expirada" + datos poblados) y sin usuario (sin populate, sin excepción).

**Fuera de alcance (documentado)**:
- `NavigationService`: no testeable sin host MAUI (todo delega en `Application.Current.Windows[0].Page` y páginas del DI); cubrirlo sería un test vacío.
- `SettingsViewModel`: flujos con timer real de 1 s y `MainThread.BeginInvokeOnMainThread` no son deterministas en unit tests (el guard de `IsAuthenticatedAsync` tras logout queda pendiente de verificación funcional).
- UI tests, biometría real, SecureStorage real e integración con la API (ya cubierto por `WebApiCore.Tests`).

## 9. Referencias

- `README.md` — manual de usuario del cliente MAUI.
- `D:\Repo\.NET\Projects_.NET9` — repo fuente de la API v9 (`WebApiCore`); referencia de comparación y paridad.
- `ANALISIS_V1.md` fue **eliminado** del repo (decisión del usuario); si se retoma la auditoría del MAUI v1, recrear el documento con los hallazgos de la conversación.