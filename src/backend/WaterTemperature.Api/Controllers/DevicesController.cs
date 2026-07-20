using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Models.Auth;
using WaterTemperature.Api.Models.Devices;
using WaterTemperature.Api.Services;

namespace WaterTemperature.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DevicesController(
    AppDbContext dbContext,
    IDeviceApiKeyService deviceApiKeyService,
    IHomeAssistantMqttSyncService homeAssistantMqttSyncService) : ApiControllerBase
{
    private const int DefaultLogsPageSize = 50;
    private const int MaxLogsPageSize = 200;
    private static readonly Regex HomeAssistantDeviceNameRegex = new(
        "^[\\p{L}\\p{N} _\\-().]+$",
        RegexOptions.Compiled);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeviceSummaryResponse>>> GetDevices()
    {
        var devices = await dbContext.Devices
            .AsNoTracking()
            .OrderBy(device => device.Status == DeviceRegistrationStatus.Registered)
            .ThenBy(device => device.Place ?? string.Empty)
            .ThenBy(device => device.Name ?? device.DeviceIdentifier)
            .Select(device => new DeviceSummaryResponse(
                device.Id,
                device.DeviceIdentifier,
                device.Status == DeviceRegistrationStatus.Registered ? "registered" : "unregistered",
                device.Name,
                device.Place,
                device.PushToHomeAssistant,
                device.LatestTemperatureCelsius,
                device.LastUpdateReceivedAtUtc,
                device.LastDiscoveredAtUtc))
            .ToListAsync();

        return Ok(devices);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DeviceDetailResponse>> GetDevice(int id)
    {
        var device = await dbContext.Devices
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new DeviceDetailResponse(
                item.Id,
                item.DeviceIdentifier,
                item.Status == DeviceRegistrationStatus.Registered ? "registered" : "unregistered",
                item.Name,
                item.Place,
                item.PushToHomeAssistant,
                item.HomeAssistantDeviceName ?? item.Name ?? item.DeviceIdentifier,
                item.FirmwareVersion,
                item.ReportIntervalSeconds,
                item.CreatedAtUtc,
                item.RegisteredAtUtc,
                item.LastDiscoveredAtUtc,
                item.LastSeenAtUtc,
                item.LastUpdateReceivedAtUtc,
                item.LatestTemperatureCelsius,
                item.LatestTemperatureAtUtc,
                new DeviceDesiredConfigurationResponse(
                    item.DesiredConfigurationVersion,
                    item.ReportIntervalSeconds,
                    item.DesiredConfigurationUpdatedAtUtc),
                new DeviceRuntimeConfigurationResponse(
                    item.ReportedConfigurationVersion,
                    item.ReportedReportIntervalSeconds,
                    item.RuntimeConfigurationReportedAtUtc),
                item.Status == DeviceRegistrationStatus.Registered
                    && item.DesiredConfigurationVersion > (item.ReportedConfigurationVersion ?? 0),
                new DevicePositionSnapshotResponse(
                    item.LatestLatitude,
                    item.LatestLongitude,
                    item.LatestAltitudeMeters,
                    item.LatestGpsTimeUtc,
                    item.LatestSpeedKnots,
                    item.LatestHdop,
                    item.LatestSatellitesVisible,
                    item.LatestSatellitesUsed,
                    item.LatestPositionAtUtc),
                new DeviceNetworkDiagnosticsResponse(
                    item.LatestNetworkTransport,
                    new DeviceWifiDiagnosticsResponse(
                        item.LatestWifiLocalIp,
                        item.LatestWifiRssiDbm,
                        item.LatestWifiSsid,
                        item.LatestWifiBssid,
                        item.LatestWifiChannel,
                        item.LatestWifiGatewayIp,
                        item.LatestWifiSubnetMask,
                        item.LatestWifiDnsIp,
                        item.LatestWifiMacAddress),
                    new DeviceCellularDiagnosticsResponse(
                        item.LatestCellularLocalIp,
                        item.LatestCellularSimStatus,
                        item.LatestCellularNetworkConnected,
                        item.LatestCellularGprsConnected,
                        item.LatestCellularOperator,
                        item.LatestCellularSignalQuality)),
                item.TemperatureHistory.Count,
                item.PositionHistory.Count))
            .SingleOrDefaultAsync();

        return device is null ? NotFound() : Ok(device);
    }

    [HttpGet("{id:int}/logs")]
    public async Task<ActionResult<DeviceLogsResponse>> GetDeviceLogs(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultLogsPageSize)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxLogsPageSize);

        var device = await dbContext.Devices
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, item.DeviceIdentifier })
            .SingleOrDefaultAsync();

        if (device is null)
        {
            return NotFound();
        }

        var logsQuery = dbContext.DeviceLogEntries
            .AsNoTracking()
            .Where(entry => entry.DeviceId == id);

        var totalCount = await logsQuery.CountAsync();
        var items = await logsQuery
            .OrderByDescending(entry => entry.SequenceNumber)
            .ThenByDescending(entry => entry.ReceivedAtUtc)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(entry => new DeviceLogEntryResponse(
                entry.Id,
                entry.SequenceNumber,
                entry.Level,
                entry.Message,
                entry.DeviceTimestampUtc,
                entry.DeviceUptimeMs,
                entry.ReceivedAtUtc))
            .ToListAsync();

        return Ok(new DeviceLogsResponse(
            device.Id,
            device.DeviceIdentifier,
            normalizedPage,
            normalizedPageSize,
            totalCount,
            items));
    }

    [HttpPost("{id:int}/register")]
    public async Task<ActionResult<DeviceRegistrationResponse>> RegisterDevice(int id, [FromBody] DeviceRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Place))
        {
            return BadRequest(new MessageResponse("Name and place are required"));
        }

        if (request.ReportIntervalSeconds <= 0)
        {
            return BadRequest(new MessageResponse("Report interval must be greater than zero"));
        }

        var device = await dbContext.Devices.FindAsync(id);
        if (device is null)
        {
            return NotFound();
        }

        var credential = deviceApiKeyService.CreateCredential();
        var normalizedName = request.Name.Trim();
        var normalizedPlace = request.Place.Trim();
        var homeAssistantDeviceName = NormalizeHomeAssistantDeviceName(request.HomeAssistantDeviceName, normalizedName);
        var effectiveHomeAssistantDeviceName = ResolveHomeAssistantDeviceName(normalizedName, homeAssistantDeviceName, request.Name.Trim());

        if (!TryValidateHomeAssistantDeviceName(effectiveHomeAssistantDeviceName, out var validationMessage))
        {
            return BadRequest(new MessageResponse(validationMessage!));
        }

        device.Name = normalizedName;
        device.Place = normalizedPlace;
        device.PushToHomeAssistant = request.PushToHomeAssistant;
        device.HomeAssistantDeviceName = homeAssistantDeviceName;
        device.ReportIntervalSeconds = request.ReportIntervalSeconds;
        device.DesiredConfigurationVersion = 1;
        device.DesiredConfigurationUpdatedAtUtc = credential.CreatedAtUtc;
        device.Status = DeviceRegistrationStatus.Registered;
        device.RegisteredAtUtc ??= credential.CreatedAtUtc;
        device.ApiKeyHash = credential.ApiKeyHash;
        device.PendingApiKeyProtected = credential.ProtectedApiKey;
        device.ApiKeyCreatedAtUtc = credential.CreatedAtUtc;
        device.ApiKeyIssuedAtUtc = credential.CreatedAtUtc;

        await dbContext.SaveChangesAsync();

        if (device.PushToHomeAssistant)
        {
            await homeAssistantMqttSyncService.SyncDeviceAsync(device.Id);
        }

        return Ok(new DeviceRegistrationResponse(
            device.Id,
            device.DeviceIdentifier,
            "registered",
            new DeviceConfigurationResponse(device.ReportIntervalSeconds, device.DesiredConfigurationVersion),
            credential.CreatedAtUtc));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<DeviceDetailResponse>> UpdateDevice(int id, [FromBody] RegisteredDeviceUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Place))
        {
            return BadRequest(new MessageResponse("Name and place are required"));
        }

        if (request.ReportIntervalSeconds <= 0)
        {
            return BadRequest(new MessageResponse("Report interval must be greater than zero"));
        }

        var device = await dbContext.Devices.FindAsync(id);
        if (device is null)
        {
            return NotFound();
        }

        if (device.Status != DeviceRegistrationStatus.Registered)
        {
            return BadRequest(new MessageResponse("Device must be registered before updating its configuration"));
        }

        var now = DateTime.UtcNow;
        var normalizedName = request.Name.Trim();
        var normalizedPlace = request.Place.Trim();
        var homeAssistantDeviceName = NormalizeHomeAssistantDeviceName(request.HomeAssistantDeviceName, normalizedName);
        var effectiveHomeAssistantDeviceName = ResolveHomeAssistantDeviceName(normalizedName, homeAssistantDeviceName, device.DeviceIdentifier);
        var reportIntervalChanged = device.ReportIntervalSeconds != request.ReportIntervalSeconds;

        if (!TryValidateHomeAssistantDeviceName(effectiveHomeAssistantDeviceName, out var validationMessage))
        {
            return BadRequest(new MessageResponse(validationMessage!));
        }

        device.Name = normalizedName;
        device.Place = normalizedPlace;
        device.PushToHomeAssistant = request.PushToHomeAssistant;
        device.HomeAssistantDeviceName = homeAssistantDeviceName;

        if (reportIntervalChanged)
        {
            device.ReportIntervalSeconds = request.ReportIntervalSeconds;
            device.DesiredConfigurationVersion += 1;
            device.DesiredConfigurationUpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync();

        if (device.PushToHomeAssistant)
        {
            await homeAssistantMqttSyncService.SyncDeviceAsync(device.Id);
        }
        else
        {
            await homeAssistantMqttSyncService.RemoveDeviceAsync(device.DeviceIdentifier);
        }

        return await GetDevice(id);
    }

    [HttpPost("{id:int}/regenerate-key")]
    public async Task<ActionResult<DeviceApiKeyRegenerationResponse>> RegenerateApiKey(int id)
    {
        var device = await dbContext.Devices.FindAsync(id);
        if (device is null)
        {
            return NotFound();
        }

        if (device.Status != DeviceRegistrationStatus.Registered)
        {
            return BadRequest(new MessageResponse("Device must be registered before regenerating its key"));
        }

        var credential = deviceApiKeyService.CreateCredential();

        device.ApiKeyHash = credential.ApiKeyHash;
        device.PendingApiKeyProtected = credential.ProtectedApiKey;
        device.ApiKeyCreatedAtUtc = credential.CreatedAtUtc;
        device.ApiKeyIssuedAtUtc = credential.CreatedAtUtc;
        device.ApiKeyLastUsedAtUtc = null;

        await dbContext.SaveChangesAsync();

        return Ok(new DeviceApiKeyRegenerationResponse(
            device.Id,
            device.DeviceIdentifier,
            credential.CreatedAtUtc,
            new DeviceConfigurationResponse(device.ReportIntervalSeconds, device.DesiredConfigurationVersion)));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<DeviceDeleteResponse>> DeleteDevice(int id)
    {
        var device = await dbContext.Devices.FindAsync(id);
        if (device is null)
        {
            return NotFound();
        }

        var deviceIdentifier = device.DeviceIdentifier;

        dbContext.Devices.Remove(device);
        await dbContext.SaveChangesAsync();

        await homeAssistantMqttSyncService.RemoveDeviceAsync(deviceIdentifier);

        return Ok(new DeviceDeleteResponse(id, deviceIdentifier, "Device deleted"));
    }

    [HttpDelete("{id:int}/telemetry/{category}")]
    public async Task<ActionResult<DeviceTelemetryClearResponse>> ClearTelemetry(int id, string category)
    {
        var device = await dbContext.Devices
            .Include(item => item.TemperatureHistory)
            .Include(item => item.PositionHistory)
            .SingleOrDefaultAsync(item => item.Id == id);
        if (device is null)
        {
            return NotFound();
        }

        var normalizedCategory = category.Trim().ToLowerInvariant();
        var deletedCount = 0;

        switch (normalizedCategory)
        {
            case "temperature":
                deletedCount = device.TemperatureHistory.Count;
                dbContext.DeviceTemperatureHistory.RemoveRange(device.TemperatureHistory);
                device.LatestTemperatureCelsius = null;
                device.LatestTemperatureAtUtc = null;
                break;

            case "position":
                deletedCount = device.PositionHistory.Count;
                dbContext.DevicePositionHistory.RemoveRange(device.PositionHistory);
                device.LatestLatitude = null;
                device.LatestLongitude = null;
                device.LatestAltitudeMeters = null;
                device.LatestGpsTimeUtc = null;
                device.LatestSpeedKnots = null;
                device.LatestHdop = null;
                device.LatestSatellitesVisible = null;
                device.LatestSatellitesUsed = null;
                device.LatestPositionAtUtc = null;
                break;

            default:
                return BadRequest(new MessageResponse("Telemetry category must be 'temperature' or 'position'"));
        }

        await dbContext.SaveChangesAsync();

        if (device.PushToHomeAssistant)
        {
            await homeAssistantMqttSyncService.SyncDeviceAsync(device.Id);
        }

        return Ok(new DeviceTelemetryClearResponse(device.Id, device.DeviceIdentifier, normalizedCategory, deletedCount));
    }

    private static string ResolveHomeAssistantDeviceName(string deviceName, string? homeAssistantDeviceName, string deviceIdentifier)
    {
        return homeAssistantDeviceName ?? deviceName ?? deviceIdentifier;
    }

    private static string? NormalizeHomeAssistantDeviceName(string? homeAssistantDeviceName, string deviceName)
    {
        if (string.IsNullOrWhiteSpace(homeAssistantDeviceName))
        {
            return null;
        }

        var normalizedName = homeAssistantDeviceName.Trim();
        return string.Equals(normalizedName, deviceName, StringComparison.Ordinal) ? null : normalizedName;
    }

    private static bool TryValidateHomeAssistantDeviceName(string deviceName, out string? validationMessage)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            validationMessage = "Home Assistant device name is required";
            return false;
        }

        if (deviceName.Length > 100)
        {
            validationMessage = "Home Assistant device name must be 100 characters or fewer";
            return false;
        }

        if (!HomeAssistantDeviceNameRegex.IsMatch(deviceName))
        {
            validationMessage = "Home Assistant device name may only contain letters, numbers, spaces, hyphens, underscores, periods, and parentheses";
            return false;
        }

        validationMessage = null;
        return true;
    }
}