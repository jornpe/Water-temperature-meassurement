using Microsoft.EntityFrameworkCore;

namespace WaterTemperature.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<HomeAssistantIntegrationSettings> HomeAssistantIntegrationSettings => Set<HomeAssistantIntegrationSettings>();
    public DbSet<DeviceLogEntry> DeviceLogEntries => Set<DeviceLogEntry>();
    public DbSet<DeviceTemperatureHistory> DeviceTemperatureHistory => Set<DeviceTemperatureHistory>();
    public DbSet<DevicePositionHistory> DevicePositionHistory => Set<DevicePositionHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasIndex(device => device.DeviceIdentifier).IsUnique();

            entity.Property(device => device.DeviceIdentifier)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(device => device.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.Property(device => device.Name).HasMaxLength(100);
            entity.Property(device => device.Place).HasMaxLength(100);
            entity.Property(device => device.HomeAssistantDeviceName).HasMaxLength(100);
            entity.Property(device => device.FirmwareVersion).HasMaxLength(50);
            entity.Property(device => device.LastDiscoveryTransport).HasMaxLength(32);
            entity.Property(device => device.ApiKeyHash).HasMaxLength(256);
            entity.Property(device => device.PendingApiKeyProtected).HasMaxLength(1024);
            entity.Property(device => device.LatestNetworkTransport).HasMaxLength(32);
            entity.Property(device => device.LatestWifiLocalIp).HasMaxLength(64);
            entity.Property(device => device.LatestWifiSsid).HasMaxLength(128);
            entity.Property(device => device.LatestWifiBssid).HasMaxLength(32);
            entity.Property(device => device.LatestWifiGatewayIp).HasMaxLength(64);
            entity.Property(device => device.LatestWifiSubnetMask).HasMaxLength(64);
            entity.Property(device => device.LatestWifiDnsIp).HasMaxLength(64);
            entity.Property(device => device.LatestWifiMacAddress).HasMaxLength(32);
            entity.Property(device => device.LatestCellularLocalIp).HasMaxLength(64);
            entity.Property(device => device.LatestCellularSimStatus).HasMaxLength(64);
            entity.Property(device => device.LatestCellularOperator).HasMaxLength(128);

            entity.HasMany(device => device.TemperatureHistory)
                .WithOne(history => history.Device)
                .HasForeignKey(history => history.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(device => device.PositionHistory)
                .WithOne(history => history.Device)
                .HasForeignKey(history => history.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(device => device.LogEntries)
                .WithOne(entry => entry.Device)
                .HasForeignKey(entry => entry.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HomeAssistantIntegrationSettings>(entity =>
        {
            entity.HasKey(settings => settings.Id);
            entity.Property(settings => settings.Id).ValueGeneratedNever();
            entity.Property(settings => settings.Host).HasMaxLength(255);
            entity.Property(settings => settings.Username).HasMaxLength(255);
            entity.Property(settings => settings.PasswordProtected).HasMaxLength(4096);
            entity.ToTable(table => table.HasCheckConstraint("CK_HomeAssistantIntegrationSettings_Singleton", "\"Id\" = 1"));
        });

        modelBuilder.Entity<DeviceLogEntry>(entity =>
        {
            entity.Property(entry => entry.Level).HasMaxLength(32);
            entity.Property(entry => entry.Message).HasMaxLength(2048).IsRequired();
            entity.HasIndex(entry => new { entry.DeviceId, entry.SequenceNumber }).IsUnique();
            entity.HasIndex(entry => new { entry.DeviceId, entry.ReceivedAtUtc });
        });

        modelBuilder.Entity<DeviceTemperatureHistory>(entity =>
        {
            entity.Property(history => history.TemperatureCelsius).HasPrecision(6, 2);
            entity.HasIndex(history => new { history.DeviceId, history.RecordedAtUtc });
        });

        modelBuilder.Entity<DevicePositionHistory>(entity =>
        {
            entity.HasIndex(history => new { history.DeviceId, history.RecordedAtUtc });
        });
    }
}
