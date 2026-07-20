using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterTemperature.Api.Controllers;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Models.Auth;
using WaterTemperature.Api.Models.Settings;
using WaterTemperature.Api.Services;
using Xunit;

namespace WaterTemperature.Api.Tests.Controllers;

public class SettingsControllerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ISecretProtectionService _secretProtectionService;
    private readonly SettingsController _controller;

    public SettingsControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _secretProtectionService = new SecretProtectionService(DataProtectionProvider.Create("WaterTemperature.Api.Tests"));
        _controller = new SettingsController(_dbContext, _secretProtectionService, new FakeHomeAssistantMqttSyncService());
    }

    [Fact]
    public async Task GetHomeAssistantSettings_WhenUnset_ReturnsDefaults()
    {
        var result = await _controller.GetHomeAssistantSettings();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<HomeAssistantSettingsResponse>(okResult.Value);

        Assert.False(response.PushDataToHomeAssistant);
        Assert.Null(response.IpAddress);
        Assert.Equal(1883, response.Port);
        Assert.Null(response.Password);
    }

    [Fact]
    public async Task UpdateHomeAssistantSettings_ValidRequest_PersistsAndReturnsPassword()
    {
        var result = await _controller.UpdateHomeAssistantSettings(new UpdateHomeAssistantSettingsRequest(
            true,
            "192.168.1.5",
            1883,
            "mqtt-user",
            "mqtt-password"));

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<HomeAssistantSettingsResponse>(okResult.Value);
        var storedSettings = await _dbContext.HomeAssistantIntegrationSettings.SingleAsync();

        Assert.True(response.PushDataToHomeAssistant);
        Assert.Equal("192.168.1.5", response.IpAddress);
        Assert.Equal("mqtt-user", response.User);
        Assert.Equal("mqtt-password", response.Password);
        Assert.NotNull(storedSettings.PasswordProtected);
        Assert.NotEqual("mqtt-password", storedSettings.PasswordProtected);
    }

    [Fact]
    public async Task UpdateHomeAssistantSettings_WithEnabledButMissingHost_ReturnsBadRequest()
    {
        var result = await _controller.UpdateHomeAssistantSettings(new UpdateHomeAssistantSettingsRequest(true, null, 1883, null, null));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<MessageResponse>(badRequest.Value);

        Assert.Equal("IP address is required when Home Assistant push is enabled", response.Message);
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