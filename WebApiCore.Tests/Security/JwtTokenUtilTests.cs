using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WebApiCore.Domain.Entities;
using WebApiCore.Infrastructure.Security;
using WebApiCore.Tests.Helpers;

namespace WebApiCore.Tests.Security;

public class JwtTokenUtilTests
{
    private readonly JwtTokenUtil _tokenUtil = new(TestJwtOptions.Create());

    private static AuthUser CreateUser(string? role = null) => new()
    {
        User_Id = Guid.NewGuid(),
        Email = "user@example.com",
        HashLogin = "hash",
        SaltLogin = "salt",
        Role = role
    };

    [Fact]
    public void GenerateToken_ContainsExpectedClaims()
    {
        var user = CreateUser();
        var result = _tokenUtil.GenerateToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        Assert.Equal(user.User_Id.ToString(), token.Subject);
        Assert.Equal(user.Email, token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("USER", token.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Contains(token.Claims, c => c.Type == JwtRegisteredClaimNames.Jti);
    }

    [Fact]
    public void GenerateToken_ExpiresInConfiguredMinutes()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(_tokenUtil.GenerateToken(CreateUser()).Token);
        var expected = DateTime.UtcNow.AddMinutes(60);

        var deviation = (expected - token.ValidTo).Duration();

        Assert.True(deviation <= TimeSpan.FromMinutes(1), $"exp desviado en {deviation}.");
    }

    [Fact]
    public void GenerateToken_HasCorrectIssuerAndAudience()
    {
        var options = TestJwtOptions.Create();
        var tokenUtil = new JwtTokenUtil(options);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenUtil.GenerateToken(CreateUser()).Token);

        Assert.Equal(options.Issuer, token.Issuer);
        Assert.Equal(options.Audience, token.Audiences.Single());
    }

    [Fact]
    public void GenerateToken_WithNullRole_DefaultsToUser()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(_tokenUtil.GenerateToken(CreateUser(role: null)).Token);

        Assert.Equal("USER", token.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void GenerateToken_VerifiesWithIssuerSigningKey()
    {
        var options = TestJwtOptions.Create();
        var tokenUtil = new JwtTokenUtil(options);
        var result = tokenUtil.GenerateToken(CreateUser());

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
            ClockSkew = TimeSpan.Zero
        };

        var principal = handler.ValidateToken(result.Token, validationParameters, out _);

        Assert.NotNull(principal);
    }
}