using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
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
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.User_Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role ?? "USER")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(_options.ExpireMin),
            signingCredentials: creds);

        return new TokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            _options.ExpireMin);
    }
}