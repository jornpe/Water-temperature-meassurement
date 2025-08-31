using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Tests.Infrastructure;

namespace WaterTemperature.Api.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests providing common setup, utilities, and helper methods.
/// Inherits from IClassFixture to share the TestWebApplicationFactory across test methods.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    protected readonly TestWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    protected readonly HttpClient AuthenticatedClient;

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        AuthenticatedClient = TestAuthenticationHelper.CreateAuthenticatedClient(factory);
    }

    /// <summary>
    /// Gets a new scope for accessing scoped services like DbContext.
    /// </summary>
    protected IServiceScope GetScope() => Factory.Services.CreateScope();

    /// <summary>
    /// Gets the database context for direct database operations in tests.
    /// </summary>
    protected AppDbContext GetDbContext()
    {
        var scope = GetScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    /// <summary>
    /// Resets the test database to a clean state by clearing all data and re-seeding test data.
    /// </summary>
    protected async Task ResetDatabaseAsync()
    {
        using var scope = GetScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Clear all data
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();

        // Re-seed test data
        var testUser = new User
        {
            UserName = TestAuthenticationHelper.TestUserName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestAuthenticationHelper.TestPassword),
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(testUser);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Serializes an object to JSON for HTTP request content.
    /// </summary>
    protected static StringContent CreateJsonContent(object obj)
    {
        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// Deserializes JSON response content to the specified type.
    /// </summary>
    protected static async Task<T?> DeserializeResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
    }

    /// <summary>
    /// Creates an authenticated HTTP client with a custom user ID and username.
    /// </summary>
    protected HttpClient CreateAuthenticatedClient(int userId, string userName)
    {
        return TestAuthenticationHelper.CreateAuthenticatedClient(Factory, userId, userName);
    }

    /// <summary>
    /// Asserts that an HTTP response has the expected status code.
    /// </summary>
    protected static void AssertStatusCode(HttpResponseMessage response, System.Net.HttpStatusCode expectedStatusCode)
    {
        if (response.StatusCode != expectedStatusCode)
        {
            var content = response.Content.ReadAsStringAsync().Result;
            throw new Exception($"Expected status code {expectedStatusCode}, but got {response.StatusCode}. Response content: {content}");
        }
    }

    /// <summary>
    /// Asserts that an HTTP response is successful (2xx status code).
    /// </summary>
    protected static void AssertSuccessStatusCode(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = response.Content.ReadAsStringAsync().Result;
            throw new Exception($"Expected success status code, but got {response.StatusCode}. Response content: {content}");
        }
    }

    public virtual void Dispose()
    {
        Client?.Dispose();
        AuthenticatedClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
