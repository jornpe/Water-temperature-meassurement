using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterTemperature.Api.Controllers;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Models.Auth;
using WaterTemperature.Api.Models.Devices;
using WaterTemperature.Api.Services;
using Xunit;

namespace WaterTemperature.Api.Tests.Controllers;

public class DevicesControllerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IDeviceApiKeyService _deviceApiKeyService;
    private readonly IHomeAssistantMqttSyncService _homeAssistantMqttSyncService;
    private readonly DevicesController _controller;
    private readonly DeviceDiscoveryController _discoveryController;

    public DevicesControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _deviceApiKeyService = new DeviceApiKeyService(DataProtectionProvider.Create("WaterTemperature.Api.Tests"));
        _homeAssistantMqttSyncService = new FakeHomeAssistantMqttSyncService();
        _controller = new DevicesController(_dbContext, _deviceApiKeyService, _homeAssistantMqttSyncService);
        _discoveryController = new DeviceDiscoveryController(_dbContext, _deviceApiKeyService);
    }

    [Fact]
    public async Task GetDevices_IncludesLatestBatterySummary()
    {
        _dbContext.Devices.Add(new Device
        {
            DeviceIdentifier = "device-1",
            Status = DeviceRegistrationStatus.Registered,
            LatestBatteryPercentage = 74,
            LatestBatteryChargeState = 1,
            LatestBatteryState = BatteryState.Charging,
            LatestBatteryAtUtc = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetDevices();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsAssignableFrom<IEnumerable<DeviceSummaryResponse>>(okResult.Value);
        var device = Assert.Single(response);

        Assert.Equal(74, device.LatestBatteryPercentage);
        Assert.Equal("Charging", device.LatestBatteryStatus);
        Assert.NotNull(device.LatestBatteryAtUtc);
    }

    [Fact]
    public async Task GetDevicePositions_FiltersAndReturnsPointsChronologically()
    {
        var device = new Device { DeviceIdentifier = "device-1", Status = DeviceRegistrationStatus.Registered };
        _dbContext.Devices.Add(device);
        await _dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        _dbContext.DevicePositionHistory.AddRange(
            new DevicePositionHistory { DeviceId = device.Id, Latitude = 59.91, Longitude = 10.75, RecordedAtUtc = now.AddHours(-3) },
            new DevicePositionHistory { DeviceId = device.Id, Latitude = 59.92, Longitude = 10.76, RecordedAtUtc = now.AddHours(-2) },
            new DevicePositionHistory { DeviceId = device.Id, Latitude = 59.93, Longitude = 10.77, RecordedAtUtc = now.AddHours(-1) });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetDevicePositions(
            device.Id,
            fromUtc: now.AddHours(-2.5),
            toUtc: now.AddMinutes(-30));

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DevicePositionHistoryResponse>(okResult.Value);

        Assert.Equal(2, response.TotalCount);
        Assert.False(response.IsSampled);
        Assert.Equal([59.92, 59.93], response.Items.Select(item => item.Latitude).ToArray());
    }

    [Fact]
    public async Task GetDeviceBatteryHistory_SamplesLargeSelectedRangeAndKeepsLastPoint()
    {
        var device = new Device { DeviceIdentifier = "device-1", Status = DeviceRegistrationStatus.Registered };
        _dbContext.Devices.Add(device);
        await _dbContext.SaveChangesAsync();

        var fromUtc = DateTime.UtcNow.AddDays(-2);
        var entries = Enumerable.Range(0, 205)
            .Select(index => new DeviceBatteryHistory
            {
                DeviceId = device.Id,
                ModemReadingValid = true,
                ChargeState = 0,
                BatteryState = BatteryState.NotCharging,
                Percentage = index % 101,
                ModemMillivolts = 3_700 + index,
                AdcVoltage = 3.7f,
                RecordedAtUtc = fromUtc.AddMinutes(index),
            });
        _dbContext.DeviceBatteryHistory.AddRange(entries);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetDeviceBatteryHistory(
            device.Id,
            fromUtc,
            fromUtc.AddDays(1),
            maxPoints: 100);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceBatteryHistoryResponse>(okResult.Value);

        Assert.Equal(205, response.TotalCount);
        Assert.True(response.IsSampled);
        Assert.InRange(response.Items.Count, 2, 100);
        Assert.Equal(fromUtc.AddMinutes(204), response.Items[^1].RecordedAtUtc);
    }

    [Fact]
    public async Task GetBatteryHistory_WithReversedRange_ReturnsBadRequest()
    {
        var now = DateTime.UtcNow;

        var result = await _controller.GetDeviceBatteryHistory(1, now, now.AddHours(-1));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RegisterDevice_ValidRequest_PersistsRegisteredStateAndCredential()
    {
        var device = new Device { DeviceIdentifier = "device-1" };
        _dbContext.Devices.Add(device);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.RegisterDevice(device.Id, new DeviceRegistrationRequest("Pool Sensor", "Pool", 120, true, "Pool Sensor HA"));

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceRegistrationResponse>(okResult.Value);
        var storedDevice = await _dbContext.Devices.SingleAsync();

        Assert.Equal("registered", response.Status);
        Assert.Equal(DeviceRegistrationStatus.Registered, storedDevice.Status);
        Assert.Equal("Pool Sensor", storedDevice.Name);
        Assert.Equal("Pool", storedDevice.Place);
        Assert.True(storedDevice.PushToHomeAssistant);
        Assert.Equal("Pool Sensor HA", storedDevice.HomeAssistantDeviceName);
        Assert.Equal(120, storedDevice.ReportIntervalSeconds);
        Assert.Equal(1, storedDevice.DesiredConfigurationVersion);
        Assert.NotNull(storedDevice.DesiredConfigurationUpdatedAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(storedDevice.ApiKeyHash));
        Assert.False(string.IsNullOrWhiteSpace(storedDevice.PendingApiKeyProtected));
    }

    [Fact]
    public async Task UpdateDevice_ReportIntervalChange_IncrementsConfigurationVersion()
    {
        var device = new Device
        {
            DeviceIdentifier = "device-1",
            Status = DeviceRegistrationStatus.Registered,
            Name = "Pool Sensor",
            Place = "Pool",
            ReportIntervalSeconds = 120,
            DesiredConfigurationVersion = 1,
            DesiredConfigurationUpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            ReportedConfigurationVersion = 1,
            ReportedReportIntervalSeconds = 120,
            RuntimeConfigurationReportedAtUtc = DateTime.UtcNow.AddMinutes(-1),
        };
        _dbContext.Devices.Add(device);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.UpdateDevice(device.Id, new RegisteredDeviceUpdateRequest("Pool Sensor v2", "Pool Deck", 300, true, "Pool Sensor Custom"));

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceDetailResponse>(okResult.Value);
        var storedDevice = await _dbContext.Devices.SingleAsync();

        Assert.Equal("Pool Sensor v2", storedDevice.Name);
        Assert.Equal("Pool Deck", storedDevice.Place);
        Assert.True(storedDevice.PushToHomeAssistant);
        Assert.Equal("Pool Sensor Custom", storedDevice.HomeAssistantDeviceName);
        Assert.Equal(300, storedDevice.ReportIntervalSeconds);
        Assert.Equal(2, storedDevice.DesiredConfigurationVersion);
        Assert.True(response.HasPendingConfiguration);
        Assert.Equal(2, response.DesiredConfiguration.Version);
        Assert.Equal("Pool Sensor Custom", response.HomeAssistantDeviceName);
        Assert.Equal(1, response.RuntimeConfiguration.AppliedConfigurationVersion);
    }

    [Fact]
    public async Task UpdateDevice_WhenOverrideMatchesDeviceName_ClearsOverrideAndUsesSystemName()
    {
        var device = new Device
        {
            DeviceIdentifier = "device-1",
            Status = DeviceRegistrationStatus.Registered,
            Name = "Pool Sensor",
            Place = "Pool",
            PushToHomeAssistant = true,
            HomeAssistantDeviceName = "Pool Sensor Custom",
            ReportIntervalSeconds = 120,
            DesiredConfigurationVersion = 1,
        };
        _dbContext.Devices.Add(device);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.UpdateDevice(device.Id, new RegisteredDeviceUpdateRequest("Pool Sensor v2", "Pool", 120, true, "Pool Sensor v2"));

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceDetailResponse>(okResult.Value);
        var storedDevice = await _dbContext.Devices.SingleAsync();

        Assert.Null(storedDevice.HomeAssistantDeviceName);
        Assert.Equal("Pool Sensor v2", response.HomeAssistantDeviceName);
    }

    [Fact]
    public async Task GetDeviceLogs_ReturnsPagedNewestFirstLogEntries()
    {
        var device = new Device { DeviceIdentifier = "device-1", Status = DeviceRegistrationStatus.Registered };
        _dbContext.Devices.Add(device);
        await _dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        _dbContext.DeviceLogEntries.AddRange(
            new DeviceLogEntry { DeviceId = device.Id, Message = "one", TimestampUtc = now.AddMinutes(-2) },
            new DeviceLogEntry { DeviceId = device.Id, Message = "two", TimestampUtc = now.AddMinutes(-1) },
            new DeviceLogEntry { DeviceId = device.Id, Message = "three", TimestampUtc = now });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetDeviceLogs(device.Id, 1, 2);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceLogsResponse>(okResult.Value);

        Assert.Equal(3L, response.TotalCount);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(["three", "two"], response.Items.Select(item => item.Message).ToArray());
        Assert.True(response.HasMore);
        Assert.Equal(response.Items[^1].Id, response.NextBeforeId);
    }

    [Fact]
    public async Task GetDeviceLogs_SearchesAndFiltersTheFullStoredHistory()
    {
        var device = new Device { DeviceIdentifier = "device-1", Status = DeviceRegistrationStatus.Registered };
        _dbContext.Devices.Add(device);
        await _dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        _dbContext.DeviceLogEntries.AddRange(
            new DeviceLogEntry { DeviceId = device.Id, Level = "info", Message = "startup complete", TimestampUtc = now.AddDays(-2) },
            new DeviceLogEntry { DeviceId = device.Id, Level = "warning", Message = "Cellular signal is weak", TimestampUtc = now.AddHours(-2) },
            new DeviceLogEntry { DeviceId = device.Id, Level = "warning", Message = "unrelated warning", TimestampUtc = now });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetDeviceLogs(
            device.Id,
            pageSize: 1,
            search: "SIGNAL",
            level: "WARNING",
            fromUtc: now.AddHours(-3),
            toUtc: now.AddHours(-1));

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceLogsResponse>(okResult.Value);

        Assert.Equal(1L, response.TotalCount);
        var entry = Assert.Single(response.Items);
        Assert.Equal("Cellular signal is weak", entry.Message);
        Assert.False(response.HasMore);
    }

    [Fact]
    public async Task GetDeviceLogs_AfterCursorReturnsEveryNewEntryOldestBatchFirst()
    {
        var device = new Device { DeviceIdentifier = "device-1", Status = DeviceRegistrationStatus.Registered };
        _dbContext.Devices.Add(device);
        await _dbContext.SaveChangesAsync();

        var entries = Enumerable.Range(1, 5)
            .Select(index => new DeviceLogEntry
            {
                DeviceId = device.Id,
                Message = $"line {index}",
                TimestampUtc = DateTime.UtcNow.AddSeconds(index),
            })
            .ToArray();
        _dbContext.DeviceLogEntries.AddRange(entries);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetDeviceLogs(
            device.Id,
            pageSize: 2,
            afterId: entries[0].Id,
            includeTotalCount: false);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceLogsResponse>(okResult.Value);

        Assert.Null(response.TotalCount);
        Assert.True(response.HasMore);
        Assert.Equal(["line 3", "line 2"], response.Items.Select(item => item.Message).ToArray());
    }

    [Fact]
    public async Task DeleteDeviceLogs_WithCutoffKeepsRecentEntries()
    {
        var device = new Device { DeviceIdentifier = "device-1", Status = DeviceRegistrationStatus.Registered };
        _dbContext.Devices.Add(device);
        await _dbContext.SaveChangesAsync();

        var cutoff = DateTime.UtcNow.AddHours(-24);
        _dbContext.DeviceLogEntries.AddRange(
            new DeviceLogEntry { DeviceId = device.Id, Message = "old", TimestampUtc = cutoff.AddMinutes(-1) },
            new DeviceLogEntry { DeviceId = device.Id, Message = "recent", TimestampUtc = cutoff.AddMinutes(1) });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteDeviceLogs(device.Id, cutoff);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceLogsDeleteResponse>(okResult.Value);

        Assert.Equal(1, response.DeletedCount);
        var remaining = await _dbContext.DeviceLogEntries.SingleAsync();
        Assert.Equal("recent", remaining.Message);
    }

    [Fact]
    public async Task RegenerateApiKey_ReplacesOldCredentialAndDiscoveryReturnsNewKey()
    {
        var device = new Device { DeviceIdentifier = "device-1" };
        _dbContext.Devices.Add(device);
        await _dbContext.SaveChangesAsync();

        await _controller.RegisterDevice(device.Id, new DeviceRegistrationRequest("Pool Sensor", "Pool", 120, false));
        var firstDiscovery = await _discoveryController.Discover(new DeviceDiscoveryRequest(device.DeviceIdentifier, "1.0.0", "wifi"));
        var firstOk = Assert.IsType<OkObjectResult>(firstDiscovery.Result);
        var firstResponse = Assert.IsType<DeviceDiscoveryResponse>(firstOk.Value);

        var regenerateResult = await _controller.RegenerateApiKey(device.Id);
        Assert.IsType<OkObjectResult>(regenerateResult.Result);

        var secondDiscovery = await _discoveryController.Discover(new DeviceDiscoveryRequest(device.DeviceIdentifier, "1.0.0", "wifi"));
        var secondOk = Assert.IsType<OkObjectResult>(secondDiscovery.Result);
        var secondResponse = Assert.IsType<DeviceDiscoveryResponse>(secondOk.Value);
        var storedDevice = await _dbContext.Devices.SingleAsync();

        Assert.NotEqual(firstResponse.ApiKey, secondResponse.ApiKey);
        Assert.False(_deviceApiKeyService.Verify(firstResponse.ApiKey!, storedDevice.ApiKeyHash));
        Assert.True(_deviceApiKeyService.Verify(secondResponse.ApiKey!, storedDevice.ApiKeyHash));
    }

    [Fact]
    public async Task ClearTelemetry_TemperatureCategory_RemovesHistoryAndResetsSnapshot()
    {
        var device = new Device
        {
            DeviceIdentifier = "device-1",
            LatestTemperatureCelsius = 17.5m,
            LatestTemperatureAtUtc = DateTime.UtcNow,
            TemperatureHistory =
            {
                new DeviceTemperatureHistory { TemperatureCelsius = 17.5m, RecordedAtUtc = DateTime.UtcNow },
                new DeviceTemperatureHistory { TemperatureCelsius = 18.1m, RecordedAtUtc = DateTime.UtcNow },
            }
        };
        _dbContext.Devices.Add(device);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.ClearTelemetry(device.Id, "temperature");

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceTelemetryClearResponse>(okResult.Value);
        var storedDevice = await _dbContext.Devices.SingleAsync();

        Assert.Equal(2, response.DeletedCount);
        Assert.Null(storedDevice.LatestTemperatureCelsius);
        Assert.Null(storedDevice.LatestTemperatureAtUtc);
        Assert.Empty(_dbContext.DeviceTemperatureHistory);
    }

    [Fact]
    public async Task DeleteDevice_RemovesDeviceAndDependentHistory()
    {
        var device = new Device { DeviceIdentifier = "device-1" };
        _dbContext.Devices.Add(device);
        await _dbContext.SaveChangesAsync();

        _dbContext.DeviceTemperatureHistory.Add(new DeviceTemperatureHistory
        {
            DeviceId = device.Id,
            TemperatureCelsius = 17.5m,
            RecordedAtUtc = DateTime.UtcNow,
        });
        _dbContext.DevicePositionHistory.Add(new DevicePositionHistory
        {
            DeviceId = device.Id,
            Latitude = 1,
            Longitude = 2,
            RecordedAtUtc = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteDevice(device.Id);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceDeleteResponse>(okResult.Value);

        Assert.Equal("device-1", response.DeviceId);
        Assert.Empty(_dbContext.Devices);
        Assert.Empty(_dbContext.DeviceTemperatureHistory);
        Assert.Empty(_dbContext.DevicePositionHistory);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}

file sealed class FakeHomeAssistantMqttSyncService : IHomeAssistantMqttSyncService
{
    public ValueTask RefreshAllAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask SyncDeviceAsync(int deviceId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask PublishDeviceStateAsync(int deviceId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask RemoveDeviceAsync(string deviceIdentifier, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
