using WebApiCore.Infrastructure.Security;

namespace WebApiCore.Tests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

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
}