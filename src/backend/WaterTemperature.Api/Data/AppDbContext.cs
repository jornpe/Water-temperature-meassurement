using Microsoft.EntityFrameworkCore;

namespace WaterTemperature.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.UserName).IsUnique();
            b.HasIndex(x => x.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
            b.Property(x => x.UserName).IsRequired().HasMaxLength(50);
            b.Property(x => x.Email).HasMaxLength(100);
            b.Property(x => x.FirstName).HasMaxLength(50);
            b.Property(x => x.LastName).HasMaxLength(50);
            b.Property(x => x.PasswordHash).IsRequired();
            b.Property(x => x.ProfilePicture);
            b.Property(x => x.CreatedAt).HasDefaultValueSql("now() at time zone 'utc'");
        });
    }
}
