using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterTemperature.Api.Controllers;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Models.Devices;
using WaterTemperature.Api.Services;
using Xunit;

namespace WaterTemperature.Api.Tests.Controllers;

public class DeviceDiscoveryControllerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IDeviceApiKeyService _deviceApiKeyService;
    private readonly DeviceDiscoveryController _controller;

    public DeviceDiscoveryControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _deviceApiKeyService = new DeviceApiKeyService(DataProtectionProvider.Create("WaterTemperature.Api.Tests"));
        _controller = new DeviceDiscoveryController(_dbContext, _deviceApiKeyService);
    }

    [Fact]
    public async Task Discover_UnknownDevice_CreatesSingleUnregisteredRecord()
    {
        var result = await _controller.Discover(new DeviceDiscoveryRequest("device-1", "1.0.0", "wifi"));

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceDiscoveryResponse>(okResult.Value);

        Assert.Equal("unregistered", response.Status);
        Assert.Null(response.ApiKey);
        Assert.Equal(1, await _dbContext.Devices.CountAsync());

        var device = await _dbContext.Devices.SingleAsync();
        Assert.Equal("device-1", device.DeviceIdentifier);
        Assert.Equal(DeviceRegistrationStatus.Unregistered, device.Status);
        Assert.Equal("wifi", device.LastDiscoveryTransport);
    }

    [Fact]
    public async Task Discover_RepeatedRequest_DoesNotCreateDuplicates()
    {
        await _controller.Discover(new DeviceDiscoveryRequest("device-1", "1.0.0", "wifi"));
        await _controller.Discover(new DeviceDiscoveryRequest("device-1", "1.0.1", "cellular"));

        Assert.Equal(1, await _dbContext.Devices.CountAsync());

        var device = await _dbContext.Devices.SingleAsync();
        Assert.Equal("1.0.1", device.FirmwareVersion);
        Assert.Equal("cellular", device.LastDiscoveryTransport);
        Assert.NotNull(device.LastDiscoveredAtUtc);
    }

    [Fact]
    public async Task Discover_RegisteredDeviceWithPendingKey_ReturnsPairingPayload()
    {
        var credential = _deviceApiKeyService.CreateCredential();
        _dbContext.Devices.Add(new Device
        {
            DeviceIdentifier = "device-registered",
            Status = DeviceRegistrationStatus.Registered,
            ReportIntervalSeconds = 45,
            ApiKeyHash = credential.ApiKeyHash,
            PendingApiKeyProtected = credential.ProtectedApiKey,
            ApiKeyIssuedAtUtc = credential.CreatedAtUtc,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Discover(new DeviceDiscoveryRequest("device-registered", "1.0.0", "wifi"));

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DeviceDiscoveryResponse>(okResult.Value);

        Assert.Equal("registered_pending_credentials", response.Status);
        Assert.Equal(credential.PlainTextApiKey, response.ApiKey);
        Assert.Equal(45, response.Configuration.ReportIntervalSeconds);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}