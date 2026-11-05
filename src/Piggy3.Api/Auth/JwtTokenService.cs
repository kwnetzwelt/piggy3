using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Piggy3.Api.Auth;

public class JwtTokenService
{
    private readonly string _secret;
    private readonly SymmetricSecurityKey _key;

    public JwtTokenService(IConfiguration configuration)
    {
        _secret = configuration["AUTH_JWT_SECRET"] ?? string.Empty;
        if (_secret.Length < 32)
            throw new InvalidOperationException("AUTH_JWT_SECRET must be at least 32 characters.");

        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
    }

    public string CreateToken(string username)
    {
        var token = new JwtSecurityToken(
            issuer: "piggy3",
            audience: "piggy3",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, username)],
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
