using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterTemperature.Api.Controllers;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Models.Devices;
using WaterTemperature.Api.Services;
using Xunit;

namespace WaterTemperature.Api.Tests.Controllers;

public class DeviceUpdatesControllerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IDeviceApiKeyService _deviceApiKeyService;
    private readonly IHomeAssistantMqttSyncService _homeAssistantMqttSyncService;
    private readonly DeviceUpdatesController _controller;

    public DeviceUpdatesControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _deviceApiKeyService = new DeviceApiKeyService(DataProtectionProvider.Create("WaterTemperature.Api.Tests"));
        _homeAssistantMqttSyncService = new FakeHomeAssistantMqttSyncService();
        _controller = new DeviceUpdatesController(_dbContext, _deviceApiKeyService, _homeAssistantMqttSyncService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            }
        };
    }

    [Fact]
    public async Task Update_WithInvalidApiKey_ReturnsUnauthorized()
    {
        var credential = _deviceApiKeyService.CreateCredential();
        _dbContext.Devices.Add(new Device
        {
            DeviceIdentifier = "device-1",
            Status = DeviceRegistrationStatus.Registered,
            ApiKeyHash = credential.ApiKeyHash,
        });
        await _dbContext.SaveChangesAsync();

        _controller.Request.Headers[DeviceAuthenticationHeaders.ApiKeyHeaderName] = "invalid-key";

        var result = await _controller.Update("device-1", new DeviceUpdateRequest("1.0.0", 20.5m, null, null, null));

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Update_WithValidApiKey_StoresHistoryAndLatestSnapshots()
    {
        var credential = _deviceApiKeyService.CreateCredential();
        _dbContext.Devices.Add(new Device
        {
            DeviceIdentifier = "device-1",
            Status = DeviceRegistrationStatus.Registered,
            ReportIntervalSeconds = 60,
            ApiKeyHash = credential.ApiKeyHash,
            PendingApiKeyProtected = credential.ProtectedApiKey,
        });
        await _dbContext.SaveChangesAsync();

        _controller.Request.Headers[DeviceAuthenticationHeaders.ApiKeyHeaderName] = credential.PlainTextApiKey;

        var request = new DeviceUpdateRequest(
            "1.0.1",
            21.75m,
            new DevicePositionUpdateRequest(52.1, 4.3, 5.5, DateTime.UtcNow, 2.4, 0.8, 10, 7),
            new Battery(true, 0, BatteryState.Unknown, 50, 1000, 1000),
            new DeviceNetworkDiagnosticsUpdateRequest(
                "wifi",
                new DeviceWifiDiagnosticsUpdateRequest("192.168.0.10", -55, "ssid", "bssid", 11, "192.168.0.1", "255.255.255.0", "8.8.8.8", "aa:bb:cc:dd:ee:ff"),
                null));

        var result = await _controller.Update("device-1", request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceUpdateResponse>(okResult.Value);
        var device = await _dbContext.Devices.SingleAsync();

        Assert.Equal("device-1", response.DeviceId);
        Assert.Equal(60, response.Configuration.ReportIntervalSeconds);
        Assert.Equal(21.75m, device.LatestTemperatureCelsius);
        Assert.Equal(52.1, device.LatestLatitude);
        Assert.Equal("wifi", device.LatestNetworkTransport);
        Assert.Equal("ssid", device.LatestWifiSsid);
        Assert.Null(device.PendingApiKeyProtected);
        Assert.Single(_dbContext.DeviceTemperatureHistory);
        Assert.Single(_dbContext.DevicePositionHistory);
    }

    [Fact]
    public async Task Update_WithRuntimeConfigurationAndDuplicateLogs_IsIdempotent()
    {
        var credential = _deviceApiKeyService.CreateCredential();
        _dbContext.Devices.Add(new Device
        {
            DeviceIdentifier = "device-1",
            Status = DeviceRegistrationStatus.Registered,
            ReportIntervalSeconds = 90,
            DesiredConfigurationVersion = 3,
            ApiKeyHash = credential.ApiKeyHash,
        });
        await _dbContext.SaveChangesAsync();

        _controller.Request.Headers[DeviceAuthenticationHeaders.ApiKeyHeaderName] = credential.PlainTextApiKey;

        var request = new DeviceUpdateRequest(
            FirmwareVersion: "1.0.2",
            Temperature: 22.1m,
            Position: null,
            Battery: null,
            Network: null,
            RuntimeConfiguration: new DeviceRuntimeConfigurationUpdateRequest(2, 60),
            Logs:
            [
                new DeviceLogEntryRequest(10, "boot", "info", DateTime.UtcNow, 1000),
                new DeviceLogEntryRequest(11, "connected", "info", null, 2000),
                new DeviceLogEntryRequest(11, "connected duplicate", "info", null, 2000)
            ]);

        var firstResult = await _controller.Update("device-1", request);
        var firstOk = Assert.IsType<OkObjectResult>(firstResult.Result);
        var firstResponse = Assert.IsType<DeviceUpdateResponse>(firstOk.Value);

        var secondResult = await _controller.Update("device-1", request);
        var secondOk = Assert.IsType<OkObjectResult>(secondResult.Result);
        var secondResponse = Assert.IsType<DeviceUpdateResponse>(secondOk.Value);

        var device = await _dbContext.Devices.SingleAsync();
        var logs = await _dbContext.DeviceLogEntries.OrderBy(entry => entry.SequenceNumber).ToListAsync();

        Assert.Equal(11, firstResponse.HighestAcknowledgedLogSequenceNumber);
        Assert.Equal(11, secondResponse.HighestAcknowledgedLogSequenceNumber);
        Assert.Equal(3, firstResponse.Configuration.DesiredConfigurationVersion);
        Assert.Equal(2, device.ReportedConfigurationVersion);
        Assert.Equal(60, device.ReportedReportIntervalSeconds);
        Assert.NotNull(device.RuntimeConfigurationReportedAtUtc);
        Assert.Equal([10L, 11L], logs.Select(entry => entry.SequenceNumber).ToArray());
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
