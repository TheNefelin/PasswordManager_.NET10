namespace PasswordManager_.NET10.Helpers;

public static class Constants_demo
{
    // Biometric Encryption Configuration
    public const string BIOMETRIC_KEY = "YourFixedKeyHere1234567890123456";
    public const string BIOMETRIC_IV = "YoutIVKeyHere123";

    // API Configuration
    public const string API_BASE_URL = "https://api.yourapp.com";
    public const string API_KEY = "your-api-key-here";

    // Endpoints
    public const string REGISTER_ENDPOINT = "/api/auth/register";
    public const string LOGIN_ENDPOINT = "/api/auth/login";

    public const string CORE_REGISTER_PASSWORD_ENDPOINT = "/api/core/register-password";
    public const string CORE_GET_IV_ENDPOINT = "/api/core/get-iv";
    public const string CORE_CRUD_ENDPOINT = "/api/core";
}