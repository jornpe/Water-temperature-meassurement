using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using WaterTemperature.Api.Data;

namespace WaterTemperature.Api.Tests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for integration testing that configures in-memory database
/// and test-specific services to isolate tests from external dependencies.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    static TestWebApplicationFactory()
    {
        // Set environment variables before any instance is created
        // This ensures they're available during Program.cs execution
        Environment.SetEnvironmentVariable("JwtSettings__Secret", TestAuthenticationHelper.TestJwtSecret);
        Environment.SetEnvironmentVariable("JwtSettings__TokenLifetimeHours", "24");
        Environment.SetEnvironmentVariable("AuthenticationSettings__MinPasswordLength", "6");
        Environment.SetEnvironmentVariable("ConnectionStrings__watertemp", "DataSource=:memory:");
        Environment.SetEnvironmentVariable("CorsSettings__AllowedOrigin", "http://localhost:3000");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Since Program.cs now conditionally configures the database based on environment,
            // and we set ASPNETCORE_ENVIRONMENT=Testing in the static constructor,
            // we don't need to override the database configuration here.
            
            // Just ensure the database is created and seeded
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<TestWebApplicationFactory>>();
            
            try
            {
                context.Database.EnsureCreated();
                SeedTestData(context);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred seeding the test database");
                throw;
            }
        });

        builder.UseEnvironment("Testing");
    }

    /// <summary>
    /// Seeds the test database with initial test data.
    /// </summary>
    private static void SeedTestData(AppDbContext context)
    {
        // Clear existing data
        context.Users.RemoveRange(context.Users);
        context.SaveChanges();

        // Add test user for authentication tests
        var testUser = new User
        {
            UserName = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(testUser);
        context.SaveChanges();
    }
}
