using Microsoft.EntityFrameworkCore;

namespace WaterTemperature.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
}
