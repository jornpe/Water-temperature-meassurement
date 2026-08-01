namespace WaterTemperature.Api.Models.Devices;

public record DeviceConfigurationResponse(
    int ReportIntervalSeconds,
    int DesiredConfigurationVersion = 1);

public record DeviceDiscoveryRequest(
    string DeviceId,
    string? FirmwareVersion,
    string? NetworkTransport);

public record DeviceDiscoveryResponse(
    string DeviceId,
    string Status,
    DeviceConfigurationResponse Configuration,
    string? ApiKey = null,
    DateTime? ApiKeyIssuedAtUtc = null);

public record DeviceSummaryResponse(
    int Id,
    string DeviceId,
    string Status,
    string? Name,
    string? Place,
    bool PushToHomeAssistant,
    decimal? LatestTemperatureCelsius,
    int? LatestBatteryPercentage,
    string? LatestBatteryStatus,
    DateTime? LatestBatteryAtUtc,
    DateTime? LastUpdateReceivedAtUtc,
    DateTime? LastDiscoveredAtUtc);

public record DeviceDesiredConfigurationResponse(
    int Version,
    int ReportIntervalSeconds,
    DateTime? UpdatedAtUtc);

public record DeviceRuntimeConfigurationResponse(
    int? AppliedConfigurationVersion,
    int? AppliedReportIntervalSeconds,
    DateTime? ReportedAtUtc);

public record DeviceConfigurationSyncStateResponse(
    string Status,
    int AttemptCount,
    int MaximumAttempts,
    string? Error,
    DateTime? UpdatedAtUtc);

public record DevicePositionSnapshotResponse(
    double? Latitude,
    double? Longitude,
    double? AltitudeMeters,
    DateTime? GpsTimeUtc,
    double? SpeedKnots,
    double? Hdop,
    int? SatellitesVisible,
    int? SatellitesUsed,
    DateTime? RecordedAtUtc);

public record DevicePositionHistoryPointResponse(
    long Id,
    double Latitude,
    double Longitude,
    double? AltitudeMeters,
    DateTime? GpsTimeUtc,
    double? SpeedKnots,
    double? Hdop,
    int? SatellitesVisible,
    int? SatellitesUsed,
    DateTime RecordedAtUtc);

public record DevicePositionHistoryResponse(
    int DeviceId,
    string DeviceIdentifier,
    DateTime? FromUtc,
    DateTime? ToUtc,
    long TotalCount,
    bool IsSampled,
    IReadOnlyList<DevicePositionHistoryPointResponse> Items);

public record DeviceTemperatureHistoryPointResponse(
    long Id,
    decimal TemperatureCelsius,
    DateTime RecordedAtUtc);

public record DeviceTemperatureHistoryResponse(
    int DeviceId,
    string DeviceIdentifier,
    DateTime? FromUtc,
    DateTime? ToUtc,
    long TotalCount,
    bool IsSampled,
    IReadOnlyList<DeviceTemperatureHistoryPointResponse> Items);

public record DeviceWifiDiagnosticsResponse(
    string? LocalIp,
    int? WifiRssiDbm,
    string? Ssid,
    string? Bssid,
    int? Channel,
    string? GatewayIp,
    string? SubnetMask,
    string? DnsIp,
    string? MacAddress);

public record DeviceCellularDiagnosticsResponse(
    string? LocalIp,
    string? SimStatus,
    bool? NetworkConnected,
    bool? GprsConnected,
    string? Operator,
    int? SignalQuality);

public record DeviceNetworkDiagnosticsResponse(
    string? Transport,
    DeviceWifiDiagnosticsResponse? Wifi,
    DeviceCellularDiagnosticsResponse? Cellular);

public record DeviceBatteryDiagnosticsResponse(
    bool? ModemReadingValid,
    int? ChargeState,
    BatteryState? BatteryState,
    int? Percentage,
    int? ModemMillivolts,
    float? AdcVoltage,
    DateTime? RecordedAtUtc);

public record DeviceBatteryHistoryPointResponse(
    long Id,
    bool ModemReadingValid,
    int ChargeState,
    BatteryState BatteryState,
    int Percentage,
    int ModemMillivolts,
    float AdcVoltage,
    DateTime RecordedAtUtc);

public record DeviceBatteryHistoryResponse(
    int DeviceId,
    string DeviceIdentifier,
    DateTime? FromUtc,
    DateTime? ToUtc,
    long TotalCount,
    bool IsSampled,
    IReadOnlyList<DeviceBatteryHistoryPointResponse> Items);

public record DeviceDetailResponse(
    int Id,
    string DeviceId,
    string Status,
    string? Name,
    string? Place,
    bool PushToHomeAssistant,
    string HomeAssistantDeviceName,
    string? FirmwareVersion,
    int ReportIntervalSeconds,
    float BatteryFullAdcVoltage,
    float BatteryEmptyAdcVoltage,
    DateTime CreatedAtUtc,
    DateTime? RegisteredAtUtc,
    DateTime? LastDiscoveredAtUtc,
    DateTime? LastSeenAtUtc,
    DateTime? LastUpdateReceivedAtUtc,
    decimal? LatestTemperatureCelsius,
    DateTime? LatestTemperatureAtUtc,
    DeviceDesiredConfigurationResponse DesiredConfiguration,
    DeviceRuntimeConfigurationResponse RuntimeConfiguration,
    bool HasPendingConfiguration,
    DeviceConfigurationSyncStateResponse ConfigurationSync,
    DevicePositionSnapshotResponse Position,
    DeviceNetworkDiagnosticsResponse NetworkDiagnostics,
    DeviceBatteryDiagnosticsResponse Battery,
    int TemperatureHistoryCount,
    int PositionHistoryCount);

public record DeviceRegistrationRequest(
    string Name,
    string Place,
    int ReportIntervalSeconds,
    bool PushToHomeAssistant = false,
    string? HomeAssistantDeviceName = null,
    float BatteryFullAdcVoltage = BatteryPercentageCalculator.DefaultFullAdcVoltage,
    float BatteryEmptyAdcVoltage = BatteryPercentageCalculator.DefaultEmptyAdcVoltage);

public record DeviceRegistrationResponse(
    int Id,
    string DeviceId,
    string Status,
    DeviceConfigurationResponse Configuration,
    DateTime ApiKeyIssuedAtUtc);

public record DeviceApiKeyRegenerationResponse(
    int Id,
    string DeviceId,
    DateTime ApiKeyIssuedAtUtc,
    DeviceConfigurationResponse Configuration);

public record RegisteredDeviceUpdateRequest(
    string Name,
    string Place,
    int ReportIntervalSeconds,
    bool PushToHomeAssistant = false,
    string? HomeAssistantDeviceName = null,
    float BatteryFullAdcVoltage = BatteryPercentageCalculator.DefaultFullAdcVoltage,
    float BatteryEmptyAdcVoltage = BatteryPercentageCalculator.DefaultEmptyAdcVoltage);

public record DeviceLogEntryRequest(
    long TimestampMs,
    string Message,
    string? Level);

public record DeviceConfigurationSyncRequest(
    int AppliedConfigurationVersion,
    int AppliedReportIntervalSeconds,
    int SyncAttempt,
    bool IsConfirmation,
    bool StorageVerified);

public record DeviceConfigurationSyncResponse(
    string DeviceId,
    DeviceConfigurationResponse Configuration,
    bool IsSynchronized,
    int MaximumAttempts,
    DateTime ReceivedAtUtc);

public static class DeviceConfigurationSyncPolicy
{
    public const int MaximumAttempts = 3;
}

public record DevicePositionUpdateRequest(
    double Latitude,
    double Longitude,
    double? AltitudeMeters,
    DateTime? GpsTimeUtc,
    double? SpeedKnots,
    double? Hdop,
    int? SatellitesVisible,
    int? SatellitesUsed);

public record DeviceWifiDiagnosticsUpdateRequest(
    string? LocalIp,
    int? WifiRssiDbm,
    string? Ssid,
    string? Bssid,
    int? Channel,
    string? GatewayIp,
    string? SubnetMask,
    string? DnsIp,
    string? MacAddress);

public record DeviceCellularDiagnosticsUpdateRequest(
    string? LocalIp,
    string? SimStatus,
    bool? NetworkConnected,
    bool? GprsConnected,
    string? Operator,
    int? SignalQuality);

public record DeviceNetworkDiagnosticsUpdateRequest(
    string? Transport,
    DeviceWifiDiagnosticsUpdateRequest? Wifi,
    DeviceCellularDiagnosticsUpdateRequest? Cellular);

public record DeviceUpdateRequest(
    string? FirmwareVersion,
    decimal? Temperature,
    DevicePositionUpdateRequest? Position,
    Battery? Battery,
    DeviceNetworkDiagnosticsUpdateRequest? Network,
    long? DeviceUptimeMs = null,
    IReadOnlyList<DeviceLogEntryRequest>? Logs = null);

public enum BatteryState
{
    Unknown = -1,
    NotCharging = 0,
    Charging = 1,
    Full = 2
}

public record Battery(
    bool ModemReadingValid,
    int ChargeState,
    BatteryState BatteryState,
    int ModemMillivolts,
    float AdcVoltage);

public record DeviceUpdateResponse(
    string DeviceId,
    DateTime ReceivedAtUtc);

public record DeviceLogEntryResponse(
    int Id,
    string? Level,
    string Message,
    DateTime TimestampUtc);

public record DeviceLogsResponse(
    int DeviceId,
    string DeviceIdentifier,
    int Page,
    int PageSize,
    long? TotalCount,
    bool HasMore,
    int? NextBeforeId,
    IReadOnlyList<DeviceLogEntryResponse> Items);

public record DeviceLogsDeleteResponse(
    int Id,
    string DeviceId,
    DateTime? DeletedBeforeUtc,
    int DeletedCount);

public record DeviceTelemetryClearResponse(
    int Id,
    string DeviceId,
    string Category,
    int DeletedCount);

public record DeviceDeleteResponse(
    int Id,
    string DeviceId,
    string Message);
