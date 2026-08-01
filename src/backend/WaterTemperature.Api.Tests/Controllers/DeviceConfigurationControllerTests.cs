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

public class DeviceConfigurationControllerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IDeviceApiKeyService _deviceApiKeyService;
    private readonly IHomeAssistantMqttSyncService _homeAssistantMqttSyncService;
    private readonly DeviceConfigurationController _controller;

    public DeviceConfigurationControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _deviceApiKeyService = new DeviceApiKeyService(DataProtectionProvider.Create("WaterTemperature.Api.Tests"));
        _homeAssistantMqttSyncService = new FakeHomeAssistantMqttSyncService();
        _controller = new DeviceConfigurationController(
            _dbContext,
            _deviceApiKeyService,
            _homeAssistantMqttSyncService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            }
        };
    }

    [Fact]
    public async Task Sync_WithInvalidApiKey_ReturnsUnauthorized()
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

        var result = await _controller.Sync(
            "device-1",
            new DeviceConfigurationSyncRequest(1, 30, 1, false, true));

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Sync_WithValidApiKey_StoresAppliedConfigurationAndReturnsDesiredConfiguration()
    {
        var credential = _deviceApiKeyService.CreateCredential();
        _dbContext.Devices.Add(new Device
        {
            DeviceIdentifier = "device-1",
            Status = DeviceRegistrationStatus.Registered,
            ReportIntervalSeconds = 90,
            DesiredConfigurationVersion = 3,
            ApiKeyHash = credential.ApiKeyHash,
            PendingApiKeyProtected = credential.ProtectedApiKey,
        });
        await _dbContext.SaveChangesAsync();

        _controller.Request.Headers[DeviceAuthenticationHeaders.ApiKeyHeaderName] = credential.PlainTextApiKey;

        var result = await _controller.Sync(
            "device-1",
            new DeviceConfigurationSyncRequest(2, 60, 1, false, true));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceConfigurationSyncResponse>(ok.Value);
        var device = await _dbContext.Devices.SingleAsync();

        Assert.Equal("device-1", response.DeviceId);
        Assert.Equal(3, response.Configuration.DesiredConfigurationVersion);
        Assert.Equal(90, response.Configuration.ReportIntervalSeconds);
        Assert.False(response.IsSynchronized);
        Assert.Equal(3, response.MaximumAttempts);
        Assert.Equal(2, device.ReportedConfigurationVersion);
        Assert.Equal(60, device.ReportedReportIntervalSeconds);
        Assert.Equal(DeviceConfigurationSyncStatus.Pending, device.ConfigurationSyncStatus);
        Assert.Equal(1, device.ConfigurationSyncAttemptCount);
        Assert.Null(device.ConfigurationSyncError);
        Assert.NotNull(device.RuntimeConfigurationReportedAtUtc);
        Assert.NotNull(device.LastSeenAtUtc);
        Assert.NotNull(device.ApiKeyLastUsedAtUtc);
        Assert.Null(device.PendingApiKeyProtected);
    }

    [Fact]
    public async Task Sync_WithVerifiedDesiredConfiguration_MarksConfigurationSynchronized()
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

        var result = await _controller.Sync(
            "device-1",
            new DeviceConfigurationSyncRequest(3, 90, 1, true, true));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceConfigurationSyncResponse>(ok.Value);
        var device = await _dbContext.Devices.SingleAsync();

        Assert.True(response.IsSynchronized);
        Assert.Equal(DeviceConfigurationSyncStatus.Synchronized, device.ConfigurationSyncStatus);
        Assert.Equal(1, device.ConfigurationSyncAttemptCount);
        Assert.Null(device.ConfigurationSyncError);
        Assert.NotNull(device.ConfigurationSyncStatusUpdatedAtUtc);
    }

    [Fact]
    public async Task Sync_ThirdFailedConfirmation_MarksConfigurationFailed()
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

        var result = await _controller.Sync(
            "device-1",
            new DeviceConfigurationSyncRequest(2, 60, 3, true, true));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceConfigurationSyncResponse>(ok.Value);
        var device = await _dbContext.Devices.SingleAsync();

        Assert.False(response.IsSynchronized);
        Assert.Equal(DeviceConfigurationSyncStatus.Failed, device.ConfigurationSyncStatus);
        Assert.Equal(3, device.ConfigurationSyncAttemptCount);
        Assert.Contains("desired version is 3", device.ConfigurationSyncError);
    }

    [Fact]
    public async Task Sync_UnverifiedStorage_CannotBeMarkedSynchronized()
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

        var result = await _controller.Sync(
            "device-1",
            new DeviceConfigurationSyncRequest(3, 90, 3, true, false));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceConfigurationSyncResponse>(ok.Value);
        var device = await _dbContext.Devices.SingleAsync();

        Assert.False(response.IsSynchronized);
        Assert.Equal(DeviceConfigurationSyncStatus.Failed, device.ConfigurationSyncStatus);
        Assert.Contains("stored configuration", device.ConfigurationSyncError);
    }

    [Theory]
    [InlineData(-1, 30)]
    [InlineData(1, 0)]
    public async Task Sync_WithInvalidAppliedConfiguration_ReturnsBadRequest(
        int appliedConfigurationVersion,
        int appliedReportIntervalSeconds)
    {
        var credential = _deviceApiKeyService.CreateCredential();
        _dbContext.Devices.Add(new Device
        {
            DeviceIdentifier = "device-1",
            Status = DeviceRegistrationStatus.Registered,
            ApiKeyHash = credential.ApiKeyHash,
        });
        await _dbContext.SaveChangesAsync();

        _controller.Request.Headers[DeviceAuthenticationHeaders.ApiKeyHeaderName] = credential.PlainTextApiKey;

        var result = await _controller.Sync(
            "device-1",
            new DeviceConfigurationSyncRequest(
                appliedConfigurationVersion,
                appliedReportIntervalSeconds,
                1,
                false,
                true));

        Assert.IsType<BadRequestObjectResult>(result.Result);

        var device = await _dbContext.Devices.SingleAsync();
        Assert.Null(device.ReportedConfigurationVersion);
        Assert.Null(device.ReportedReportIntervalSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Sync_WithInvalidAttempt_ReturnsBadRequest(int syncAttempt)
    {
        var credential = _deviceApiKeyService.CreateCredential();
        _dbContext.Devices.Add(new Device
        {
            DeviceIdentifier = "device-1",
            Status = DeviceRegistrationStatus.Registered,
            ApiKeyHash = credential.ApiKeyHash,
        });
        await _dbContext.SaveChangesAsync();

        _controller.Request.Headers[DeviceAuthenticationHeaders.ApiKeyHeaderName] = credential.PlainTextApiKey;

        var result = await _controller.Sync(
            "device-1",
            new DeviceConfigurationSyncRequest(1, 30, syncAttempt, false, true));

        Assert.IsType<BadRequestObjectResult>(result.Result);
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
