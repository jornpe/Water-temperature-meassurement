using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WaterTemperature.Api.Configuration;
using WaterTemperature.Api.Data;

namespace WaterTemperature.Api.Services;

public interface IJwtService
{
    string CreateToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
    SymmetricSecurityKey GetSigningKey();
}

/// <summary>
/// Implementation of JWT service for token operations.
/// </summary>
public class JwtService(ILogger<JwtService> logger, IOptions<JwtSettings> jwtSettings): IJwtService
{
    private readonly ILogger<JwtService> _logger = logger;
    private readonly SymmetricSecurityKey _signingKey = CreateSigningKey(jwtSettings.Value.Secret);

    public string CreateToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.UserName),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim("admin", user.IsAdmin.ToString())
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(jwtSettings.Value.TokenLifetimeHours);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expiry,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public SymmetricSecurityKey GetSigningKey()
    {
        return _signingKey;
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var generator = RandomNumberGenerator.Create();
        generator.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidateLifetime = false // Important: we don't validate the lifetime here
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var validatedToken);
            
            if (validatedToken is not JwtSecurityToken jwtToken || 
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    private static SymmetricSecurityKey CreateSigningKey(string jwtSecret)
    {
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            throw new InvalidOperationException("JWT secret is not configured. Set 'JwtSettings:Secret' in configuration.");
        }
        
        if (jwtSecret.Length < 32) // 256 bits
        {
            throw new InvalidOperationException($"JWT secret too short: {jwtSecret} characters. Provide at least 32 bits (32 bytes). ");
        }
        
        var rawSecret = Encoding.ASCII.GetBytes(jwtSecret);

        return new SymmetricSecurityKey(rawSecret);
    }
}
