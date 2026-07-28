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

        if (request.RuntimeConfiguration is not null)
        {
            device.ReportedConfigurationVersion = request.RuntimeConfiguration.AppliedConfigurationVersion;
            device.ReportedReportIntervalSeconds = request.RuntimeConfiguration.AppliedReportIntervalSeconds;
            device.RuntimeConfigurationReportedAtUtc = now;
        }

        long? highestAcknowledgedLogSequenceNumber = null;

        if (request.Logs is { Count: > 0 })
        {
            var validLogs = request.Logs
                .Where(log => log.SequenceNumber >= 0 && !string.IsNullOrWhiteSpace(log.Message))
                .GroupBy(log => log.SequenceNumber)
                .Select(group => group.First())
                .OrderBy(log => log.SequenceNumber)
                .ToList();

            if (validLogs.Count > 0)
            {
                var submittedSequenceNumbers = validLogs
                    .Select(log => log.SequenceNumber)
                    .ToHashSet();

                var existingSequenceNumbers = await dbContext.DeviceLogEntries
                    .Where(entry => entry.DeviceId == device.Id && submittedSequenceNumbers.Contains(entry.SequenceNumber))
                    .Select(entry => entry.SequenceNumber)
                    .ToListAsync();

                var existingSequenceNumberSet = existingSequenceNumbers.ToHashSet();

                var newEntries = validLogs
                    .Where(log => !existingSequenceNumberSet.Contains(log.SequenceNumber))
                    .Select(log => new DeviceLogEntry
                    {
                        DeviceId = device.Id,
                        SequenceNumber = log.SequenceNumber,
                        Level = string.IsNullOrWhiteSpace(log.Level) ? null : log.Level.Trim().ToLowerInvariant(),
                        Message = log.Message.Trim(),
                        DeviceTimestampUtc = log.DeviceTimestampUtc,
                        DeviceUptimeMs = log.DeviceUptimeMs,
                        ReceivedAtUtc = now,
                    })
                    .ToList();

                if (newEntries.Count > 0)
                {
                    dbContext.DeviceLogEntries.AddRange(newEntries);
                }

                highestAcknowledgedLogSequenceNumber = validLogs.Max(log => log.SequenceNumber);
            }
        }

        await dbContext.SaveChangesAsync();

        if (device.PushToHomeAssistant)
        {
            await homeAssistantMqttSyncService.PublishDeviceStateAsync(device.Id);
        }

        return Ok(new DeviceUpdateResponse(
            device.DeviceIdentifier,
            new DeviceConfigurationResponse(device.ReportIntervalSeconds, device.DesiredConfigurationVersion),
            now,
            highestAcknowledgedLogSequenceNumber));
    }
}
