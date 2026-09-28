namespace PasswordManager_.NET10.Helpers;

public static class Constants_demo
{
    // AES-256 exige EXACTAMENTE 32 bytes y el IV EXACTAMENTE 16, asi que deben quedar
    // 32 y 16 caracteres ASCII: cualquier otro largo revienta en runtime, no al compilar.
    // EncryptionService.Encrypt/Decrypt hacen Encoding.UTF8.GetBytes() sobre estas cadenas
    // y se las pasan a aes.Key / aes.IV.
    //
    // Solo cifra el cache local de la contraseña de login (AuthService.SavePasswordAsync);
    // las contraseñas de la nube se cifran con GetAesKey(contraseña maestra) y el IV de
    // la API, asi que nada de esto viaja entre dispositivos.
    //
    // La versión de la app NO va acá: vive solo en el .csproj
    // (ApplicationDisplayVersion / ApplicationVersion) y la lee IAppInfoService.

    // Biometric Encryption Configuration
    public const string BIOMETRIC_KEY = "YourFixedKeyHere1234567890123456";
    public const string BIOMETRIC_IV = "YoutIVKeyHere123";
    public const string API_KEY = "your-api-key-here";
    public const string API_BASE_URL = "https://10.0.2.2:7286/api";
    //public const string API_BASE_URL = "https://api.yourapp.com/api";
    // API Configuration
    // La URL base incluye el prefijo /api y los endpoints son relativos a ella
    // (ej. "auth/login"). No hace falta barra final: ApiService la normaliza,
    // porque sin ella HttpClient perdería el prefijo al resolver la ruta.
}