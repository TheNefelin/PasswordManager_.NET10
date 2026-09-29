using System.Security.Cryptography;
using WebApiCore.Application.Interfaces;

namespace WebApiCore.Infrastructure.Security;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;

    // PBKDF2-SHA256 con 600k iteraciones (recomendación OWASP 2023 para SHA-256).
    private const int CurrentIterationCount = 600_000;

    // Conteos anteriores, conservados SOLO para verificar hashes ya persistidos:
    // los hashes legacy se re-derivan con el valor actual únicamente cuando el
    // usuario re-registra o cambia la clave maestra.
    private static readonly int[] LegacyIterationCounts = { 100_000 };

    public (string Hash, string Salt) HashPassword(string password)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        string hash = NewHash(password, saltBytes, CurrentIterationCount);

        return (hash, Convert.ToBase64String(saltBytes));
    }

    public string HashPassword(string password, string salt)
    {
        return NewHash(password, Convert.FromBase64String(salt), CurrentIterationCount);
    }

    public bool VerifyPassword(string password, string hashedPassword, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);

        // Costo aceptado del fallback: un hash legacy (100k) duplica la derivación
        // por intento (600k + 100k) y el tiempo de respuesta delata si la cuenta
        // usa iteraciones legacy. Leak menor asumido por diseño (los legacy se
        // re-derivan al re-registrar o cambiar la clave maestra) y mitigado por
        // el lockout y el rate limit a nivel de IP.
        if (NewHash(password, saltBytes, CurrentIterationCount) == hashedPassword)
            return true;

        foreach (int iterations in LegacyIterationCounts)
        {
            if (NewHash(password, saltBytes, iterations) == hashedPassword)
                return true;
        }

        return false;
    }

    private static string NewHash(string password, byte[] salt, int iterations)
    {
        return Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeySize));
    }
}