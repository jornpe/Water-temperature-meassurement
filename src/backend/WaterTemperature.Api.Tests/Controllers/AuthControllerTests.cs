using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using WaterTemperature.Api.Controllers;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Models.Auth;
using WaterTemperature.Api.Configuration;
using WaterTemperature.Api.Services;
using Xunit;

namespace WaterTemperature.Api.Tests.Controllers;

/// <summary>
/// Unit tests for the AuthController covering authentication, registration, 
/// and user management functionality with mocked dependencies.
/// Implements Context7 best practices with xUnit lifecycle management.
/// </summary>
public class AuthControllerTests : IAsyncLifetime, IDisposable
{
    private readonly Mock<IOptions<AuthenticationSettings>> _mockAuthSettings;
    private readonly Mock<IOptions<JwtSettings>> _mockJwtSettings;
    private readonly Mock<IJwtService> _mockJwtService;
    private AppDbContext _dbContext = null!;
    private AuthController _controller = null!;

    public AuthControllerTests()
    {
        // Setup mock settings - shared across tests as they're stateless
        _mockAuthSettings = new Mock<IOptions<AuthenticationSettings>>();
        _mockAuthSettings.Setup(x => x.Value).Returns(new AuthenticationSettings { MinPasswordLength = 6 });

        _mockJwtSettings = new Mock<IOptions<JwtSettings>>();
        _mockJwtSettings.Setup(x => x.Value).Returns(new JwtSettings 
        { 
            Secret = "SuperSecretTestKeyThatIsLongEnough123456", 
            TokenLifetimeHours = 24 
        });

        _mockJwtService = new Mock<IJwtService>();
    }

    /// <summary>
    /// xUnit native setup method - called before each test method.
    /// Creates fresh DbContext and controller instances for complete test isolation.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Create fresh in-memory database for each test
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);

        // Create controller with fresh dependencies
        _controller = new AuthController(
            _dbContext, 
            _mockAuthSettings.Object, 
            _mockJwtSettings.Object, 
            _mockJwtService.Object
        );

        // Reset mock interactions for each test
        _mockJwtService.Reset();

        await Task.CompletedTask;
    }

    /// <summary>
    /// xUnit native teardown method - called after each test method.
    /// Ensures proper cleanup and test isolation.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
    }

    public void Dispose()
    {
        // Synchronous dispose for IDisposable
        _dbContext?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region User Existence Tests

    [Fact]
    public async Task CheckUsersExist_WhenUserExists_ReturnsTrue()
    {
        // Arrange - Fresh context and controller already available
        _dbContext.Users.Add(new User 
        { 
            UserName = "testuser", 
            PasswordHash = "hashedpassword",
            Email = "test@example.com"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.CheckUsersExist();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<UserExistsResponse>(okResult.Value);
        Assert.True(response.Exists);
    }

    [Fact]
    public async Task CheckUsersExist_WhenNoUsers_ReturnsFalse()
    {
        // Act - Fresh, empty context already available
        var result = await _controller.CheckUsersExist();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<UserExistsResponse>(okResult.Value);
        Assert.False(response.Exists);
    }

    #endregion

    #region Registration Tests

    [Fact]
    public async Task Register_WithValidRequest_WhenNoUsersExist_ReturnsCreated()
    {
        // Arrange
        var registerRequest = new RegisterRequest("newuser", "ValidPass123", "newuser@example.com", "John", "Doe");

        // Act
        var result = await _controller.Register(registerRequest);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<UserCreatedResponse>(createdResult.Value);
        Assert.Equal("newuser", response.UserName);
        Assert.True(response.Id > 0);

        // Verify user was saved to database
        var savedUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserName == "newuser");
        Assert.NotNull(savedUser);
        Assert.Equal("newuser@example.com", savedUser.Email);
        Assert.Equal("John", savedUser.FirstName);
        Assert.Equal("Doe", savedUser.LastName);
    }

    [Fact]
    public async Task Register_WhenUserAlreadyExists_ReturnsConflict()
    {
        // Arrange
        _dbContext.Users.Add(new User 
        { 
            UserName = "existinguser", 
            PasswordHash = "hashedpass",
            Email = "existing@example.com"
        });
        await _dbContext.SaveChangesAsync();

        var registerRequest = new RegisterRequest("newuser", "ValidPass123", "newuser@example.com");

        // Act
        var result = await _controller.Register(registerRequest);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<MessageResponse>(conflictResult.Value);
        Assert.Equal("Only one user can exist", response.Message);
    }

    [Fact]
    public async Task Register_WithEmptyUsername_ReturnsBadRequest()
    {
        // Arrange
        var registerRequest = new RegisterRequest("", "ValidPass123", "test@example.com");

        // Act
        var result = await _controller.Register(registerRequest);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<MessageResponse>(badRequestResult.Value);
        Assert.Equal("Username and password required", response.Message);
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        // Arrange
        var registerRequest = new RegisterRequest("testuser", "123", "test@example.com"); // Too short

        // Act
        var result = await _controller.Register(registerRequest);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<MessageResponse>(badRequestResult.Value);
        Assert.Equal("Password must be at least 6 characters long", response.Message);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var registerRequest = new RegisterRequest("testuser", "ValidPass123", "invalid-email");

        // Act
        var result = await _controller.Register(registerRequest);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<MessageResponse>(badRequestResult.Value);
        Assert.Equal("Invalid email format", response.Message);
    }

    [Theory]
    [InlineData("ab")] // Too short
    [InlineData("a")] // Too short
    [InlineData("ThisUsernameIsWayTooLongAndShouldBeRejectedByValidation")] // Too long
    public async Task Register_WithInvalidUsernameLength_ReturnsBadRequest(string username)
    {
        // Arrange
        var registerRequest = new RegisterRequest(username, "ValidPass123", "test@example.com");

        // Act
        var result = await _controller.Register(registerRequest);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<MessageResponse>(badRequestResult.Value);
        Assert.Equal("Username must be between 3-50 characters", response.Message);
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndProfile()
    {
        // Arrange
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("password123");
        var user = new User
        {
            UserName = "testuser",
            PasswordHash = passwordHash,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe"
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var loginRequest = new LoginRequest("testuser", "password123");

        const string expectedToken = "mock.jwt.token";
        _mockJwtService.Setup(x => x.CreateToken(It.IsAny<User>())).Returns(expectedToken);

        // Act
        var result = await _controller.Login(loginRequest);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(okResult.Value);
        
        Assert.Equal(expectedToken, response.Token);
        Assert.Equal(24 * 3600, response.ExpiresIn); // 24 hours in seconds
        Assert.NotNull(response.Profile);
        Assert.Equal("testuser", response.Profile.UserName);
        Assert.Equal("test@example.com", response.Profile.Email);

        // Verify JWT service was called
        _mockJwtService.Verify(x => x.CreateToken(It.Is<User>(u => u.UserName == "testuser")), Times.Once);
    }

    [Fact]
    public async Task Login_WithInvalidUsername_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest("nonexistentuser", "password123");

        // Act
        var result = await _controller.Login(loginRequest);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        _mockJwtService.Verify(x => x.CreateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("correctpassword");
        var user = new User
        {
            UserName = "testuser",
            PasswordHash = passwordHash,
            Email = "test@example.com"
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var loginRequest = new LoginRequest("testuser", "wrongpassword");

        // Act
        var result = await _controller.Login(loginRequest);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        _mockJwtService.Verify(x => x.CreateToken(It.IsAny<User>()), Times.Never);
    }

    #endregion
}
