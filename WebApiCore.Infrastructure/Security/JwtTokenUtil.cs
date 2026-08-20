using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using WebApiCore.Application.Interfaces;
using WebApiCore.Domain.Entities;
using WebApiCore.Infrastructure.Options;

namespace WebApiCore.Infrastructure.Security;

public class JwtTokenUtil : IAuthTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenUtil(JwtOptions options)
    {
        _options = options;
    }

    public TokenResult GenerateToken(AuthUser user)
    {
        var claimsIdentity = new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.User_Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role ?? "USER")
        });

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = claimsIdentity,
            Expires = DateTime.UtcNow.AddMinutes(_options.ExpireMin),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var handler = new JsonWebTokenHandler
        {
            SetDefaultTimesOnTokenCreation = false
        };

        return new TokenResult(handler.CreateToken(descriptor), _options.ExpireMin);
    }
}