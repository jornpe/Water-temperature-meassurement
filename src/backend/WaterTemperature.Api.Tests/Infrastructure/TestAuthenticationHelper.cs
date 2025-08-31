using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using WaterTemperature.Api.Configuration;
using Microsoft.Extensions.Options;

namespace WaterTemperature.Api.Tests.Infrastructure;

/// <summary>
/// Provides utilities for authentication in integration tests, including JWT token generation
/// and authenticated HttpClient creation.
/// </summary>
public static class TestAuthenticationHelper
{
    /// <summary>
    /// Default test JWT secret used for signing tokens in tests.
    /// </summary>
    public const string TestJwtSecret = "this-is-a-test-secret-key-with-minimum-256-bits-for-HS256-algorithm-testing";
    
    /// <summary>
    /// Default test user credentials.
    /// </summary>
    public const string TestUserName = "testuser";
    public const string TestPassword = "Test123!";
    public const int TestUserId = 1;

    /// <summary>
    /// Generates a JWT token for testing purposes with the specified user ID and username.
    /// </summary>
    /// <param name="userId">The user ID to include in the token claims.</param>
    /// <param name="userName">The username to include in the token claims.</param>
    /// <param name="expireMinutes">Token expiration time in minutes (default: 60).</param>
    /// <returns>A JWT token string.</returns>
    public static string GenerateJwtToken(int userId = TestUserId, string userName = TestUserName, int expireMinutes = 60)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(TestJwtSecret);
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(expireMinutes);
        
        // For expired tokens, we need to set NotBefore in the past to avoid validation errors
        var notBefore = expireMinutes < 0 ? expires.AddMinutes(-1) : now;
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("id", userId.ToString()),
                new Claim("username", userName),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, userName)
            }),
            NotBefore = notBefore,
            Expires = expires,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Creates an authenticated HttpClient with a JWT token in the Authorization header.
    /// </summary>
    /// <param name="factory">The WebApplicationFactory to create the client from.</param>
    /// <param name="userId">The user ID for the token (default: TestUserId).</param>
    /// <param name="userName">The username for the token (default: TestUserName).</param>
    /// <returns>An HttpClient with authentication headers set.</returns>
    public static HttpClient CreateAuthenticatedClient(TestWebApplicationFactory factory, int userId = TestUserId, string userName = TestUserName)
    {
        var client = factory.CreateClient();
        var token = GenerateJwtToken(userId, userName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Validates that a JWT token is properly formatted and contains expected claims.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <returns>The validated JWT security token.</returns>
    public static JwtSecurityToken ValidateAndDecodeToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(TestJwtSecret);
        
        tokenHandler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.Zero
        }, out SecurityToken validatedToken);

        return (JwtSecurityToken)validatedToken;
    }

    /// <summary>
    /// Extracts the user ID claim from a JWT token.
    /// </summary>
    /// <param name="token">The JWT token.</param>
    /// <returns>The user ID from the token claims.</returns>
    public static int GetUserIdFromToken(string token)
    {
        var jwtToken = ValidateAndDecodeToken(token);
        var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
        return int.Parse(userIdClaim ?? throw new InvalidOperationException("User ID claim not found in token"));
    }

    /// <summary>
    /// Extracts the username claim from a JWT token.
    /// </summary>
    /// <param name="token">The JWT token.</param>
    /// <returns>The username from the token claims.</returns>
    public static string GetUsernameFromToken(string token)
    {
        var jwtToken = ValidateAndDecodeToken(token);
        var usernameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "username")?.Value;
        return usernameClaim ?? throw new InvalidOperationException("Username claim not found in token");
    }
}
