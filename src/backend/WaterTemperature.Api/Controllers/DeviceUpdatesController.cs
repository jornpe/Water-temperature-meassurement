using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Models.Devices;
using WaterTemperature.Api.Services;

namespace WaterTemperature.Api.Controllers;

[ApiController]
[Route("api/devices")]
public class DeviceUpdatesController(
    AppDbContext dbContext,
    IDeviceApiKeyService deviceApiKeyService,
    IHomeAssistantMqttSyncService homeAssistantMqttSyncService) : ApiControllerBase
{
    private static DateTime ResolveLogTimestampUtc(
        long timestampMs,
        long? deviceUptimeMs,
        DateTime receivedAtUtc)
    {
        if (timestampMs < 0
            || timestampMs > uint.MaxValue
            || !deviceUptimeMs.HasValue
            || deviceUptimeMs.Value < 0
            || deviceUptimeMs.Value > uint.MaxValue)
        {
            return receivedAtUtc;
        }

        var loggedAt = (ulong)timestampMs;
        var currentUptime = (ulong)deviceUptimeMs.Value;
        var ageMs = currentUptime >= loggedAt
            ? currentUptime - loggedAt
            : (ulong)uint.MaxValue + 1 + currentUptime - loggedAt;

        return receivedAtUtc.AddMilliseconds(-(double)ageMs);
    }

    [HttpPost("{deviceId}/updates")]
    public async Task<ActionResult<DeviceUpdateResponse>> Update(string deviceId, [FromBody] DeviceUpdateRequest request)
    {
        if (!Request.Headers.TryGetValue(DeviceAuthenticationHeaders.ApiKeyHeaderName, out var apiKeyValues))
        {
            return Unauthorized();
        }

        var device = await dbContext.Devices.SingleOrDefaultAsync(item => item.DeviceIdentifier == deviceId);
        if (device is null || !deviceApiKeyService.Verify(apiKeyValues.ToString(), device.ApiKeyHash))
        {
            return Unauthorized();
        }

        var now = DateTime.UtcNow;

        device.FirmwareVersion = string.IsNullOrWhiteSpace(request.FirmwareVersion)
            ? device.FirmwareVersion
            : request.FirmwareVersion.Trim();
        device.LastSeenAtUtc = now;
        device.LastUpdateReceivedAtUtc = now;
        device.ApiKeyLastUsedAtUtc = now;
        deviceApiKeyService.ClearPendingApiKey(device);

        if (request.Temperature.HasValue)
        {
            device.LatestTemperatureCelsius = request.Temperature.Value;
            device.LatestTemperatureAtUtc = now;

            dbContext.DeviceTemperatureHistory.Add(new DeviceTemperatureHistory
            {
                Device = device,
                TemperatureCelsius = request.Temperature.Value,
                RecordedAtUtc = now,
            });
        }

        if (request.Position is not null)
        {
            device.LatestLatitude = request.Position.Latitude;
            device.LatestLongitude = request.Position.Longitude;
            device.LatestAltitudeMeters = request.Position.AltitudeMeters;
            device.LatestGpsTimeUtc = request.Position.GpsTimeUtc;
            device.LatestSpeedKnots = request.Position.SpeedKnots;
            device.LatestHdop = request.Position.Hdop;
            device.LatestSatellitesVisible = request.Position.SatellitesVisible;
            device.LatestSatellitesUsed = request.Position.SatellitesUsed;
            device.LatestPositionAtUtc = now;

            dbContext.DevicePositionHistory.Add(new DevicePositionHistory
            {
                Device = device,
                Latitude = request.Position.Latitude,
                Longitude = request.Position.Longitude,
                AltitudeMeters = request.Position.AltitudeMeters,
                GpsTimeUtc = request.Position.GpsTimeUtc,
                SpeedKnots = request.Position.SpeedKnots,
                Hdop = request.Position.Hdop,
                SatellitesVisible = request.Position.SatellitesVisible,
                SatellitesUsed = request.Position.SatellitesUsed,
                RecordedAtUtc = now,
            });
        }

        if (request.Battery is not null)
        {
            device.LatestBatteryModemReadingValid = request.Battery.ModemReadingValid;
            device.LatestBatteryChargeState = request.Battery.ChargeState;
            device.LatestBatteryState = request.Battery.BatteryState;
            device.LatestBatteryPercentage = request.Battery.Percentage;
            device.LatestBatteryModemMillivolts = request.Battery.ModemMillivolts;
            device.LatestBatteryAdcVoltage = request.Battery.AdcVoltage;
            device.LatestBatteryAtUtc = now;

            dbContext.DeviceBatteryHistory.Add(new DeviceBatteryHistory
            {
                Device = device,
                ModemReadingValid = request.Battery.ModemReadingValid,
                ChargeState = request.Battery.ChargeState,
                BatteryState = request.Battery.BatteryState,
                Percentage = request.Battery.Percentage,
                ModemMillivolts = request.Battery.ModemMillivolts,
                AdcVoltage = request.Battery.AdcVoltage,
                RecordedAtUtc = now,
            });
        }

        if (request.Network is not null)
        {
            device.LatestNetworkTransport = request.Network.Transport;

            if (request.Network.Wifi is not null)
            {
                device.LatestWifiLocalIp = request.Network.Wifi.LocalIp;
                device.LatestWifiRssiDbm = request.Network.Wifi.WifiRssiDbm;
                device.LatestWifiSsid = request.Network.Wifi.Ssid;
                device.LatestWifiBssid = request.Network.Wifi.Bssid;
                device.LatestWifiChannel = request.Network.Wifi.Channel;
                device.LatestWifiGatewayIp = request.Network.Wifi.GatewayIp;
                device.LatestWifiSubnetMask = request.Network.Wifi.SubnetMask;
                device.LatestWifiDnsIp = request.Network.Wifi.DnsIp;
                device.LatestWifiMacAddress = request.Network.Wifi.MacAddress;
            }

            if (request.Network.Cellular is not null)
            {
                device.LatestCellularLocalIp = request.Network.Cellular.LocalIp;
                device.LatestCellularSimStatus = request.Network.Cellular.SimStatus;
                device.LatestCellularNetworkConnected = request.Network.Cellular.NetworkConnected;
                device.LatestCellularGprsConnected = request.Network.Cellular.GprsConnected;
                device.LatestCellularOperator = request.Network.Cellular.Operator;
                device.LatestCellularSignalQuality = request.Network.Cellular.SignalQuality;
            }
        }

        if (request.Logs is { Count: > 0 })
        {
            var validLogs = request.Logs
                .Where(log => log.TimestampMs >= 0
                    && log.TimestampMs <= uint.MaxValue
                    && !string.IsNullOrWhiteSpace(log.Message))
                .Select(log => new DeviceLogEntry
                {
                    DeviceId = device.Id,
                    Level = string.IsNullOrWhiteSpace(log.Level) ? null : log.Level.Trim().ToLowerInvariant(),
                    Message = log.Message.Trim(),
                    TimestampUtc = ResolveLogTimestampUtc(log.TimestampMs, request.DeviceUptimeMs, now),
                })
                .OrderBy(log => log.TimestampUtc)
                .ToList();

            if (validLogs.Count > 0)
            {
                dbContext.DeviceLogEntries.AddRange(validLogs);
            }
        }

        await dbContext.SaveChangesAsync();

        if (device.PushToHomeAssistant)
        {
            await homeAssistantMqttSyncService.PublishDeviceStateAsync(device.Id);
        }

        return Ok(new DeviceUpdateResponse(device.DeviceIdentifier, now));
    }
}
