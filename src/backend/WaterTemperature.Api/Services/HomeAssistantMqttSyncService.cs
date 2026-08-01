using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Protocol;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Models.Devices;

namespace WaterTemperature.Api.Services;

public interface IHomeAssistantMqttSyncService
{
    ValueTask RefreshAllAsync(CancellationToken cancellationToken = default);
    ValueTask SyncDeviceAsync(int deviceId, CancellationToken cancellationToken = default);
    ValueTask PublishDeviceStateAsync(int deviceId, CancellationToken cancellationToken = default);
    ValueTask RemoveDeviceAsync(string deviceIdentifier, CancellationToken cancellationToken = default);
}

public enum HomeAssistantSyncAction
{
    RefreshAll,
    SyncDevice,
    PublishDeviceState,
    RemoveDevice,
}

public sealed record HomeAssistantSyncRequest(
    HomeAssistantSyncAction Action,
    int? DeviceId = null,
    string? DeviceIdentifier = null);

internal sealed record HomeAssistantBrokerSettings(
    bool Enabled,
    string Host,
    int Port,
    string? Username,
    string? Password);

internal sealed record HomeAssistantPublishMessage(
    string Topic,
    string Payload,
    bool Retain = false,
    MqttQualityOfServiceLevel QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce);

public class HomeAssistantMqttSyncService : BackgroundService, IHomeAssistantMqttSyncService
{
    private const string HomeAssistantStatusTopic = "homeassistant/status";
    private const string BackendAvailabilityTopic = "water-temperature/backend/status";
    private const string DiscoveryPrefix = "homeassistant";
    private const string DeviceTopicPrefix = "water-temperature/devices";

    private readonly Channel<HomeAssistantSyncRequest> _requests = Channel.CreateUnbounded<HomeAssistantSyncRequest>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    
    
    private readonly MqttClientFactory _mqttFactory = new();
    private readonly IMqttClient _mqttClient;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<HomeAssistantMqttSyncService> _logger;
    private HomeAssistantBrokerSettings? _activeBrokerSettings;

    public HomeAssistantMqttSyncService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<HomeAssistantMqttSyncService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _mqttClient = _mqttFactory.CreateMqttClient();
        _mqttClient.ApplicationMessageReceivedAsync += HandleApplicationMessageReceivedAsync;
        _mqttClient.ConnectedAsync += HandleConnectedAsync;
        _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;
    }

    public ValueTask RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        return EnqueueAsync(new HomeAssistantSyncRequest(HomeAssistantSyncAction.RefreshAll), cancellationToken);
    }

    public ValueTask SyncDeviceAsync(int deviceId, CancellationToken cancellationToken = default)
    {
        return EnqueueAsync(new HomeAssistantSyncRequest(HomeAssistantSyncAction.SyncDevice, deviceId), cancellationToken);
    }

    public ValueTask PublishDeviceStateAsync(int deviceId, CancellationToken cancellationToken = default)
    {
        return EnqueueAsync(new HomeAssistantSyncRequest(HomeAssistantSyncAction.PublishDeviceState, deviceId), cancellationToken);
    }

    public ValueTask RemoveDeviceAsync(string deviceIdentifier, CancellationToken cancellationToken = default)
    {
        return EnqueueAsync(new HomeAssistantSyncRequest(HomeAssistantSyncAction.RemoveDevice, null, deviceIdentifier), cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAllAsync(stoppingToken);

        while (await _requests.Reader.WaitToReadAsync(stoppingToken))
        {
            while (_requests.Reader.TryRead(out var request))
            {
                try
                {
                    await ProcessRequestAsync(request, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed processing Home Assistant MQTT request {@Request}", request);
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient.IsConnected)
        {
            try
            {
                await PublishAsync(new HomeAssistantPublishMessage(BackendAvailabilityTopic, "offline", true), cancellationToken);
                await _mqttClient.DisconnectAsync(
                    new MqttClientDisconnectOptionsBuilder()
                        .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                        .Build(),
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disconnecting MQTT client during shutdown");
            }
        }

        await base.StopAsync(cancellationToken);
    }

    private ValueTask EnqueueAsync(HomeAssistantSyncRequest request, CancellationToken cancellationToken)
    {
        return _requests.Writer.WriteAsync(request, cancellationToken);
    }

    private async Task ProcessRequestAsync(HomeAssistantSyncRequest request, CancellationToken cancellationToken)
    {
        switch (request.Action)
        {
            case HomeAssistantSyncAction.RefreshAll:
                await RefreshAllInternalAsync(cancellationToken);
                break;

            case HomeAssistantSyncAction.SyncDevice when request.DeviceId.HasValue:
                await SyncDeviceInternalAsync(request.DeviceId.Value, cancellationToken);
                break;

            case HomeAssistantSyncAction.PublishDeviceState when request.DeviceId.HasValue:
                await PublishDeviceStateInternalAsync(request.DeviceId.Value, cancellationToken);
                break;

            case HomeAssistantSyncAction.RemoveDevice when !string.IsNullOrWhiteSpace(request.DeviceIdentifier):
                await RemoveDeviceInternalAsync(request.DeviceIdentifier!, cancellationToken);
                break;
        }
    }

    private async Task RefreshAllInternalAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await LoadBrokerSettingsAsync(dbContext, includeDisabled: true, cancellationToken);
        if (settings is null)
        {
            return;
        }

        await EnsureConnectedAsync(settings, cancellationToken);

        var devices = await dbContext.Devices
            .AsNoTracking()
            .Where(device => device.PushToHomeAssistant)
            .ToListAsync(cancellationToken);

        if (!settings.Enabled)
        {
            foreach (var device in devices)
            {
                await PublishRemovalAsync(device.DeviceIdentifier, cancellationToken);
            }

            return;
        }

        foreach (var device in devices)
        {
            await PublishDiscoveryAndStateAsync(device, cancellationToken);
        }
    }

    private async Task SyncDeviceInternalAsync(int deviceId, CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await LoadBrokerSettingsAsync(dbContext, includeDisabled: true, cancellationToken);
        if (settings is null)
        {
            return;
        }

        var device = await dbContext.Devices
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == deviceId, cancellationToken);
        if (device is null)
        {
            return;
        }

        await EnsureConnectedAsync(settings, cancellationToken);

        if (!settings.Enabled || !device.PushToHomeAssistant)
        {
            await PublishRemovalAsync(device.DeviceIdentifier, cancellationToken);
            return;
        }

        await PublishDiscoveryAndStateAsync(device, cancellationToken);
    }

    private async Task PublishDeviceStateInternalAsync(int deviceId, CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await LoadBrokerSettingsAsync(dbContext, includeDisabled: false, cancellationToken);
        if (settings is null)
        {
            return;
        }

        var device = await dbContext.Devices
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == deviceId, cancellationToken);
        if (device is null || !device.PushToHomeAssistant)
        {
            return;
        }

        await EnsureConnectedAsync(settings, cancellationToken);

        foreach (var message in BuildStateMessages(device))
        {
            await PublishAsync(message, cancellationToken);
        }
    }

    private async Task RemoveDeviceInternalAsync(string deviceIdentifier, CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await LoadBrokerSettingsAsync(dbContext, includeDisabled: true, cancellationToken);
        if (settings is null)
        {
            return;
        }

        await EnsureConnectedAsync(settings, cancellationToken);
        await PublishRemovalAsync(deviceIdentifier, cancellationToken);
    }

    private async Task<HomeAssistantBrokerSettings?> LoadBrokerSettingsAsync(AppDbContext dbContext, bool includeDisabled, CancellationToken cancellationToken)
    {
        var settings = await dbContext.HomeAssistantIntegrationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == HomeAssistantIntegrationSettings.SingletonId, cancellationToken);
        if (settings is null || string.IsNullOrWhiteSpace(settings.Host))
        {
            return null;
        }

        if (!includeDisabled && !settings.Enabled)
        {
            return null;
        }

        return new HomeAssistantBrokerSettings(
            settings.Enabled,
            settings.Host,
            settings.Port > 0 ? settings.Port : 1883,
            settings.Username,
            settings.Password);
    }

    private async Task EnsureConnectedAsync(HomeAssistantBrokerSettings settings, CancellationToken cancellationToken)
    {
        if (_mqttClient.IsConnected && settings.Equals(_activeBrokerSettings))
        {
            return;
        }

        if (_mqttClient.IsConnected)
        {
            await _mqttClient.DisconnectAsync(
                new MqttClientDisconnectOptionsBuilder()
                    .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                    .Build(),
                cancellationToken: cancellationToken);
        }

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId($"watertemperature-api-{Guid.NewGuid():N}")
            .WithTcpServer(settings.Host, settings.Port)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
            .WithCleanSession()
            .WithWillTopic(BackendAvailabilityTopic)
            .WithWillPayload("offline")
            .WithWillRetain()
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            optionsBuilder = optionsBuilder.WithCredentials(settings.Username, settings.Password);
        }

        await _mqttClient.ConnectAsync(optionsBuilder.Build(), cancellationToken);
        _activeBrokerSettings = settings;
    }

    private async Task PublishDiscoveryAndStateAsync(Device device, CancellationToken cancellationToken)
    {
        await PublishAsync(BuildDiscoveryMessage(device), cancellationToken);

        foreach (var message in BuildStateMessages(device))
        {
            await PublishAsync(message, cancellationToken);
        }
    }

    private async Task PublishRemovalAsync(string deviceIdentifier, CancellationToken cancellationToken)
    {
        await PublishAsync(new HomeAssistantPublishMessage(GetDiscoveryTopic(deviceIdentifier), string.Empty, true), cancellationToken);
    }

    private async Task PublishAsync(HomeAssistantPublishMessage message, CancellationToken cancellationToken)
    {
        var mqttMessage = new MqttApplicationMessageBuilder()
            .WithTopic(message.Topic)
            .WithPayload(message.Payload)
            .WithRetainFlag(message.Retain)
            .WithQualityOfServiceLevel(message.QualityOfServiceLevel)
            .Build();

        await _mqttClient.PublishAsync(mqttMessage, cancellationToken);
    }

    private Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        if (!string.Equals(eventArgs.ApplicationMessage.Topic, HomeAssistantStatusTopic, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var payloadBytes = eventArgs.ApplicationMessage.Payload;
        var payload = payloadBytes.IsEmpty
            ? string.Empty
            : System.Text.Encoding.UTF8.GetString(payloadBytes.ToArray());

        if (string.Equals(payload, "online", StringComparison.OrdinalIgnoreCase))
        {
            return RefreshAllAsync().AsTask();
        }

        return Task.CompletedTask;
    }

    private async Task HandleConnectedAsync(MqttClientConnectedEventArgs _)
    {
        var subscribeOptions = _mqttFactory
            .CreateSubscribeOptionsBuilder()
            .WithTopicFilter(HomeAssistantStatusTopic)
            .Build();
        
        await _mqttClient.SubscribeAsync(subscribeOptions);
        await PublishAsync(new HomeAssistantPublishMessage(BackendAvailabilityTopic, "online", true), CancellationToken.None);
    }

    private Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs eventArgs)
    {
        if (eventArgs.Exception is not null)
        {
            _logger.LogWarning(eventArgs.Exception, "Disconnected from Home Assistant MQTT broker");
        }

        return Task.CompletedTask;
    }

    internal HomeAssistantPublishMessage BuildDiscoveryMessage(Device device)
    {
        var uniquePrefix = GetUniquePrefix(device.DeviceIdentifier);
        var components = new Dictionary<string, object?>
        {
            ["temperature"] = BuildSensorComponent(
                uniquePrefix,
                "temperature",
                null,
                GetStateTopic(device.DeviceIdentifier, "temperature"),
                deviceClass: "temperature",
                unitOfMeasurement: "°C",
                stateClass: "measurement",
                suggestedDisplayPrecision: 2),
            
            ["location"] = BuildDeviceTrackerComponent(uniquePrefix, GetLocationAttributesTopic(device.DeviceIdentifier)),
            ["altitude"] = BuildSensorComponent(uniquePrefix, "altitude", "Altitude", GetStateTopic(device.DeviceIdentifier, "altitude"), unitOfMeasurement: "m"),
            ["speed"] = BuildSensorComponent(uniquePrefix, "speed", "Speed", GetStateTopic(device.DeviceIdentifier, "speed"), unitOfMeasurement: "kn"),
            ["hdop"] = BuildSensorComponent(uniquePrefix, "hdop", "HDOP", GetStateTopic(device.DeviceIdentifier, "hdop")),
            ["satellites_visible"] = BuildSensorComponent(uniquePrefix, "satellites_visible", "Satellites visible", GetStateTopic(device.DeviceIdentifier, "satellites-visible")),
            ["satellites_used"] = BuildSensorComponent(uniquePrefix, "satellites_used", "Satellites used", GetStateTopic(device.DeviceIdentifier, "satellites-used")),
            
            ["network_transport"] = BuildSensorComponent(uniquePrefix, "network_transport", "Network transport", GetStateTopic(device.DeviceIdentifier, "network-transport"), entityCategory: "diagnostic", enabledByDefault: true),
            ["firmware_version"] = BuildSensorComponent(uniquePrefix, "firmware_version", "Firmware version", GetStateTopic(device.DeviceIdentifier, "firmware-version"), entityCategory: "diagnostic", enabledByDefault: true),
            ["last_seen"] = BuildSensorComponent(uniquePrefix, "last_seen", "Last seen", GetStateTopic(device.DeviceIdentifier, "last-seen"), deviceClass: "timestamp", entityCategory: "diagnostic", enabledByDefault: true),
            ["last_update_received"] = BuildSensorComponent(uniquePrefix, "last_update_received", "Last update received", GetStateTopic(device.DeviceIdentifier, "last-update-received"), deviceClass: "timestamp", entityCategory: "diagnostic", enabledByDefault: true),
            ["last_discovered"] = BuildSensorComponent(uniquePrefix, "last_discovered", "Last discovered", GetStateTopic(device.DeviceIdentifier, "last-discovered"), deviceClass: "timestamp", entityCategory: "diagnostic", enabledByDefault: true),
            ["report_interval_seconds"] = BuildSensorComponent(uniquePrefix, "report_interval_seconds", "Report interval", GetStateTopic(device.DeviceIdentifier, "report-interval-seconds"), unitOfMeasurement: "s", entityCategory: "diagnostic", enabledByDefault: true),
            ["desired_configuration_version"] = BuildSensorComponent(uniquePrefix, "desired_configuration_version", "Desired configuration version", GetStateTopic(device.DeviceIdentifier, "desired-configuration-version"), entityCategory: "diagnostic", enabledByDefault: true),
            ["reported_configuration_version"] = BuildSensorComponent(uniquePrefix, "reported_configuration_version", "Reported configuration version", GetStateTopic(device.DeviceIdentifier, "reported-configuration-version"), entityCategory: "diagnostic", enabledByDefault: true),
            ["reported_report_interval_seconds"] = BuildSensorComponent(uniquePrefix, "reported_report_interval_seconds", "Reported report interval", GetStateTopic(device.DeviceIdentifier, "reported-report-interval-seconds"), unitOfMeasurement: "s", entityCategory: "diagnostic", enabledByDefault: true),
            
            ["wifi_local_ip"] = BuildSensorComponent(uniquePrefix, "wifi_local_ip", "Wi-Fi local IP", GetStateTopic(device.DeviceIdentifier, "wifi-local-ip"), entityCategory: "diagnostic", enabledByDefault: true),
            ["wifi_rssi"] = BuildSensorComponent(uniquePrefix, "wifi_rssi", "Wi-Fi RSSI", GetStateTopic(device.DeviceIdentifier, "wifi-rssi"), deviceClass: "signal_strength", unitOfMeasurement: "dBm", entityCategory: "diagnostic", enabledByDefault: true),
            ["wifi_ssid"] = BuildSensorComponent(uniquePrefix, "wifi_ssid", "Wi-Fi SSID", GetStateTopic(device.DeviceIdentifier, "wifi-ssid"), entityCategory: "diagnostic", enabledByDefault: true),
            ["wifi_bssid"] = BuildSensorComponent(uniquePrefix, "wifi_bssid", "Wi-Fi BSSID", GetStateTopic(device.DeviceIdentifier, "wifi-bssid"), entityCategory: "diagnostic", enabledByDefault: true),
            ["wifi_channel"] = BuildSensorComponent(uniquePrefix, "wifi_channel", "Wi-Fi channel", GetStateTopic(device.DeviceIdentifier, "wifi-channel"), entityCategory: "diagnostic", enabledByDefault: true),
            ["wifi_gateway_ip"] = BuildSensorComponent(uniquePrefix, "wifi_gateway_ip", "Wi-Fi gateway IP", GetStateTopic(device.DeviceIdentifier, "wifi-gateway-ip"), entityCategory: "diagnostic", enabledByDefault: true),
            ["wifi_subnet_mask"] = BuildSensorComponent(uniquePrefix, "wifi_subnet_mask", "Wi-Fi subnet mask", GetStateTopic(device.DeviceIdentifier, "wifi-subnet-mask"), entityCategory: "diagnostic", enabledByDefault: true),
            ["wifi_dns_ip"] = BuildSensorComponent(uniquePrefix, "wifi_dns_ip", "Wi-Fi DNS IP", GetStateTopic(device.DeviceIdentifier, "wifi-dns-ip"), entityCategory: "diagnostic", enabledByDefault: true),
            ["wifi_mac_address"] = BuildSensorComponent(uniquePrefix, "wifi_mac_address", "Wi-Fi MAC address", GetStateTopic(device.DeviceIdentifier, "wifi-mac-address"), entityCategory: "diagnostic", enabledByDefault: true),
            
            ["cellular_local_ip"] = BuildSensorComponent(uniquePrefix, "cellular_local_ip", "Cellular local IP", GetStateTopic(device.DeviceIdentifier, "cellular-local-ip"), entityCategory: "diagnostic", enabledByDefault: true),
            ["cellular_sim_status"] = BuildSensorComponent(uniquePrefix, "cellular_sim_status", "Cellular SIM status", GetStateTopic(device.DeviceIdentifier, "cellular-sim-status"), entityCategory: "diagnostic", enabledByDefault: true),
            ["cellular_network_connected"] = BuildBinarySensorComponent(uniquePrefix, "cellular_network_connected", "Cellular network connected", GetStateTopic(device.DeviceIdentifier, "cellular-network-connected"), entityCategory: "diagnostic", enabledByDefault: true),
            ["cellular_gprs_connected"] = BuildBinarySensorComponent(uniquePrefix, "cellular_gprs_connected", "Cellular GPRS connected", GetStateTopic(device.DeviceIdentifier, "cellular-gprs-connected"), entityCategory: "diagnostic", enabledByDefault: true),
            ["cellular_operator"] = BuildSensorComponent(uniquePrefix, "cellular_operator", "Cellular operator", GetStateTopic(device.DeviceIdentifier, "cellular-operator"), entityCategory: "diagnostic", enabledByDefault: true),
            ["cellular_signal_quality"] = BuildSensorComponent(uniquePrefix, "cellular_signal_quality", "Cellular signal quality", GetStateTopic(device.DeviceIdentifier, "cellular-signal-quality"), entityCategory: "diagnostic", enabledByDefault: true),
            
            ["battery_state"] = BuildSensorComponent(uniquePrefix, "battery_state", "Battery State", GetStateTopic(device.DeviceIdentifier, "battery-state"), entityCategory: "diagnostic", enabledByDefault: true, deviceClass: "enum", options: Enum.GetNames<BatteryState>()),
            ["battery_percentage"] = BuildSensorComponent(uniquePrefix, "battery_percentage", "Battery Percentage", GetStateTopic(device.DeviceIdentifier, "battery-percentage"), enabledByDefault: true, deviceClass: "battery", unitOfMeasurement: "%", stateClass: "measurement"),
            ["battery_adc_voltage"] = BuildSensorComponent(uniquePrefix, "battery_adc_voltage", "Battery ADC Voltage", GetStateTopic(device.DeviceIdentifier, "battery-adc-voltage"), entityCategory: "diagnostic", enabledByDefault: true, deviceClass: "voltage", unitOfMeasurement: "V", stateClass: "measurement"),
            ["battery_modem_millivoltage"] = BuildSensorComponent(uniquePrefix, "battery_modem_millivoltage", "Battery Modem Millivoltage", GetStateTopic(device.DeviceIdentifier, "battery-modem-millivoltage"), entityCategory: "diagnostic", enabledByDefault: true, deviceClass: "voltage", unitOfMeasurement: "mV", stateClass: "measurement"),
            ["battery_modem_read_valid"] = BuildBinarySensorComponent(uniquePrefix, "battery_modem_read_valid", "Battery Modem Read Valid", GetStateTopic(device.DeviceIdentifier, "battery-modem-read-valid"), entityCategory: "diagnostic", enabledByDefault: true),
            ["battery_recorded_time"] = BuildSensorComponent(uniquePrefix, "battery_recorded_time", "Battery Recorded Time", GetStateTopic(device.DeviceIdentifier, "battery-recorded-time"), entityCategory: "diagnostic", enabledByDefault: true, deviceClass: "timestamp"),
        };

        var payload = new Dictionary<string, object?>
        {
            ["device"] = new Dictionary<string, object?>
            {
                ["identifiers"] = new[] { $"water-temperature:{device.DeviceIdentifier}" },
                ["name"] = device.HomeAssistantDeviceName ?? device.Name ?? device.DeviceIdentifier,
                ["manufacturer"] = "Water Temperature Measurement",
                ["model"] = "Water Temperature Sensor",
                ["serial_number"] = device.DeviceIdentifier,
                ["sw_version"] = device.FirmwareVersion,
                ["suggested_area"] = device.Place,
            },
            ["o"] = new Dictionary<string, object?>
            {
                ["name"] = "WaterTemperature.Api",
                ["sw_version"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                ["support_url"] = "https://github.com/jornpe/Water-temperature-meassurement",
            },
            ["availability"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["topic"] = BackendAvailabilityTopic,
                    ["payload_available"] = "online",
                    ["payload_not_available"] = "offline",
                }
            },
            ["cmps"] = components,
        };

        return new HomeAssistantPublishMessage(
            GetDiscoveryTopic(device.DeviceIdentifier),
            JsonSerializer.Serialize(StripNulls(payload), _jsonOptions),
            true);
    }

    /// <summary>
    /// Recursively removes entries with a <see langword="null"/> value from dictionaries (and array elements).
    /// This is required because <see cref="JsonIgnoreCondition.WhenWritingNull"/> only suppresses null values for
    /// serialized POCO properties, not for <see cref="Dictionary{TKey, TValue}"/> values. Without this, optional
    /// discovery fields such as "suggested_display_precision" get sent as an explicit JSON null, which Home
    /// Assistant's MQTT discovery schema rejects (it expects the field to either be a valid value or be absent),
    /// causing the whole component to be dropped.
    /// </summary>
    private static object? StripNulls(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case Dictionary<string, object?> dictionary:
            {
                var result = new Dictionary<string, object?>();
                foreach (var (key, entryValue) in dictionary)
                {
                    if (entryValue is null)
                    {
                        continue;
                    }

                    result[key] = StripNulls(entryValue);
                }

                return result;
            }
            case object[] array:
                return array.Select(StripNulls).ToArray();
            default:
                return value;
        }
    }

    internal IReadOnlyList<HomeAssistantPublishMessage> BuildStateMessages(Device device)
    {
        var messages = new List<HomeAssistantPublishMessage>
        {
            new(BackendAvailabilityTopic, "online", true),
        };

        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "temperature"), FormatNumber(device.LatestTemperatureCelsius));
        
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "altitude"), FormatNumber(device.LatestAltitudeMeters));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "speed"), FormatNumber(device.LatestSpeedKnots));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "hdop"), FormatNumber(device.LatestHdop));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "satellites-visible"), FormatNumber(device.LatestSatellitesVisible));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "satellites-used"), FormatNumber(device.LatestSatellitesUsed));
        
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "network-transport"), device.LatestNetworkTransport);
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "firmware-version"), device.FirmwareVersion);
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "last-seen"), FormatTimestamp(device.LastSeenAtUtc));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "last-update-received"), FormatTimestamp(device.LastUpdateReceivedAtUtc));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "last-discovered"), FormatTimestamp(device.LastDiscoveredAtUtc));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "report-interval-seconds"), FormatNumber<int>(device.ReportIntervalSeconds));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "desired-configuration-version"), FormatNumber<int>(device.DesiredConfigurationVersion));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "reported-configuration-version"), FormatNumber<int>(device.ReportedConfigurationVersion));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "reported-report-interval-seconds"), FormatNumber<int>(device.ReportedReportIntervalSeconds));
        
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "wifi-local-ip"), device.LatestWifiLocalIp);
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "wifi-rssi"), FormatNumber(device.LatestWifiRssiDbm));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "wifi-ssid"), device.LatestWifiSsid);
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "wifi-bssid"), device.LatestWifiBssid);
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "wifi-channel"), FormatNumber(device.LatestWifiChannel));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "wifi-gateway-ip"), device.LatestWifiGatewayIp);
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "wifi-subnet-mask"), device.LatestWifiSubnetMask);
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "wifi-dns-ip"), device.LatestWifiDnsIp);
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "wifi-mac-address"), device.LatestWifiMacAddress);
        
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "cellular-local-ip"), device.LatestCellularLocalIp);
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "cellular-sim-status"), device.LatestCellularSimStatus);
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "cellular-operator"), device.LatestCellularOperator);
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "cellular-signal-quality"), FormatNumber(device.LatestCellularSignalQuality));
        
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "battery-state"), device.LatestBatteryState.ToString());
        AddIfPresent(
            messages,
            GetStateTopic(device.DeviceIdentifier, "battery-percentage"),
            FormatNumber(BatteryPercentageCalculator.Calculate(
                device.LatestBatteryAdcVoltage,
                device.BatteryFullAdcVoltage,
                device.BatteryEmptyAdcVoltage)));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "battery-adc-voltage"), FormatNumber(device.LatestBatteryAdcVoltage));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "battery-modem-millivoltage"), FormatNumber(device.LatestBatteryModemMillivolts));
        AddIfPresent(messages, GetStateTopic(device.DeviceIdentifier, "battery-recorded-time"), FormatTimestamp(device.LatestBatteryAtUtc));

        if (device.LatestBatteryModemReadingValid.HasValue)
        {
            messages.Add(new HomeAssistantPublishMessage(
                GetStateTopic(device.DeviceIdentifier, "battery-modem-read-valid"),
                device.LatestBatteryModemReadingValid.Value ? "ON" : "OFF"));
        }

        if (device.LatestCellularNetworkConnected.HasValue)
        {
            messages.Add(new HomeAssistantPublishMessage(
                GetStateTopic(device.DeviceIdentifier, "cellular-network-connected"),
                device.LatestCellularNetworkConnected.Value ? "ON" : "OFF"));
        }

        if (device.LatestCellularGprsConnected.HasValue)
        {
            messages.Add(new HomeAssistantPublishMessage(
                GetStateTopic(device.DeviceIdentifier, "cellular-gprs-connected"),
                device.LatestCellularGprsConnected.Value ? "ON" : "OFF"));
        }

        if (device.LatestLatitude.HasValue && device.LatestLongitude.HasValue)
        {
            var locationPayload = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["latitude"] = device.LatestLatitude.Value,
                ["longitude"] = device.LatestLongitude.Value,
                ["gps_accuracy"] = device.LatestHdop,
            }, _jsonOptions);

            messages.Add(new HomeAssistantPublishMessage(GetLocationAttributesTopic(device.DeviceIdentifier), locationPayload));
        }

        return messages;
    }

    private static Dictionary<string, object?> BuildSensorComponent(
        string uniquePrefix,
        string uniqueSuffix,
        string? name,
        string stateTopic,
        string? deviceClass = null,
        string? unitOfMeasurement = null,
        string? stateClass = null,
        string? entityCategory = null,
        bool? enabledByDefault = null,
        int? suggestedDisplayPrecision = null, 
        IEnumerable<string>? options = null)
    {
        return new Dictionary<string, object?>
        {
            ["p"] = "sensor",
            ["unique_id"] = $"{uniquePrefix}_{uniqueSuffix}",
            ["name"] = name,
            ["state_topic"] = stateTopic,
            ["device_class"] = deviceClass,
            ["unit_of_measurement"] = unitOfMeasurement,
            ["state_class"] = stateClass,
            ["entity_category"] = entityCategory,
            ["enabled_by_default"] = enabledByDefault,
            ["suggested_display_precision"] = suggestedDisplayPrecision,
            ["options"] = options
        };
    }

    private static Dictionary<string, object?> BuildBinarySensorComponent(
        string uniquePrefix,
        string uniqueSuffix,
        string name,
        string stateTopic,
        string? deviceClass = null,
        string? entityCategory = null,
        bool? enabledByDefault = null)
    {
        return new Dictionary<string, object?>
        {
            ["p"] = "binary_sensor",
            ["unique_id"] = $"{uniquePrefix}_{uniqueSuffix}",
            ["name"] = name,
            ["state_topic"] = stateTopic,
            ["payload_on"] = "ON",
            ["payload_off"] = "OFF",
            ["device_class"] = deviceClass,
            ["entity_category"] = entityCategory,
            ["enabled_by_default"] = enabledByDefault,
        };
    }

    private static Dictionary<string, object?> BuildDeviceTrackerComponent(string uniquePrefix, string attributesTopic)
    {
        return new Dictionary<string, object?>
        {
            ["p"] = "device_tracker",
            ["unique_id"] = $"{uniquePrefix}_location",
            ["name"] = "Location",
            ["json_attributes_topic"] = attributesTopic,
            ["source_type"] = "gps",
            ["enabled_by_default"] = true,
        };
    }

    private static void AddIfPresent(ICollection<HomeAssistantPublishMessage> messages, string topic, string? payload)
    {
        if (!string.IsNullOrWhiteSpace(payload))
        {
            messages.Add(new HomeAssistantPublishMessage(topic, payload));
        }
    }

    private static string GetDiscoveryTopic(string deviceIdentifier)
    {
        return $"{DiscoveryPrefix}/device/{GetTopicSafeDeviceId(deviceIdentifier)}/config";
    }

    private static string GetStateTopic(string deviceIdentifier, string metric)
    {
        return $"{DeviceTopicPrefix}/{GetTopicSafeDeviceId(deviceIdentifier)}/{metric}/state";
    }

    private static string GetLocationAttributesTopic(string deviceIdentifier)
    {
        return $"{DeviceTopicPrefix}/{GetTopicSafeDeviceId(deviceIdentifier)}/location/attributes";
    }

    private static string GetUniquePrefix(string deviceIdentifier)
    {
        return $"watertemperature_{GetTopicSafeDeviceId(deviceIdentifier)}";
    }

    private static string GetTopicSafeDeviceId(string deviceIdentifier)
    {
        var chars = deviceIdentifier.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_').ToArray();
        return new string(chars);
    }

    private static string? FormatTimestamp(DateTime? value)
    {
        return value?.ToString("O", CultureInfo.InvariantCulture);
    }

    private static string? FormatNumber<T>(T? value) where T : struct
    {
        return value switch
        {
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => null,
        };
    }
}
