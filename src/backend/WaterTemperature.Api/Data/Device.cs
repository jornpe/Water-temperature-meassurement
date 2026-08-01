using System.ComponentModel.DataAnnotations;
using WaterTemperature.Api.Models.Devices;

namespace WaterTemperature.Api.Data;

public class Device
{
    [Key]
    public int Id { get; init; }

    [Required]
    [MaxLength(100)]
    public string DeviceIdentifier { get; set; } = string.Empty;

    public DeviceRegistrationStatus Status { get; set; } = DeviceRegistrationStatus.Unregistered;

    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(100)]
    public string? Place { get; set; }

    public bool PushToHomeAssistant { get; set; }

    [MaxLength(100)]
    public string? HomeAssistantDeviceName { get; set; }

    public int ReportIntervalSeconds { get; set; } = 30;
    public float BatteryFullAdcVoltage { get; set; } = BatteryPercentageCalculator.DefaultFullAdcVoltage;
    public float BatteryEmptyAdcVoltage { get; set; } = BatteryPercentageCalculator.DefaultEmptyAdcVoltage;
    public int DesiredConfigurationVersion { get; set; } = 1;
    public DateTime? DesiredConfigurationUpdatedAtUtc { get; set; }
    public int? ReportedConfigurationVersion { get; set; }
    public int? ReportedReportIntervalSeconds { get; set; }
    public DateTime? RuntimeConfigurationReportedAtUtc { get; set; }
    public DeviceConfigurationSyncStatus ConfigurationSyncStatus { get; set; } = DeviceConfigurationSyncStatus.Pending;
    public int ConfigurationSyncAttemptCount { get; set; }

    [MaxLength(512)]
    public string? ConfigurationSyncError { get; set; }

    public DateTime? ConfigurationSyncStatusUpdatedAtUtc { get; set; }

    [MaxLength(50)]
    public string? FirmwareVersion { get; set; }

    [MaxLength(32)]
    public string? LastDiscoveryTransport { get; set; }

    [MaxLength(256)]
    public string? ApiKeyHash { get; set; }

    [MaxLength(1024)]
    public string? PendingApiKeyProtected { get; set; }

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? RegisteredAtUtc { get; set; }
    public DateTime? LastDiscoveredAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? LastUpdateReceivedAtUtc { get; set; }
    public DateTime? ApiKeyCreatedAtUtc { get; set; }
    public DateTime? ApiKeyIssuedAtUtc { get; set; }
    public DateTime? ApiKeyLastUsedAtUtc { get; set; }

    public decimal? LatestTemperatureCelsius { get; set; }
    public DateTime? LatestTemperatureAtUtc { get; set; }

    public double? LatestLatitude { get; set; }
    public double? LatestLongitude { get; set; }
    public double? LatestAltitudeMeters { get; set; }
    public DateTime? LatestGpsTimeUtc { get; set; }
    public double? LatestSpeedKnots { get; set; }
    public double? LatestHdop { get; set; }
    public int? LatestSatellitesVisible { get; set; }
    public int? LatestSatellitesUsed { get; set; }
    public DateTime? LatestPositionAtUtc { get; set; }

    [MaxLength(32)]
    public string? LatestNetworkTransport { get; set; }

    [MaxLength(64)]
    public string? LatestWifiLocalIp { get; set; }

    public int? LatestWifiRssiDbm { get; set; }

    [MaxLength(128)]
    public string? LatestWifiSsid { get; set; }

    [MaxLength(32)]
    public string? LatestWifiBssid { get; set; }

    public int? LatestWifiChannel { get; set; }

    [MaxLength(64)]
    public string? LatestWifiGatewayIp { get; set; }

    [MaxLength(64)]
    public string? LatestWifiSubnetMask { get; set; }

    [MaxLength(64)]
    public string? LatestWifiDnsIp { get; set; }

    [MaxLength(32)]
    public string? LatestWifiMacAddress { get; set; }

    [MaxLength(64)]
    public string? LatestCellularLocalIp { get; set; }

    [MaxLength(64)]
    public string? LatestCellularSimStatus { get; set; }

    public bool? LatestCellularNetworkConnected { get; set; }
    public bool? LatestCellularGprsConnected { get; set; }

    [MaxLength(128)]
    public string? LatestCellularOperator { get; set; }

    public int? LatestCellularSignalQuality { get; set; }

    public bool? LatestBatteryModemReadingValid { get; set; }
    public int? LatestBatteryChargeState { get; set; }
    public BatteryState? LatestBatteryState { get; set; }
    public int? LatestBatteryModemMillivolts { get; set; }
    public float? LatestBatteryAdcVoltage { get; set; }
    public DateTime? LatestBatteryAtUtc { get; set; }

    public ICollection<DeviceTemperatureHistory> TemperatureHistory { get; init; } = new List<DeviceTemperatureHistory>();
    public ICollection<DevicePositionHistory> PositionHistory { get; init; } = new List<DevicePositionHistory>();
    public ICollection<DeviceLogEntry> LogEntries { get; init; } = new List<DeviceLogEntry>();
    public ICollection<DeviceBatteryHistory> BatteryHistory { get; init; } = new List<DeviceBatteryHistory>();
}
