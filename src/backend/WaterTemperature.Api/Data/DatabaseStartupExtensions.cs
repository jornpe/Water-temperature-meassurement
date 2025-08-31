using Microsoft.EntityFrameworkCore;

namespace WaterTemperature.Api.Data;

public static class DatabaseStartupExtensions
{
    public static async Task ConfigureDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
        
        await dbContext.EnsureDatabaseAsync(logger);
    }
    
    private static async Task EnsureDatabaseAsync(this AppDbContext dbContext, ILogger logger)
    {
        // Check if using InMemory provider (for testing)
        var isInMemory = dbContext.Database.ProviderName?.Contains("InMemory") == true;
        
        if (isInMemory)
        {
            logger.LogInformation("Using InMemory database provider. Ensuring database is created...");
            await dbContext.Database.EnsureCreatedAsync();
            logger.LogInformation("InMemory database created successfully.");
            return;
        }
        
        // For relational databases, use execution strategy and migrations
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            try
            {
                logger.LogInformation("Checking database connectivity...");
                
                // Check if we can connect to the database
                var canConnect = await dbContext.Database.CanConnectAsync();
                
                if (!canConnect)
                {
                    logger.LogInformation("Database not accessible. This may be expected for a fresh deployment.");
                }
                
                // Get pending migrations (this will create the database if it doesn't exist)
                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
                
                if (pendingMigrations.Any())
                {
                    logger.LogInformation("Applying {Count} pending migrations: {Migrations}", 
                        pendingMigrations.Count(), 
                        string.Join(", ", pendingMigrations));
                    
                    await dbContext.Database.MigrateAsync();
                    
                    logger.LogInformation("Database migrations completed successfully.");
                }
                else
                {
                    logger.LogInformation("Database is up to date. No pending migrations.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to configure database. Retrying...");
                throw; // Let the execution strategy handle retries
            }
        });
    }
}
