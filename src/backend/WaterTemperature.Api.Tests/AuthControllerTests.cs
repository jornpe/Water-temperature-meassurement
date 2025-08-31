using System.Net;
using WaterTemperature.Api.Models.Auth;
using WaterTemperature.Api.Tests.Infrastructure;

namespace WaterTemperature.Api.Tests;

/// <summary>
/// Comprehensive integration tests for the authentication controller covering 
/// registration, login, password validation, and various edge cases.
/// </summary>
public class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    #region User Existence Tests

    [Fact]
    public async Task CheckUsersExist_WhenUserExists_ReturnsTrue()
    {
        // Act
        var response = await Client.GetAsync("/api/auth/users/exists");
        
        // Assert
        AssertSuccessStatusCode(response);
        
        var result = await DeserializeResponseAsync<UserExistsResponse>(response);
        Assert.NotNull(result);
        Assert.True(result.Exists);
    }

    [Fact]
    public async Task CheckUsersExist_WhenNoUsers_ReturnsFalse()
    {
        // Arrange - Clear all users
        using (var context = GetDbContext())
        {
            context.Users.RemoveRange(context.Users);
            await context.SaveChangesAsync();
        }
        
        // Act
        var response = await Client.GetAsync("/api/auth/users/exists");
        
        // Assert
        AssertSuccessStatusCode(response);
        
        var result = await DeserializeResponseAsync<UserExistsResponse>(response);
        Assert.NotNull(result);
        Assert.False(result.Exists);
    }

    #endregion

    #region Registration Tests

    [Fact]
    public async Task Register_ValidUser_WhenNoUsersExist_ReturnsSuccess()
    {
        // Arrange - Clear all users first
        using (var context = GetDbContext())
        {
            context.Users.RemoveRange(context.Users);
            await context.SaveChangesAsync();
        }

        var registerRequest = new RegisterRequest(
            UserName: "newuser",
            Password: "SecurePass123!",
            Email: "newuser@example.com",
            FirstName: "New",
            LastName: "User"
        );

        // Act
        var response = await Client.PostAsync("/api/auth/register", CreateJsonContent(registerRequest));
        
        // Assert
        AssertSuccessStatusCode(response);
        
        var result = await DeserializeResponseAsync<UserCreatedResponse>(response);
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("newuser", result.UserName);
    }

    [Fact]
    public async Task Register_WhenUserAlreadyExists_ReturnsConflict()
    {
        // Arrange
        var registerRequest = new RegisterRequest(
            UserName: "anotheruser",
            Password: "SecurePass123!",
            Email: "another@example.com"
        );

        // Act
        var response = await Client.PostAsync("/api/auth/register", CreateJsonContent(registerRequest));
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.Conflict);
        
        var result = await DeserializeResponseAsync<MessageResponse>(response);
        Assert.NotNull(result);
        Assert.Equal("Only one user can exist", result.Message);
    }

    [Fact]
    public async Task Register_WithEmptyUsername_ReturnsBadRequest()
    {
        // Arrange
        await ResetDatabaseAsync(); // Clear any existing users for clean test
        var registerRequest = new RegisterRequest(
            UserName: "",
            Password: "SecurePass123!"
        );

        // Act
        var response = await Client.PostAsync("/api/auth/register", CreateJsonContent(registerRequest));
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithEmptyPassword_ReturnsBadRequest()
    {
        // Arrange
        await ResetDatabaseAsync(); // Clear any existing users for clean test
        var registerRequest = new RegisterRequest(
            UserName: "validuser",
            Password: ""
        );

        // Act
        var response = await Client.PostAsync("/api/auth/register", CreateJsonContent(registerRequest));
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        // Arrange - Clear users first
        using (var context = GetDbContext())
        {
            context.Users.RemoveRange(context.Users);
            await context.SaveChangesAsync();
        }

        var registerRequest = new RegisterRequest(
            UserName: "validuser",
            Password: "123"  // Too short based on MinPasswordLength setting
        );

        // Act
        var response = await Client.PostAsync("/api/auth/register", CreateJsonContent(registerRequest));
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
        
        var result = await DeserializeResponseAsync<MessageResponse>(response);
        Assert.NotNull(result);
        Assert.Contains("Password must be at least", result.Message);
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndProfile()
    {
        // Arrange
        var loginRequest = new LoginRequest(
            UserName: TestAuthenticationHelper.TestUserName,
            Password: TestAuthenticationHelper.TestPassword
        );

        // Act
        var response = await Client.PostAsync("/api/auth/login", CreateJsonContent(loginRequest));
        
        // Assert
        AssertSuccessStatusCode(response);
        
        var result = await DeserializeResponseAsync<LoginResponse>(response);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.True(result.ExpiresIn > 0);
        Assert.NotNull(result.Profile);
        Assert.Equal(TestAuthenticationHelper.TestUserName, result.Profile.UserName);
        
        // Validate the token
        var decodedToken = TestAuthenticationHelper.ValidateAndDecodeToken(result.Token);
        Assert.NotNull(decodedToken);
    }

    [Fact]
    public async Task Login_InvalidUsername_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest(
            UserName: "nonexistentuser",
            Password: TestAuthenticationHelper.TestPassword
        );

        // Act
        var response = await Client.PostAsync("/api/auth/login", CreateJsonContent(loginRequest));
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest(
            UserName: TestAuthenticationHelper.TestUserName,
            Password: "wrongpassword"
        );

        // Act
        var response = await Client.PostAsync("/api/auth/login", CreateJsonContent(loginRequest));
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_EmptyCredentials_ReturnsBadRequest()
    {
        // Arrange
        var loginRequest = new LoginRequest(
            UserName: "",
            Password: ""
        );

        // Act
        var response = await Client.PostAsync("/api/auth/login", CreateJsonContent(loginRequest));
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Profile Tests

    [Fact]
    public async Task GetProfile_WithValidToken_ReturnsUserProfile()
    {
        // Act
        var response = await AuthenticatedClient.GetAsync("/api/auth/profile");
        
        // Assert
        AssertSuccessStatusCode(response);
        
        var profile = await DeserializeResponseAsync<UserProfileResponse>(response);
        Assert.NotNull(profile);
        Assert.Equal(TestAuthenticationHelper.TestUserName, profile.UserName);
        Assert.Equal("test@example.com", profile.Email);
        Assert.Equal("Test", profile.FirstName);
        Assert.Equal("User", profile.LastName);
    }

    [Fact]
    public async Task GetProfile_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/api/auth/profile");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var updateRequest = new UpdateProfileRequest(
            Email: "updated@example.com",
            FirstName: "Updated",
            LastName: "Name"
        );

        // Act
        var response = await AuthenticatedClient.PutAsync("/api/auth/profile", CreateJsonContent(updateRequest));
        
        // Assert
        AssertSuccessStatusCode(response);
        
        // Verify the update was applied
        var profileResponse = await AuthenticatedClient.GetAsync("/api/auth/profile");
        var profile = await DeserializeResponseAsync<UserProfileResponse>(profileResponse);
        Assert.NotNull(profile);
        Assert.Equal("updated@example.com", profile.Email);
        Assert.Equal("Updated", profile.FirstName);
        Assert.Equal("Name", profile.LastName);
    }

    #endregion

    #region Password Change Tests

    [Fact]
    public async Task ChangePassword_WithValidCurrentPassword_ReturnsSuccess()
    {
        // Arrange
        var changePasswordRequest = new ChangePasswordRequest(
            CurrentPassword: TestAuthenticationHelper.TestPassword,
            NewPassword: "NewSecurePass123!"
        );

        // Act
        var response = await AuthenticatedClient.PostAsync("/api/auth/profile/change-password", CreateJsonContent(changePasswordRequest));
        
        // Assert
        AssertSuccessStatusCode(response);
        
        // Verify we can login with the new password
        var loginRequest = new LoginRequest(TestAuthenticationHelper.TestUserName, "NewSecurePass123!");
        var loginResponse = await Client.PostAsync("/api/auth/login", CreateJsonContent(loginRequest));
        AssertSuccessStatusCode(loginResponse);
    }

    [Fact]
    public async Task ChangePassword_WithInvalidCurrentPassword_ReturnsUnauthorized()
    {
        // Arrange
        var changePasswordRequest = new ChangePasswordRequest(
            CurrentPassword: "wrongpassword",
            NewPassword: "NewSecurePass123!"
        );

        // Act
        var response = await AuthenticatedClient.PostAsync("/api/auth/profile/change-password", CreateJsonContent(changePasswordRequest));
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithWeakNewPassword_ReturnsBadRequest()
    {
        // Arrange
        var changePasswordRequest = new ChangePasswordRequest(
            CurrentPassword: TestAuthenticationHelper.TestPassword,
            NewPassword: "123"  // Too weak
        );

        // Act
        var response = await AuthenticatedClient.PostAsync("/api/auth/profile/change-password", CreateJsonContent(changePasswordRequest));
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task Auth_WithMalformedJson_ReturnsBadRequest()
    {
        // Arrange
        var malformedJson = "{invalid json}";
        var content = new StringContent(malformedJson, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/auth/login", content);
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Auth_WithNullRequest_ReturnsBadRequest()
    {
        // Arrange
        var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/auth/login", content);
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    #endregion
}
