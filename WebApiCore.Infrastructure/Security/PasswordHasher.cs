using System.Security.Cryptography;
using WebApiCore.Application.Interfaces;

namespace WebApiCore.Infrastructure.Security;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int IterationCount = 100_000;

    public (string Hash, string Salt) HashPassword(string password)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        string hash = NewHash(password, saltBytes);

        return (hash, Convert.ToBase64String(saltBytes));
    }

    public bool VerifyPassword(string password, string hashedPassword, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        string hash = NewHash(password, saltBytes);

        return hash == hashedPassword;
    }

    private static string NewHash(string password, byte[] salt)
    {
        return Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            IterationCount,
            HashAlgorithmName.SHA256,
            KeySize));
    }
}