using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KeyFactorDashboard.Data;
using KeyFactorDashboard.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KeyFactorDashboard.Services;

public sealed class TokenIssuer
{
    private readonly AuthOptions _opt;

    public TokenIssuer(IOptions<AuthOptions> opt) => _opt = opt.Value;

    public (string Token, DateTimeOffset Expires) Issue(PlantUser user)
    {
        var expires = DateTimeOffset.UtcNow.AddHours(Math.Clamp(_opt.LifetimeHours, 1, 72));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey.PadRight(32)));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("displayName", user.DisplayName)
        };
        var jwt = new JwtSecurityToken(
            _opt.Issuer,
            _opt.Audience,
            claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}
