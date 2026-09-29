using System.Security.Cryptography;
using WebApiCore.Infrastructure.Security;

namespace WebApiCore.Tests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    private const string LegacyPassword = "LegacyPassword";
    private const int LegacyIterationCount = 100_000;

    [Fact]
    public void HashPassword_GeneratesDifferentSaltAndHashPerCall()
    {
        var first = _hasher.HashPassword("SecretPassword");
        var second = _hasher.HashPassword("SecretPassword");

        Assert.NotEqual(first.Hash, second.Hash);
        Assert.NotEqual(first.Salt, second.Salt);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        var (hash, salt) = _hasher.HashPassword("SecretPassword");

        Assert.True(_hasher.VerifyPassword("SecretPassword", hash, salt));
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        var (hash, salt) = _hasher.HashPassword("SecretPassword");

        Assert.False(_hasher.VerifyPassword("WrongPassword", hash, salt));
    }

    [Fact]
    public void VerifyPassword_IsDeterministic_ForSameHashAndSalt()
    {
        var (hash, salt) = _hasher.HashPassword("SecretPassword");

        Assert.True(_hasher.VerifyPassword("SecretPassword", hash, salt));
        Assert.True(_hasher.VerifyPassword("SecretPassword", hash, salt));
    }

    [Fact]
    public void HashPassword_WithProvidedSalt_MatchesGeneratedSaltHash()
    {
        var (hash, salt) = _hasher.HashPassword("SecretPassword");
        string hashWithSalt = _hasher.HashPassword("SecretPassword", salt);

        Assert.Equal(hash, hashWithSalt);
        Assert.True(_hasher.VerifyPassword("SecretPassword", hashWithSalt, salt));
        Assert.False(_hasher.VerifyPassword("WrongPassword", hashWithSalt, salt));
    }

    [Fact]
    public void HashPassword_WithProvidedSalt_UsesCurrentIterationCount()
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
        string salt = Convert.ToBase64String(saltBytes);
        string expected = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(
            "SecretPassword",
            saltBytes,
            600_000,
            HashAlgorithmName.SHA256,
            32));

        string actual = _hasher.HashPassword("SecretPassword", salt);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void VerifyPassword_WithLegacyIterationHash_ReturnsTrue()
    {
        var (hash, salt) = LegacyHash(LegacyPassword);

        Assert.True(_hasher.VerifyPassword(LegacyPassword, hash, salt));
    }

    [Fact]
    public void VerifyPassword_WithLegacyIterationHashAndWrongPassword_ReturnsFalse()
    {
        var (hash, salt) = LegacyHash(LegacyPassword);

        Assert.False(_hasher.VerifyPassword("WrongPassword", hash, salt));
    }

    private static (string Hash, string Salt) LegacyHash(string password)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
        string hash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            LegacyIterationCount,
            HashAlgorithmName.SHA256,
            32));

        return (hash, Convert.ToBase64String(saltBytes));
    }
}