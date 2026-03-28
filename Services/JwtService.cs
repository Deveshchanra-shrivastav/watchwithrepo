using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WatchWith.Models;

namespace WatchWith.Services;

public class JwtService
{
    private readonly IConfiguration _cfg;
    public JwtService(IConfiguration cfg) => _cfg = cfg;

    public string GenerateToken(AppUser user)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddDays(int.Parse(_cfg["Jwt:ExpiryDays"] ?? "30"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email,          user.Email ?? ""),
            new Claim(ClaimTypes.Name,           user.UserName ?? ""),
            new Claim("displayName",             user.DisplayName),
            new Claim("avatarInitials",          user.AvatarInitials),
            new Claim("avatarColor",             user.AvatarColor),
        };

        var token = new JwtSecurityToken(
            issuer:             _cfg["Jwt:Issuer"],
            audience:           _cfg["Jwt:Audience"],
            claims:             claims,
            expires:            expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
