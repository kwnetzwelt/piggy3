using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Piggy3.Api.Auth;

public class JwtTokenService
{
    public const string Issuer = "piggy3";
    public const string Audience = "piggy3";
    public const string TwoFactorAudience = "piggy3-2fa";

    private readonly SymmetricSecurityKey _key;

    public JwtTokenService(IConfiguration configuration)
    {
        var secret = configuration["AUTH_JWT_SECRET"] ?? string.Empty;
        if (secret.Length < 32)
            throw new InvalidOperationException("AUTH_JWT_SECRET must be at least 32 characters.");

        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public string CreateToken(string username) =>
        WriteToken(username, Audience, DateTime.UtcNow.AddDays(7), []);

    public string CreateChallengeToken(string username) =>
        WriteToken(username, TwoFactorAudience, DateTime.UtcNow.AddMinutes(5),
            [new Claim("purpose", "2fa")]);

    public bool TryValidateChallengeToken(string? token, out string? username)
    {
        username = null;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = TwoFactorAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            if (principal.FindFirst("purpose")?.Value != "2fa")
                return false;

            username = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                       ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return !string.IsNullOrEmpty(username);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private string WriteToken(
        string username,
        string audience,
        DateTime expires,
        IEnumerable<Claim> extraClaims)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, username) };
        claims.AddRange(extraClaims);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
