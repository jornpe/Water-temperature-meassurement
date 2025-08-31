using System.Net;
using WaterTemperature.Api.Tests.Infrastructure;

namespace WaterTemperature.Api.Tests.IntegrationTests;

/// <summary>
/// Tests for error handling, edge cases, and various failure scenarios across the application.
/// </summary>
public class ErrorHandlingTests : IntegrationTestBase
{
    public ErrorHandlingTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    #region HTTP Method Tests

    [Fact]
    public async Task UnsupportedHttpMethod_ReturnsMethodNotAllowed()
    {
        // Act - Try PATCH on an endpoint that doesn't support it
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "/api/temperatures"));
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task NonExistentEndpoint_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/nonexistent");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    #endregion

    #region Content Type Tests

    [Fact]
    public async Task PostWithoutContentType_ReturnsBadRequest()
    {
        // Arrange
        var content = new StringContent("{\"userName\":\"test\",\"password\":\"test\"}", System.Text.Encoding.UTF8);
        content.Headers.ContentType = null; // Remove content type
        
        // Act
        var response = await Client.PostAsync("/api/auth/login", content);
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task PostWithInvalidContentType_ReturnsUnsupportedMediaType()
    {
        // Arrange
        var content = new StringContent("{\"userName\":\"test\",\"password\":\"test\"}", System.Text.Encoding.UTF8, "text/plain");
        
        // Act
        var response = await Client.PostAsync("/api/auth/login", content);
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.UnsupportedMediaType);
    }

    #endregion

    #region Large Request Tests

    [Fact]
    public async Task LargeJsonPayload_HandlesGracefully()
    {
        // Arrange - Create a very large username
        var largeUsername = new string('a', 10000);
        var largeRequest = new
        {
            userName = largeUsername,
            password = "Test123!"
        };
        
        // Act
        var response = await Client.PostAsync("/api/auth/login", CreateJsonContent(largeRequest));
        
        // Assert
        // Should handle gracefully - either BadRequest for validation or Unauthorized for wrong credentials
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest || 
                   response.StatusCode == HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Malformed Request Tests

    [Fact]
    public async Task EmptyRequestBody_ReturnsBadRequest()
    {
        // Arrange
        var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
        
        // Act
        var response = await Client.PostAsync("/api/auth/login", content);
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidJsonSyntax_ReturnsBadRequest()
    {
        // Arrange
        var invalidJson = "{\"userName\":\"test\", \"password\":}"; // Missing value
        var content = new StringContent(invalidJson, System.Text.Encoding.UTF8, "application/json");
        
        // Act
        var response = await Client.PostAsync("/api/auth/login", content);
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Authentication Header Tests

    [Fact]
    public async Task MalformedAuthorizationHeader_ReturnsUnauthorized()
    {
        // Arrange
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not.a.valid.jwt.token");
        
        // Act
        var response = await client.GetAsync("/api/temperatures");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredToken_ReturnsUnauthorized()
    {
        // Arrange - Generate expired token
        var expiredToken = TestAuthenticationHelper.GenerateJwtToken(expireMinutes: -60); // Expired 1 hour ago
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);
        
        // Act
        var response = await client.GetAsync("/api/temperatures");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MissingBearerPrefix_ReturnsUnauthorized()
    {
        // Arrange
        var token = TestAuthenticationHelper.GenerateJwtToken();
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("InvalidScheme", token);
        
        // Act
        var response = await client.GetAsync("/api/temperatures");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Special Characters and Encoding Tests

    [Fact]
    public async Task SpecialCharactersInUsername_HandlesCorrectly()
    {
        // Arrange
        var loginRequest = new
        {
            userName = "test@user.com+special!chars#123",
            password = "Test123!"
        };
        
        // Act
        var response = await Client.PostAsync("/api/auth/login", CreateJsonContent(loginRequest));
        
        // Assert
        // Should return Unauthorized (user doesn't exist) rather than a server error
        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnicodeCharactersInRequest_HandlesCorrectly()
    {
        // Arrange
        var loginRequest = new
        {
            userName = "用户名", // Chinese characters
            password = "пароль123" // Cyrillic characters
        };
        
        // Act
        var response = await Client.PostAsync("/api/auth/login", CreateJsonContent(loginRequest));
        
        // Assert
        // Should return Unauthorized rather than a server error
        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    #endregion

    #region SQL Injection and Security Tests

    [Fact]
    public async Task SqlInjectionAttempt_DoesNotCauseServerError()
    {
        // Arrange
        var loginRequest = new
        {
            userName = "admin'; DROP TABLE Users; --",
            password = "Test123!"
        };
        
        // Act
        var response = await Client.PostAsync("/api/auth/login", CreateJsonContent(loginRequest));
        
        // Assert
        // Should return Unauthorized, not cause a server error
        AssertStatusCode(response, HttpStatusCode.Unauthorized);
        
        // Verify users table still exists by checking user existence
        var usersExistResponse = await Client.GetAsync("/api/auth/users/exists");
        AssertSuccessStatusCode(usersExistResponse);
    }

    #endregion

    #region Concurrent Request Tests

    [Fact]
    public async Task ConcurrentAuthenticationRequests_HandleCorrectly()
    {
        // Arrange
        var loginRequest = new
        {
            userName = TestAuthenticationHelper.TestUserName,
            password = TestAuthenticationHelper.TestPassword
        };
        
        var tasks = new List<Task<HttpResponseMessage>>();
        
        // Act - Send 10 concurrent login requests
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Client.PostAsync("/api/auth/login", CreateJsonContent(loginRequest)));
        }
        
        var responses = await Task.WhenAll(tasks);
        
        // Assert
        foreach (var response in responses)
        {
            AssertSuccessStatusCode(response);
        }
        
        // Clean up
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    #endregion

    #region Database Connection Tests

    [Fact]
    public async Task DatabaseOperations_WithValidContext_Succeed()
    {
        // Act & Assert - Basic database operations should work
        using var context = GetDbContext();
        
        // Should be able to query users
        var userCount = context.Users.Count();
        Assert.True(userCount >= 0);
        
        // Should be able to check database connection
        var canConnect = await context.Database.CanConnectAsync();
        Assert.True(canConnect);
    }

    #endregion

    #region Rate Limiting Simulation

    [Fact]
    public async Task MultipleFailedLoginAttempts_HandleGracefully()
    {
        // Arrange
        var invalidLoginRequest = new
        {
            userName = "nonexistent",
            password = "invalid"
        };
        
        // Act - Simulate multiple failed login attempts
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Client.PostAsync("/api/auth/login", CreateJsonContent(invalidLoginRequest)));
        }
        
        var responses = await Task.WhenAll(tasks);
        
        // Assert - All should return Unauthorized, not cause server errors
        foreach (var response in responses)
        {
            AssertStatusCode(response, HttpStatusCode.Unauthorized);
        }
        
        // Clean up
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    #endregion
}
