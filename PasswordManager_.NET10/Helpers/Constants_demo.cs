namespace PasswordManager_.NET10.Helpers;

public static class Constants_demo
{
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