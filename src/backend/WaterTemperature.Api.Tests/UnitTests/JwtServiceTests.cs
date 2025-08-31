using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterTemperature.Api.Configuration;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Services;

namespace WaterTemperature.Api.Tests.UnitTests;

/// <summary>
/// Unit tests for the JwtService class.
/// </summary>
public class JwtServiceTests
{
    private readonly ILogger<JwtService> _logger;
    private readonly JwtSettings _jwtSettings;
    private readonly IOptions<JwtSettings> _jwtOptions;

    public JwtServiceTests()
    {
        _logger = new LoggerFactory().CreateLogger<JwtService>();
        _jwtSettings = new JwtSettings
        {
            Secret = "this-is-a-test-secret-key-with-minimum-256-bits-for-HS256-algorithm-testing",
            TokenLifetimeHours = 24
        };
        _jwtOptions = Options.Create(_jwtSettings);
    }

    [Fact]
    public void CreateToken_ValidUser_ReturnsValidJwtToken()
    {
        // Arrange
        var jwtService = new JwtService(_logger, _jwtOptions);
        var user = new User
        {
            Id = 1,
            UserName = "testuser",
            Email = "test@example.com",
            IsAdmin = false
        };

        // Act
        var token = jwtService.CreateToken(user);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(token));
        
        // Validate the token structure
        var tokenHandler = new JwtSecurityTokenHandler();
        Assert.True(tokenHandler.CanReadToken(token));
        
        var jwtToken = tokenHandler.ReadJwtToken(token);
        Assert.NotNull(jwtToken);
    }

    [Fact]
    public void CreateToken_ValidUser_ContainsExpectedClaims()
    {
        // Arrange
        var jwtService = new JwtService(_logger, _jwtOptions);
        var user = new User
        {
            Id = 123,
            UserName = "testuser",
            Email = "test@example.com",
            IsAdmin = true
        };

        // Act
        var token = jwtService.CreateToken(user);

        // Assert
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);
        
        // Check subject claim
        var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.NotNull(subClaim);
        Assert.Equal("123", subClaim.Value);
        
        // Check name claim
        var nameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name);
        Assert.NotNull(nameClaim);
        Assert.Equal("testuser", nameClaim.Value);
        
        // Check email claim
        var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);
        Assert.NotNull(emailClaim);
        Assert.Equal("test@example.com", emailClaim.Value);
        
        // Check admin claim
        var adminClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "admin");
        Assert.NotNull(adminClaim);
        Assert.Equal("True", adminClaim.Value);
    }

    [Fact]
    public void CreateToken_UserWithNullEmail_HandlesGracefully()
    {
        // Arrange
        var jwtService = new JwtService(_logger, _jwtOptions);
        var user = new User
        {
            Id = 1,
            UserName = "testuser",
            Email = null, // Null email
            IsAdmin = false
        };

        // Act
        var token = jwtService.CreateToken(user);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(token));
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);
        
        var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);
        Assert.NotNull(emailClaim);
        Assert.Equal(string.Empty, emailClaim.Value);
    }

    [Fact]
    public void CreateToken_ValidUser_SetsCorrectExpiry()
    {
        // Arrange
        var customSettings = new JwtSettings
        {
            Secret = "this-is-a-test-secret-key-with-minimum-256-bits-for-HS256-algorithm-testing",
            TokenLifetimeHours = 12
        };
        var customOptions = Options.Create(customSettings);
        var jwtService = new JwtService(_logger, customOptions);
        var user = new User { Id = 1, UserName = "testuser", IsAdmin = false };

        var beforeTokenCreation = DateTime.UtcNow;

        // Act
        var token = jwtService.CreateToken(user);

        // Assert
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);
        
        var expectedExpiry = beforeTokenCreation.AddHours(12);
        var actualExpiry = jwtToken.ValidTo;
        
        // Allow for small time differences (within 1 minute)
        var timeDifference = Math.Abs((expectedExpiry - actualExpiry).TotalMinutes);
        Assert.True(timeDifference < 1, $"Token expiry time difference too large: {timeDifference} minutes");
    }

    [Fact]
    public void GetSigningKey_ReturnsValidKey()
    {
        // Arrange
        var jwtService = new JwtService(_logger, _jwtOptions);

        // Act
        var signingKey = jwtService.GetSigningKey();

        // Assert
        Assert.NotNull(signingKey);
        Assert.True(signingKey.KeySize >= 256); // Minimum key size for HS256
    }

    [Fact]
    public void Constructor_EmptySecret_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidSettings = new JwtSettings
        {
            Secret = "",
            TokenLifetimeHours = 24
        };
        var invalidOptions = Options.Create(invalidSettings);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new JwtService(_logger, invalidOptions));
        
        Assert.Contains("JWT secret is not configured", exception.Message);
    }

    [Fact]
    public void Constructor_ShortSecret_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidSettings = new JwtSettings
        {
            Secret = "tooshort", // Less than 32 characters
            TokenLifetimeHours = 24
        };
        var invalidOptions = Options.Create(invalidSettings);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new JwtService(_logger, invalidOptions));
        
        Assert.Contains("JWT secret too short", exception.Message);
    }

    [Fact]
    public void CreateToken_MultipleTokensForSameUser_AreDifferent()
    {
        // Arrange
        var jwtService = new JwtService(_logger, _jwtOptions);
        var user = new User
        {
            Id = 1,
            UserName = "testuser",
            Email = "test@example.com",
            IsAdmin = false
        };

        // Act
        var token1 = jwtService.CreateToken(user);
        Thread.Sleep(1000); // Ensure different timestamps
        var token2 = jwtService.CreateToken(user);

        // Assert
        Assert.NotEqual(token1, token2);
        
        // But they should have the same claims
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt1 = tokenHandler.ReadJwtToken(token1);
        var jwt2 = tokenHandler.ReadJwtToken(token2);
        
        var userId1 = jwt1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var userId2 = jwt2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        Assert.Equal(userId1, userId2);
    }
}
