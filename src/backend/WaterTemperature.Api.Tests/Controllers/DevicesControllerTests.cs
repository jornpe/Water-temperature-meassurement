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

        _dbContext.DeviceLogEntries.AddRange(
            new DeviceLogEntry { DeviceId = device.Id, SequenceNumber = 1, Message = "one", ReceivedAtUtc = DateTime.UtcNow.AddMinutes(-2) },
            new DeviceLogEntry { DeviceId = device.Id, SequenceNumber = 2, Message = "two", ReceivedAtUtc = DateTime.UtcNow.AddMinutes(-1) },
            new DeviceLogEntry { DeviceId = device.Id, SequenceNumber = 3, Message = "three", ReceivedAtUtc = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetDeviceLogs(device.Id, 1, 2);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceLogsResponse>(okResult.Value);

        Assert.Equal(3, response.TotalCount);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal([3L, 2L], response.Items.Select(item => item.SequenceNumber).ToArray());
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