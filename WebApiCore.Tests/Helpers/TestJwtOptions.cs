using WebApiCore.Infrastructure.Options;

namespace WebApiCore.Tests.Helpers;

public static class TestJwtOptions
{
    public static JwtOptions Create() => new()
    {
        Key = "TestKeyTestKeyTestKeyTestKeyTestKeyTestKey",
        Issuer = "TestIssuer",
        Audience = "TestAudience",
        Subject = "TestSubject",
        ExpireMin = 60
    };
}