# Análisis Técnico — Password Manager v1 → v2

## Índice

1. [🔴 Seguridad — Crítico](#-seguridad--crítico)
2. [🔴 Arquitectura — Crítico](#-arquitectura--crítico)
3. [🟡 Calidad de Código](#-calidad-de-código)
4. [🟡 MVVM y MAUI Anti-Patterns](#-mvvm-y-maui-anti-patterns)
5. [🟡 Patrones Ausentes](#-patrones-ausentes)
6. [✅ Lo que se hizo bien](#-lo-que-se-hizo-bien)
7. [📐 Arquitectura Propuesta para v2](#-arquitectura-propuesta-para-v2)
8. [📋 Checklist para v2](#-checklist-para-v2)

---

## 🔴 Seguridad — Crítico

### 1. Clave AES inválida — el cifrado biométrico nunca funciona

**Archivo:** `Helpers/Constants.cs:6-7`

```csharp
BIOMETRIC_KEY = "SecretKeyForBiom3tricPassword001"; // 34 bytes
BIOMETRIC_IV = "BiometricIV12345";                   // 16 bytes
```

AES requiere claves de exactamente **16, 24 o 32 bytes** (`Aes.Key` setter lanza `CryptographicException` si no). La clave tiene **34 bytes** → el método `Encrypt(string)` y `Decrypt(string)` **siempre lanzan excepción**. El feature completo de guardar contraseña para biometría **nunca funcionó en producción**.

**Solución v2:** Usar `Rfc2898DeriveBytes` (PBKDF2) para derivar clave AES de 32 bytes desde una frase, o mejor, usar `ProtectedData` (Windows) / Keychain (iOS/macOS) / `EncryptedSharedPreferences` (Android) directamente sin AES casero.

---

### 2. Key Derivation inexistente

**Archivo:** `Services/Implementation/EncryptionService.cs:127-133`

```csharp
private byte[] GetAesKey(string pass)
{
    byte[] keyBytes = Encoding.UTF8.GetBytes(pass);
    while (keyBytes.Length < 32)
        keyBytes = keyBytes.Concat(keyBytes).ToArray();
    return keyBytes.Take(32).ToArray();
}
```

Sin PBKDF2, sin Argon2, sin bcrypt, sin sal, sin iteraciones. Una contraseña de 6 caracteres produce una clave AES de 32 bytes en **nanosegundos**. Un ataque de fuerza bruta es trivial.

**Solución v2:** Usar `Rfc2898DeriveBytes(pass, salt, iterations, HashAlgorithmName.SHA256)` con al menos 600,000 iteraciones y un salt único por usuario.

---

### 3. SSL validation bypass en DEBUG

**Archivo:** `Services/Implementation/ApiService.cs:21-23`

```csharp
#if DEBUG
handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif
```

Acepta cualquier certificado SSL. Si un desarrollador compila en Debug y distribuye el APK/IPA, todos los datos viajan sin verificación de identidad del servidor (MITM).

**Solución v2:** No deshabilitar validación SSL. Usar certificados de desarrollo firmados o un `.cer` embebido para entornos de prueba.

---

### 4. SqlToken viajando en URL

**Archivo:** `Services/Implementation/CoreDataService.cs:100`

```csharp
var response = await _apiService.GetAsync<IEnumerable<CoreSecretData>>(
    $"{Constants.CORE_CRUD_ENDPOINT}?User_Id={coreUserRequest.User_Id}&SqlToken={coreUserRequest.SqlToken}");
```

El token de base de datos se transmite como query parameter GET. Esto expone el token en:
- Logs del servidor
- Headers `Referer`
- Historial del proxy corporativo
- Pantalla si alguien ve el teléfono

**Solución v2:** Usar POST con body, o headers `X-Sql-Token` + `X-User-Id` en cada request.

---

### 5. Generación de contraseñas con System.Random

**Archivo:** `ViewModels/PasswordFormViewModel.cs:199-203`

```csharp
var random = new Random(); // System.Random
```

`System.Random` es predecible si se conoce la semilla. Un atacante que observe la hora del sistema puede reproducir las contraseñas generadas.

**Solución v2:** Usar `RandomNumberGenerator.GetString(chars, length)` de .NET 8+ o `RandomNumberGenerator.GetBytes()`.

---

### 6. Secretos hardcodeados en el binario

**Archivo:** `Helpers/Constants.cs`

| Secreto | Valor | Impacto |
|---------|-------|---------|
| `API_KEY` | `Esmerilemelo-777` | Acceso a API sin autenticación |
| `BIOMETRIC_KEY` | `SecretKeyForBiom3tricPassword001` | Descifrar contraseñas guardadas |
| `BIOMETRIC_IV` | `BiometricIV12345` | Descifrar contraseñas guardadas |
| `API_BASE_URL` | `https://artema.bsite.net` | Endpoint de producción visible |

Las constantes string se incrustan en el IL y se extraen con cualquier decompilador (ILSpy, dnSpy, etc.).

**Solución v2:**
- `API_KEY` debe venir del servidor tras login
- La clave de cifrado local debe derivarse del password del usuario + un salt aleatorio almacenado en SecureStorage
- Para desarrollo, usar configuration json excluida del repo (`appsettings.Development.json` en `.gitignore`)

---

## 🔴 Arquitectura — Crítico

### 7. Service Layer depende de View Layer

**Archivo:** `Services/Implementation/SessionManager.cs:86-87`

```csharp
var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
Application.Current!.Windows[0].Page = loginPage;
```

Un servicio de dominio (`SessionManager`) crea instancias de páginas y manipula el árbol visual directamente. Esto es una **violación de dependencias en todas las capas**: la capa de servicios conoce y depende de la capa de presentación.

**Solución v2:**
- Crear un `INavigationService` que abstraiga `Shell.GoToAsync`, `PushModalAsync`, etc.
- Los servicios devuelven resultados, no navegan. La navegación se dispara desde ViewModels o desde un mediador.

```csharp
// Bien
public interface INavigationService
{
    Task GoToLoginAsync();
    Task GoToMainAsync();
    Task<T?> ShowModalAsync<T>(string pageKey, object? parameter = null);
}

// Mal
public class SessionManager
{
    public async Task Logout() {
        var page = _serviceProvider.GetRequiredService<LoginPage>();
        Application.Current!.Windows[0].Page = page; // NO
    }
}
```

---

### 8. ViewModel crea Pages directamente (acoplamiento rígido)

**Ejemplo en todos los ViewModels:**

```csharp
var page = _serviceProvider.GetRequiredService<PasswordFormPage>();
await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(page);
```

El ViewModel conoce la implementación concreta de la View (`PasswordFormPage`), el método de navegación (`PushModalAsync`), y el contexto visual (`Windows[0].Page!`). Esto hace imposible:
- Unit testear el ViewModel
- Cambiar a otra plataforma de navegación
- Reutilizar el ViewModel con otra View

**Solución v2:**
- Todo ViewModel usa `INavigationService` para navegar
- Las páginas se resuelven por DI y se mapean con `Routing.RegisterRoute`

```csharp
// Bien
[RelayCommand]
async Task CreateSecret()
{
    var result = await _navigationService.ShowModalAsync<CoreSecretData?>("PasswordForm", new { Mode = "Create" });
}

// Mal
[RelayCommand]
async Task CreateSecret()
{
    var page = _serviceProvider.GetRequiredService<PasswordFormPage>();
    await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(page); // NO
}
```

---

### 9. HttpClient inyectado pero ignorado

**Archivo:** `Services/Implementation/ApiService.cs:16-29`

```csharp
public ApiService(HttpClient httpClient) // ← Recibe HttpClient
{
    var handler = new HttpClientHandler();
    // ...
    _httpClient = new HttpClient(handler) // ← Crea otro, ignora el inyectado
    {
        BaseAddress = new Uri(Constants.API_BASE_URL),
        Timeout = TimeSpan.FromSeconds(30)
    };
}
```

El `HttpClient` registrado en DI (`builder.Services.AddSingleton<HttpClient>()`) **nunca se usa**. La instancia interna creada no comparte connection pool, no respeta configuración global, y puede causar socket exhaustion.

**Solución v2:**
- Usar `IHttpClientFactory` en lugar de `HttpClient` singleton
- Configurar `BaseAddress`, headers, timeouts via `AddHttpClient<IApiService, ApiService>()`
- No crear `HttpClientHandler` manualmente

---

### 10. Interface hinchada — NotImplementedException

**Archivo:** `Services/Implementation/SessionManager.cs`

```csharp
public TimeSpan GetRemainingTime()       => throw new NotImplementedException();
public void InitializeSession(int)       => throw new NotImplementedException();
public bool IsSessionExpired()            => throw new NotImplementedException();
public Task PerformFullLogoutAsync(string?) => throw new NotImplementedException();
public void UpdateSessionTime()          => throw new NotImplementedException();
```

**5 de 6 métodos** de la interface `ISessionManager` lanzan `NotImplementedException`. Esto significa que la interface está mal diseñada (viola Interface Segregation Principle) — agrupa responsabilidades que no pertenecen a una misma abstracción.

**Solución v2:**
- Separar `ISessionManager` en interfaces más pequeñas:
  - `IAuthSession` → LoginAsync, LogoutAsync
  - `ISessionTimer` → StartTimer, RemainingTime, IsExpired
  - `ISessionPersistence` → SaveSession, LoadSession, ClearSession
- Cada implementación concreta implementa solo lo que necesita

---

### 11. ViewModel Singleton

**Archivo:** `MauiProgram.cs:75`

```csharp
builder.Services.AddSingleton<TestingViewModel>();
```

Los ViewModels deben ser **Transient** o **Scoped**. Un ViewModel Singleton acumula estado entre navegaciones: si el usuario abre TestingPage, escribe algo, navega a otro lado y vuelve, el estado anterior persiste. Esto causa bugs difíciles de reproducir.

**Solución v2:** Todos los ViewModels registrados como `AddTransient<T>()`.

---

### 12. Sin NavigationService

Actualmente la navegación está implementada de 4 formas distintas e inconsistentes:

| Ubicación | Técnica |
|-----------|---------|
| `LoginViewModel` | `Application.Current!.Windows[0].Page = appShell` |
| `SettingsViewModel` | `Application.Current!.MainPage = loginPage` |
| `PasswordDetailsViewModel` | `Application.Current!.Windows[0].Page!.Navigation.PushModalAsync()` |
| `PasswordFormViewModel` | `Shell.Current.GoToAsync("..")` |
| `PasswordPromptCreateViewModel` | `Application.Current!.Windows[0].Page!.Navigation.PopAsync()` |

**Solución v2:**
- Una sola clase `NavigationService` que implementa `INavigationService`
- Todas las navegaciones pasan por ahí
- Soporta Shell navigation + modal + reset de la pila

---

### 13. Sin DialogService

Cada ViewModel llama directamente a:

```csharp
await Application.Current!.Windows[0].Page!.DisplayAlertAsync(...)
```

Esto es imposible de mockear en unit tests y frágil (crash si `Windows[0]` no existe).

**Solución v2:**
```csharp
public interface IDialogService
{
    Task ShowAlertAsync(string title, string message, string cancel = "OK");
    Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel);
    Task<string?> ShowPromptAsync(string title, string message);
}
```

---

### 14. ViewModel con lógica de código-behind conviviendo con MVVM

**Archivo:** `Views/Main/PasswordPromptPage.xaml.cs`

`PasswordPromptPage` usa code-behind con `TaskCompletionSource<string?>` en vez de ViewModel. Esto rompe la consistencia del patrón MVVM.

Además, el `CompletionSource` es una propiedad pública mutable en la Page. Si alguien reusa la página sin reiniciar el TCS, el await cuelga para siempre.

**Solución v2:** Unificar todo a ViewModels con CommunityToolkit.Mvvm. Centralizar `TaskCompletionSource` en un servicio de navegación con resultados.

---

## 🟡 Calidad de Código

### 15. Logger tipado incorrecto

**Archivo:** `ViewModels/PasswordFormViewModel.cs:11`

```csharp
private readonly ILogger<PasswordDetailsViewModel> _logger;
```

Usa `ILogger<PasswordDetailsViewModel>` en lugar de `ILogger<PasswordFormViewModel>`. Todas las entradas de log aparecen como si vinieran del ViewModel equivocado. Esto mata la depuración en producción.

---

### 16. Catch block vacío

**Archivo:** `ViewModels/LoginViewModel.cs:250-254`

```csharp
catch (Exception ex)
{
    // ← completamente vacío
}
```

Una excepción en `OpenUrl` se traga sin log, sin feedback al usuario, sin nada.

---

### 17. Fire-and-forget sin manejo de errores

En múltiples lugares:

```csharp
_ = PerformSearchAsync();
_ = Application.Current!.Windows[0].Page!.Navigation.PopAsync();
```

Cualquier excepción no capturada en estas tasks **derriba la aplicación** (UnobservedTaskException → App crash). En `PopAsync` ni siquiera hay un try-catch.

**Solución v2:** Crear un helper `SafeFireAndForget` con manejo de excepciones global, o mejor, evitar fire-and-forget en lo posible.

---

### 18. Dead code y código comentado

| Archivo | Líneas | Problema |
|---------|--------|----------|
| `ApiService.cs` | 170-195 | Método `DeleteAsync` comentado (coexiste con otro igual) |
| `PasswordFormViewModel.cs` | 169 | `//await Shell.Current.GoToAsync("..");` |
| `PasswordPromptPage.xaml.cs` | 34-48 | Bloque de toggle comentado |
| `EncryptionService.cs` | 183, 211 | Variable `msg` asignada y nunca usada |

---

### 19. Naming críptico e inconsistente

| Nombre actual | Problema | Propuesta |
|---------------|----------|-----------|
| `Data01`, `Data02`, `Data03` | No dice qué son | `Name`, `Username`, `Password` |
| `IsPassword` | Confuso: ¿es una contraseña? | `IsPasswordHidden` o `ShowPassword` |
| `Data_Id`, `User_Id` | Snake_case mezclado con PascalCase | `DataId`, `UserId` |
| Route `"psssword"` | Typo en `AppShell.xaml:19` | `"secrets"` |

---

### 20. Manejo de errores duplicado con mismo pattern

Cada método en los services tiene esta estructura (ejemplo de `CoreDataService.cs`):

```csharp
try
{
    // ...
    if (!response.IsSuccess || response.Data == null)
        throw new Exception($"Failed to... StatusCode: {response.StatusCode}, Message: {response.Message}");
    return response.Data;
}
catch (Exception ex)
{
    _logger.LogError(ex, "[...]");
    throw;
}
```

~100 líneas de código repetitivo idéntico en cada método. Cualquier change (ej: cambiar formato de log) requiere editar 10+ métodos.

**Solución v2:** Usar un wrapper genérico o un `ApiService` que ya maneje esto centralizadamente.

---

### 21. WeakReferenceMessenger sin cleanup

`LoginViewModel` se suscribe a `RegistrationCompletedMessage` en el constructor:

```csharp
WeakReferenceMessenger.Default.Register<RegistrationCompletedMessage>(this, ...);
```

Nunca llama a `Unregister`. Aunque `WeakReferenceMessenger` usa referencias débiles, el delegate captura el ViewModel y puede retrasar el GC. En ViewModels Transient que se crean/destruyen, esto acumula suscripciones fantasma.

**Solución v2:** Llamar a `WeakReferenceMessenger.Default.Unregister<RegistrationCompletedMessage>(this)` en `Cleanup()`.

---

## 🟡 MVVM y MAUI Anti-Patterns

### 22. Acceso frágil a la UI desde ViewModel

```csharp
Application.Current!.Windows[0].Page!.DisplayAlertAsync(...)
Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(...)
```

6+ operadores null-forgiving (`!`) por línea. Si en cualquier momento `Application.Current` es null, `Windows[0]` no existe, o `Page` no está asignada, la app crashea sin un mensaje útil.

---

### 23. System.Timers.Timer para UI

**Archivo:** `ViewModels/SettingsViewModel.cs:171`

```csharp
_sessionTimer = new System.Timers.Timer(1000);
_sessionTimer.Elapsed += SessionTimer_Elapsed;
```

`System.Timers.Timer` dispara en thread pool. Actualizar propiedades de UI desde ahí sin `MainThread.BeginInvokeOnMainThread` causa excepciones cross-thread o race conditions.

**Solución v2:** Usar `IDispatcherTimer` de MAUI que opera en el dispatcher de la UI:

```csharp
var timer = Application.Current!.Dispatcher.CreateTimer();
timer.Interval = TimeSpan.FromSeconds(1);
timer.Tick += (s, e) => UpdateSessionTime();
timer.Start();
```

O mejor, usar `PeriodicTimer` con `await Dispatcher.DispatchAsync()`.

---

### 24. CoreSecretData mezcla Model y ViewModel

**Archivo:** `Models/CoreSecretData.cs`

```csharp
public partial class CoreSecretData : ObservableObject
{
    public Guid Data_Id { get; set; }           // ← DTO de API
    public required string Data01 { get; set; }  // ← DTO de API
    // ...
    [ObservableProperty]                         // ← UI state
    public bool isExpanded = false;              // ← UI state
}
```

La misma clase es DTO de API (serializable) y ViewModel (observable). Si la API cambia el contrato, se rompe la UI. Si la UI cambia, se rompe la serialización.

**Solución v2:** Separar en:
- `SecretDto` → solo propiedades planas para API
- `SecretItemViewModel` → observable, con commands, wrapping del DTO

---

### 25. Validación duplicada

**Archivo:** `ViewModels/LoginViewModel.cs`

```csharp
private void ValidateForm() { /* email + password length check */ }
private bool ValidateFieldsDetailed() { /* exactamente las mismas validaciones con mensajes */ }
```

Un método habilita el botón, el otro se ejecuta al enviar. La lógica de validación está duplicada y puede desincronizarse.

**Solución v2:** Usar `CommunityToolkit.Mvvm` validators o FluentValidation con una sola fuente de verdad.

---

## 🟡 Patrones Ausentes

### 26. Sin Repository Pattern

`CoreDataService` llama directamente a `ApiService`. No hay abstracción entre la fuente de datos y el consumo. No hay caché, ni offline, ni cambio de backend sin modificar el servicio.

**Solución v2:**
```
ICoreDataRepository → CoreDataApiRepository (online)
                   → CoreDataLocalRepository (offline cache)
```

### 27. Sin Unit of Work

Las operaciones de Create y Update dependen de `GetCoreUserIVAsync` que es otra llamada HTTP. Si esa falla a mitad, no hay rollback.

### 28. Sin Global Exception Handler

No hay `AppDomain.CurrentDomain.UnhandledException` ni `TaskScheduler.UnobservedTaskException`. Los crash silenciosos no se loguean.

### 29. Sin configuración externa

Toda la configuración está en `Constants.cs` compilada en el binario. No hay `appsettings.json`, no hay variables de entorno, no hay perfiles (dev/staging/prod).

### 30. Sin Polly / Resilience

Si la API no responde (timeout, 5xx), la app muestra error y termina. No hay reintentos, ni circuit breaker, ni timeout policies.

---

## ✅ Lo que se hizo bien

No todo es malo. Esto se rescata para v2:

| Acierto | Detalle |
|---------|---------|
| **CommunityToolkit.Mvvm** | Uso de `[ObservableProperty]`, `[RelayCommand]`, source generators |
| **Dependency Injection** | DI bien configurada en `MauiProgram.cs` con separación de servicios/VMs/páginas |
| **Interfaces para servicios** | Cada servicio tiene su interface, programación contra abstracciones |
| **WeakReferenceMessenger** | Comunicación entre VMs sin acoplamiento (Register→Login) |
| **SecureStorage para sesión** | Tokens y datos sensibles guardados con la API nativa del SO |
| **ThemeService** | Soporte Light/Dark/Auto con persistencia |
| **BiometricService** | Abstracción sobre Plugin.Maui.Biometric |
| **Estructura de carpetas** | Separación clara Models/ViewModels/Views/Services/DTOs |
| **Localización a español** | UI y mensajes consistentes en español |
| **Constants_demo.cs** | Template para open-source, aunque el real está hardcodeado |

---

## 📐 Arquitectura Propuesta para v2

```
PasswordManager.sln
│
├── PasswordManager.Core/                  ← netstandard2.0 / net10.0
│   ├── Models/                            ← DTOs, entidades (sin ObservableObject)
│   ├── Interfaces/                        ← Repositories, Services, Navigation, Dialog
│   ├── Services/                          ← Lógica de dominio pura
│   ├── Security/                          ← EncryptionService (PBKDF2 + AES-GCM)
│   └── Exceptions/                        ← ApiException, etc
│
├── PasswordManager.Infrastructure/        ← Implementaciones concretas
│   ├── Api/                               ← ApiService, HttpClientFactory, Polly
│   ├── Storage/                           ← SecureStorage, SQLite (caché offline)
│   ├── Navigation/                        ← NavigationService (Shell)
│   └── Platform/                          ← BiometricService, ThemeService (platform-specific)
│
├── PasswordManager.Application/           ← MVVM layer
│   ├── ViewModels/                        ← Solo VMs, sin reference a Views
│   ├── Converters/                        ← Value converters
│   ├── Behaviors/                         ← Platform behaviors
│   └── Messages/                          ← WeakReferenceMessenger messages
│
└── PasswordManager.Maui/                  ← MAUI head (slim)
    ├── Views/                             ← Pages (XAML + code-behind mínimo)
    ├── Resources/                         ← Fonts, Images, Styles
    ├── Platforms/                         ← Platform-specific code
    ├── App.xaml / AppShell.xaml
    └── MauiProgram.cs                     ← DI registration
```

### Principios de diseño para v2

| Principio | Cómo aplicarlo |
|-----------|----------------|
| **Single Responsibility** | Cada clase hace una cosa. `AuthService` no guarda contraseñas. `SettingsViewModel` no maneja timers. |
| **Interface Segregation** | Interfaces pequeñas y focused. No más `IAuthService` con 12 métodos. |
| **Dependency Inversion** | Core/Application no reference Infrastructure/Maui. Infrastructure implementa interfaces de Core. |
| **Composition over Inheritance** | No más `[ObservableProperty]` en Models. Usar wrapping o composición. |
| **Explicit Dependencies** | Constructor injection para todo. No más `Application.Current!.Windows[0]`. |
| **Fail Fast** | Las configuraciones inválidas se detectan al startup, no en runtime. |
| **Security by Design** | No hardcodear secrets. No deshabilitar SSL. Usar KDF estándar. |

---

---

## 🚀 Estrategia de Deploy / CI/CD

La solución contiene **dos proyectos deployables** independientes y un proyecto compartido de contratos:

```
PasswordManager.slnx (solo organización local)
│
├── src/
│   ├── PasswordManager.Api/         → Se deploya como contenedor Docker
│   ├── PasswordManager.Maui/        → Se compila a APK/IPA/MSIX
│   └── PasswordManager.Contracts/   → No se deploya (solo referencia)
```

Cada pipeline de CI/CD apunta al `.csproj` específico — el `.slnx` no se usa en build automático:

```yaml
# API pipeline: apunta a src/PasswordManager.Api/PasswordManager.Api.csproj
# MAUI pipeline: apunta a src/PasswordManager.Maui/PasswordManager.Maui.csproj
```

Los deploys son **independientes**: puedes actualizar la API sin tocar el cliente, y vicecersa.

---

### API — Docker / Contenedor

```dockerfile
# src/PasswordManager.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/PasswordManager.Api/", "PasswordManager.Api/"]
COPY ["src/PasswordManager.Contracts/", "PasswordManager.Contracts/"]
RUN dotnet restore "PasswordManager.Api/PasswordManager.Api.csproj"
RUN dotnet publish "PasswordManager.Api/PasswordManager.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PasswordManager.Api.dll"]
```

```yaml
# .github/workflows/api-deploy.yml
name: Deploy API
on:
  push:
    branches: [main]
    paths: ['src/PasswordManager.Api/**']
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet publish src/PasswordManager.Api -c Release
      - run: docker build -t passwordmanager-api src/PasswordManager.Api
      - run: docker push registry/passwordmanager-api
```

---

### MAUI — Compilación a binarios nativos

MAUI no se "deploya" como servicio — se **compila** a binarios específicos por plataforma:

| Plataforma | Comando | Artifacto |
|------------|---------|-----------|
| Android | `dotnet publish -f net10.0-android -c Release` | `.apk` / `.aab` |
| iOS | `dotnet publish -f net10.0-ios -c Release` | `.ipa` |
| Windows | `dotnet publish -f net10.0-windows10.0.19041.0 -c Release` | `.msix` |
| macOS | `dotnet publish -f net10.0-maccatalyst -c Release` | `.app` |

El pipeline firma el binario y lo sube como artifact para distribución manual o automática (App Center, Google Play, App Store).

```yaml
# .github/workflows/maui-build-android.yml
name: Build Android APK
on:
  push:
    tags: ['v*']
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet publish src/PasswordManager.Maui
              -f net10.0-android -c Release -o artifacts/
              -p:AndroidSigningKeyStore=${{ secrets.ANDROID_KEYSTORE }}
              -p:AndroidSigningStorePass=${{ secrets.ANDROID_STORE_PASS }}
      - uses: actions/upload-artifact@v4
        with:
          name: android-apk
          path: artifacts/*.apk
```

> **Nota:** A diferencia de la API, MAUI no corre como servicio. No hay "docker run" ni "deploy continuo" — produces un artifact que distribuyes en stores o mediante sideloading.

---

## 🤔 Pregunta Frecuente

### ¿Por qué API y MAUI en la misma solución si se deployan separados?

El `.slnx` es solo para **organización local en Visual Studio**. No afecta los pipelines:

- **Misma solución** → facilidad de navegación, debugging simultáneo, un solo repo
- **Proyectos separados** → cada `.csproj` se build/deploya independientemente
- **Contracts compartido** → los DTOs se mantienen en un solo lugar sin duplicación

Si prefieres repos separados, también es válido. En ese caso, `PasswordManager.Contracts` se publica como NuGet package interno.

---

## 📋 Checklist para v2

### Seguridad (impostergable)
- [ ] Reemplazar `GetAesKey()` por `Rfc2898DeriveBytes` con salt e iteraciones
- [ ] Eliminar `Constants.BIOMETRIC_KEY` y `Constants.BIOMETRIC_IV` — derivar clave del password
- [ ] Eliminar `Constants.API_KEY` hardcodeada — obtenerla del servidor post-login
- [ ] No deshabilitar SSL validation nunca, ni en Debug
- [ ] Mover SqlToken de query string a header HTTP
- [ ] Reemplazar `System.Random` por `RandomNumberGenerator` en generación de passwords
- [ ] Usar `AesGcm` (modo GCM) en vez de `Aes` (modo CBC por defecto) para AEAD

### Arquitectura
- [ ] Implementar `INavigationService` y eliminar todo `Application.Current!.Windows[0].Page` de VMs
- [ ] Implementar `IDialogService`
- [ ] Eliminar `NotImplementedException`s — diseñar interfaces correctas
- [ ] Corregir `HttpClient` — usar `IHttpClientFactory`
- [ ] Separar `CoreSecretData` en DTO (`SecretDto`) y ViewModel (`SecretItemViewModel`)
- [ ] Todos los ViewModels como Transient
- [ ] Eliminar el código-behind de `PasswordPromptPage` — crear ViewModel

### Código
- [ ] Corregir `ILogger<PasswordFormViewModel>` en PasswordFormViewModel
- [ ] Eliminar catch blocks vacíos
- [ ] Manejar tareas fire-and-forget con `SafeFireAndForget` o evitar el patrón
- [ ] Renombrar `Data01/02/03` → `Name/Username/Password`
- [ ] Renombrar `IsPassword` → `IsPasswordHidden`
- [ ] Eliminar código comentado
- [ ] Cleanup de `WeakReferenceMessenger` subscriptions
- [ ] Unificar formato de query parameters nullable con argument-null-checking

### Patrones
- [ ] Agregar `IHttpClientFactory` con Polly para resiliencia (retry, circuit breaker)
- [ ] Agregar configuración externa (`appsettings.json` por ambiente)
- [ ] Agregar Global Exception Handler
- [ ] Agregar analizadores Roslyn en el `.csproj` (`.editorconfig`, `roslynator`, `sonaranalyzer`)
- [ ] Agregar unit tests (al menos para `EncryptionService` y `ApiService`)
- [ ] Agregar `FluentValidation` para validaciones de formularios
- [ ] Agregar caché offline con SQLite (opcional pero recomendado para un password manager)

### Notas técnicas adicionales

```xml
<!-- .csproj improvements -->
<PropertyGroup>
  <AnalysisLevel>latest-recommended</AnalysisLevel>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

```csharp
// appsettings.json en vez de Constants.cs
{
  "Api": {
    "BaseUrl": "https://artema.bsite.net",
    "TimeoutSeconds": 30
  },
  "Security": {
    "Pbkdf2Iterations": 600000,
    "KeySizeBytes": 32
  }
}
```
