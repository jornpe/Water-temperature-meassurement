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
    private const int DefaultPositionHistoryPoints = 5_000;
    private const int MaxPositionHistoryPoints = 10_000;
    private const int DefaultTemperatureHistoryPoints = 1_500;
    private const int MaxTemperatureHistoryPoints = 5_000;
    private const int DefaultBatteryHistoryPoints = 1_500;
    private const int MaxBatteryHistoryPoints = 5_000;
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
                device.LatestBatteryPercentage,
                device.LatestBatteryState == BatteryState.Charging || device.LatestBatteryChargeState == 1
                    ? "Charging"
                    : device.LatestBatteryState == BatteryState.Full || device.LatestBatteryChargeState == 2
                        ? "Full"
                        : device.LatestBatteryState == BatteryState.NotCharging || device.LatestBatteryChargeState == 0
                            ? "Not charging"
                            : device.LatestBatteryPercentage.HasValue
                                ? "Unknown"
                                : null,
                device.LatestBatteryAtUtc,
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
                    && (item.ConfigurationSyncStatus != DeviceConfigurationSyncStatus.Synchronized
                        || item.DesiredConfigurationVersion != item.ReportedConfigurationVersion
                        || item.ReportIntervalSeconds != item.ReportedReportIntervalSeconds),
                new DeviceConfigurationSyncStateResponse(
                    item.ConfigurationSyncStatus == DeviceConfigurationSyncStatus.Synchronized
                        ? "synchronized"
                        : item.ConfigurationSyncStatus == DeviceConfigurationSyncStatus.Failed
                            ? "failed"
                            : "pending",
                    item.ConfigurationSyncAttemptCount,
                    DeviceConfigurationSyncPolicy.MaximumAttempts,
                    item.ConfigurationSyncError,
                    item.ConfigurationSyncStatusUpdatedAtUtc),
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
                new DeviceBatteryDiagnosticsResponse(
                    item.LatestBatteryModemReadingValid,
                    item.LatestBatteryChargeState,
                    item.LatestBatteryState,
                    item.LatestBatteryPercentage,
                    item.LatestBatteryModemMillivolts,
                    item.LatestBatteryAdcVoltage,
                    item.LatestBatteryAtUtc),
                item.TemperatureHistory.Count,
                item.PositionHistory.Count))
            .SingleOrDefaultAsync();

        return device is null ? NotFound() : Ok(device);
    }

    [HttpGet("{id:int}/positions")]
    public async Task<ActionResult<DevicePositionHistoryResponse>> GetDevicePositions(
        int id,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int maxPoints = DefaultPositionHistoryPoints,
        CancellationToken cancellationToken = default)
    {
        var normalizedFromUtc = fromUtc.HasValue ? NormalizeUtc(fromUtc.Value) : (DateTime?)null;
        var normalizedToUtc = toUtc.HasValue ? NormalizeUtc(toUtc.Value) : (DateTime?)null;

        if (normalizedFromUtc > normalizedToUtc)
        {
            return BadRequest(new MessageResponse("The position-history start time must be earlier than the end time"));
        }

        var device = await dbContext.Devices
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, item.DeviceIdentifier })
            .SingleOrDefaultAsync(cancellationToken);

        if (device is null)
        {
            return NotFound();
        }

        var normalizedMaxPoints = Math.Clamp(maxPoints, 100, MaxPositionHistoryPoints);
        var query = dbContext.DevicePositionHistory
            .AsNoTracking()
            .Where(item => item.DeviceId == id);

        if (normalizedFromUtc.HasValue)
        {
            query = query.Where(item => item.RecordedAtUtc >= normalizedFromUtc.Value);
        }

        if (normalizedToUtc.HasValue)
        {
            query = query.Where(item => item.RecordedAtUtc <= normalizedToUtc.Value);
        }

        var totalCount = await query.LongCountAsync(cancellationToken);
        var sampleStride = Math.Max(1L, (long)Math.Ceiling(totalCount / (double)normalizedMaxPoints));
        var items = new List<DevicePositionHistoryPointResponse>(
            (int)Math.Min(totalCount, normalizedMaxPoints));
        DevicePositionHistory? lastEntry = null;
        long index = 0;

        await foreach (var entry in query
            .OrderBy(item => item.RecordedAtUtc)
            .ThenBy(item => item.Id)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            lastEntry = entry;

            if (index % sampleStride == 0)
            {
                items.Add(ToPositionHistoryResponse(entry));
            }

            index += 1;
        }

        if (lastEntry is not null && (items.Count == 0 || items[^1].Id != lastEntry.Id))
        {
            var lastResponse = ToPositionHistoryResponse(lastEntry);
            if (items.Count >= normalizedMaxPoints)
            {
                items[^1] = lastResponse;
            }
            else
            {
                items.Add(lastResponse);
            }
        }

        return Ok(new DevicePositionHistoryResponse(
            device.Id,
            device.DeviceIdentifier,
            normalizedFromUtc,
            normalizedToUtc,
            totalCount,
            sampleStride > 1,
            items));
    }

    [HttpGet("{id:int}/battery-history")]
    public async Task<ActionResult<DeviceBatteryHistoryResponse>> GetDeviceBatteryHistory(
        int id,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int maxPoints = DefaultBatteryHistoryPoints,
        CancellationToken cancellationToken = default)
    {
        var normalizedToUtc = toUtc.HasValue ? NormalizeUtc(toUtc.Value) : DateTime.UtcNow;
        var normalizedFromUtc = fromUtc.HasValue
            ? NormalizeUtc(fromUtc.Value)
            : normalizedToUtc.AddDays(-1);

        if (normalizedFromUtc > normalizedToUtc)
        {
            return BadRequest(new MessageResponse("The battery-history start time must be earlier than the end time"));
        }

        var device = await dbContext.Devices
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, item.DeviceIdentifier })
            .SingleOrDefaultAsync(cancellationToken);

        if (device is null)
        {
            return NotFound();
        }

        var normalizedMaxPoints = Math.Clamp(maxPoints, 100, MaxBatteryHistoryPoints);
        var query = dbContext.DeviceBatteryHistory
            .AsNoTracking()
            .Where(item => item.DeviceId == id
                && item.RecordedAtUtc >= normalizedFromUtc
                && item.RecordedAtUtc <= normalizedToUtc);

        var totalCount = await query.LongCountAsync(cancellationToken);
        var sampleStride = Math.Max(1L, (long)Math.Ceiling(totalCount / (double)normalizedMaxPoints));
        var items = new List<DeviceBatteryHistoryPointResponse>(
            (int)Math.Min(totalCount, normalizedMaxPoints));
        DeviceBatteryHistory? lastEntry = null;
        long index = 0;

        await foreach (var entry in query
            .OrderBy(item => item.RecordedAtUtc)
            .ThenBy(item => item.Id)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            lastEntry = entry;

            if (index % sampleStride == 0)
            {
                items.Add(ToBatteryHistoryResponse(entry));
            }

            index += 1;
        }

        if (lastEntry is not null && (items.Count == 0 || items[^1].Id != lastEntry.Id))
        {
            var lastResponse = ToBatteryHistoryResponse(lastEntry);
            if (items.Count >= normalizedMaxPoints)
            {
                items[^1] = lastResponse;
            }
            else
            {
                items.Add(lastResponse);
            }
        }

        return Ok(new DeviceBatteryHistoryResponse(
            device.Id,
            device.DeviceIdentifier,
            normalizedFromUtc,
            normalizedToUtc,
            totalCount,
            sampleStride > 1,
            items));
    }

    [HttpGet("{id:int}/temperature-history")]
    public async Task<ActionResult<DeviceTemperatureHistoryResponse>> GetDeviceTemperatureHistory(
        int id,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int maxPoints = DefaultTemperatureHistoryPoints,
        CancellationToken cancellationToken = default)
    {
        var normalizedToUtc = toUtc.HasValue ? NormalizeUtc(toUtc.Value) : DateTime.UtcNow;
        var normalizedFromUtc = fromUtc.HasValue
            ? NormalizeUtc(fromUtc.Value)
            : normalizedToUtc.AddDays(-1);

        if (normalizedFromUtc > normalizedToUtc)
        {
            return BadRequest(new MessageResponse("The temperature-history start time must be earlier than the end time"));
        }

        var device = await dbContext.Devices
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, item.DeviceIdentifier })
            .SingleOrDefaultAsync(cancellationToken);

        if (device is null)
        {
            return NotFound();
        }

        var normalizedMaxPoints = Math.Clamp(maxPoints, 100, MaxTemperatureHistoryPoints);
        var query = dbContext.DeviceTemperatureHistory
            .AsNoTracking()
            .Where(item => item.DeviceId == id
                && item.RecordedAtUtc >= normalizedFromUtc
                && item.RecordedAtUtc <= normalizedToUtc);

        var totalCount = await query.LongCountAsync(cancellationToken);
        var sampleStride = Math.Max(1L, (long)Math.Ceiling(totalCount / (double)normalizedMaxPoints));
        var items = new List<DeviceTemperatureHistoryPointResponse>(
            (int)Math.Min(totalCount, normalizedMaxPoints));
        DeviceTemperatureHistory? lastEntry = null;
        long index = 0;

        await foreach (var entry in query
            .OrderBy(item => item.RecordedAtUtc)
            .ThenBy(item => item.Id)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            lastEntry = entry;

            if (index % sampleStride == 0)
            {
                items.Add(ToTemperatureHistoryResponse(entry));
            }

            index += 1;
        }

        if (lastEntry is not null && (items.Count == 0 || items[^1].Id != lastEntry.Id))
        {
            var lastResponse = ToTemperatureHistoryResponse(lastEntry);
            if (items.Count >= normalizedMaxPoints)
            {
                items[^1] = lastResponse;
            }
            else
            {
                items.Add(lastResponse);
            }
        }

        return Ok(new DeviceTemperatureHistoryResponse(
            device.Id,
            device.DeviceIdentifier,
            normalizedFromUtc,
            normalizedToUtc,
            totalCount,
            sampleStride > 1,
            items));
    }

    [HttpGet("{id:int}/logs")]
    public async Task<ActionResult<DeviceLogsResponse>> GetDeviceLogs(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultLogsPageSize,
        [FromQuery] string? search = null,
        [FromQuery] string? level = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int? beforeId = null,
        [FromQuery] int? afterId = null,
        [FromQuery] bool includeTotalCount = true,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxLogsPageSize);

        if (beforeId.HasValue && afterId.HasValue)
        {
            return BadRequest(new MessageResponse("Only one log cursor can be supplied at a time"));
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (normalizedSearch?.Length > 200)
        {
            return BadRequest(new MessageResponse("Log search text must be 200 characters or fewer"));
        }

        var normalizedLevel = string.IsNullOrWhiteSpace(level) ? null : level.Trim().ToLowerInvariant();
        if (normalizedLevel?.Length > 32)
        {
            return BadRequest(new MessageResponse("Log level must be 32 characters or fewer"));
        }

        DateTime? normalizedFromUtc = fromUtc.HasValue ? NormalizeUtc(fromUtc.Value) : null;
        DateTime? normalizedToUtc = toUtc.HasValue ? NormalizeUtc(toUtc.Value) : null;
        if (normalizedFromUtc > normalizedToUtc)
        {
            return BadRequest(new MessageResponse("The log start time must be earlier than the end time"));
        }

        var device = await dbContext.Devices
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, item.DeviceIdentifier })
            .SingleOrDefaultAsync(cancellationToken);

        if (device is null)
        {
            return NotFound();
        }

        var logsQuery = dbContext.DeviceLogEntries
            .AsNoTracking()
            .Where(entry => entry.DeviceId == id);

        if (normalizedSearch is not null)
        {
            if (dbContext.Database.IsNpgsql())
            {
                var escapedSearch = normalizedSearch
                    .Replace(@"\", @"\\", StringComparison.Ordinal)
                    .Replace("%", @"\%", StringComparison.Ordinal)
                    .Replace("_", @"\_", StringComparison.Ordinal);
                var searchPattern = $"%{escapedSearch}%";
                logsQuery = logsQuery.Where(entry => EF.Functions.ILike(entry.Message, searchPattern, @"\"));
            }
            else
            {
                var lowercaseSearch = normalizedSearch.ToLowerInvariant();
                logsQuery = logsQuery.Where(entry => entry.Message.ToLower().Contains(lowercaseSearch));
            }
        }

        if (normalizedLevel is not null)
        {
            logsQuery = logsQuery.Where(entry => entry.Level == normalizedLevel);
        }

        if (normalizedFromUtc.HasValue)
        {
            logsQuery = logsQuery.Where(entry => entry.TimestampUtc >= normalizedFromUtc.Value);
        }

        if (normalizedToUtc.HasValue)
        {
            logsQuery = logsQuery.Where(entry => entry.TimestampUtc <= normalizedToUtc.Value);
        }

        var totalCount = includeTotalCount
            ? await logsQuery.LongCountAsync(cancellationToken)
            : (long?)null;

        if (beforeId.HasValue)
        {
            logsQuery = logsQuery.Where(entry => entry.Id < beforeId.Value);
        }
        else if (afterId.HasValue)
        {
            logsQuery = logsQuery.Where(entry => entry.Id > afterId.Value);
        }

        IQueryable<DeviceLogEntry> orderedQuery = afterId.HasValue
            ? logsQuery.OrderBy(entry => entry.TimestampUtc).ThenBy(entry => entry.Id)
            : logsQuery.OrderByDescending(entry => entry.TimestampUtc).ThenByDescending(entry => entry.Id);

        if (!beforeId.HasValue && !afterId.HasValue && normalizedPage > 1)
        {
            orderedQuery = orderedQuery.Skip((normalizedPage - 1) * normalizedPageSize);
        }

        var items = await orderedQuery
            .Take(normalizedPageSize + 1)
            .Select(entry => new DeviceLogEntryResponse(
                entry.Id,
                entry.Level,
                entry.Message,
                entry.TimestampUtc))
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > normalizedPageSize;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        if (afterId.HasValue)
        {
            items.Reverse();
        }

        return Ok(new DeviceLogsResponse(
            device.Id,
            device.DeviceIdentifier,
            normalizedPage,
            normalizedPageSize,
            totalCount,
            hasMore,
            items.Count > 0 ? items[^1].Id : null,
            items));
    }

    [HttpDelete("{id:int}/logs")]
    public async Task<ActionResult<DeviceLogsDeleteResponse>> DeleteDeviceLogs(
        int id,
        [FromQuery] DateTime? beforeUtc = null,
        CancellationToken cancellationToken = default)
    {
        var device = await dbContext.Devices
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, item.DeviceIdentifier })
            .SingleOrDefaultAsync(cancellationToken);

        if (device is null)
        {
            return NotFound();
        }

        DateTime? normalizedBeforeUtc = beforeUtc.HasValue ? NormalizeUtc(beforeUtc.Value) : null;
        var logsQuery = dbContext.DeviceLogEntries.Where(entry => entry.DeviceId == id);

        if (normalizedBeforeUtc.HasValue)
        {
            logsQuery = logsQuery.Where(entry => entry.TimestampUtc < normalizedBeforeUtc.Value);
        }

        int deletedCount;
        if (dbContext.Database.IsRelational())
        {
            deletedCount = await logsQuery.ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            var entries = await logsQuery.ToListAsync(cancellationToken);
            deletedCount = entries.Count;
            dbContext.DeviceLogEntries.RemoveRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new DeviceLogsDeleteResponse(
            device.Id,
            device.DeviceIdentifier,
            normalizedBeforeUtc,
            deletedCount));
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
            device.ConfigurationSyncStatus = DeviceConfigurationSyncStatus.Pending;
            device.ConfigurationSyncAttemptCount = 0;
            device.ConfigurationSyncError = null;
            device.ConfigurationSyncStatusUpdatedAtUtc = now;
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

    private static DevicePositionHistoryPointResponse ToPositionHistoryResponse(DevicePositionHistory entry)
    {
        return new DevicePositionHistoryPointResponse(
            entry.Id,
            entry.Latitude,
            entry.Longitude,
            entry.AltitudeMeters,
            entry.GpsTimeUtc,
            entry.SpeedKnots,
            entry.Hdop,
            entry.SatellitesVisible,
            entry.SatellitesUsed,
            entry.RecordedAtUtc);
    }

    private static DeviceBatteryHistoryPointResponse ToBatteryHistoryResponse(DeviceBatteryHistory entry)
    {
        return new DeviceBatteryHistoryPointResponse(
            entry.Id,
            entry.ModemReadingValid,
            entry.ChargeState,
            entry.BatteryState,
            entry.Percentage,
            entry.ModemMillivolts,
            entry.AdcVoltage,
            entry.RecordedAtUtc);
    }

    private static DeviceTemperatureHistoryPointResponse ToTemperatureHistoryResponse(DeviceTemperatureHistory entry)
    {
        return new DeviceTemperatureHistoryPointResponse(
            entry.Id,
            entry.TemperatureCelsius,
            entry.RecordedAtUtc);
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }
}
