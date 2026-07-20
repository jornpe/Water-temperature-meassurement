using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Services;
using Xunit;

namespace WaterTemperature.Api.Tests.Services;

public class HomeAssistantMqttSyncServiceTests
{
    [Fact]
    public void BuildDiscoveryMessage_IncludesTemperatureLocationAndDiagnostics()
    {
        using var provider = CreateServiceProvider();
        var service = CreateService(provider);
        var device = new Device
        {
            DeviceIdentifier = "device-1",
            Name = "Pool Sensor",
            Place = "Pool",
            FirmwareVersion = "1.2.3",
        };

        var message = service.BuildDiscoveryMessage(device);
        using var document = JsonDocument.Parse(message.Payload);
        var root = document.RootElement;
        var origin = root.GetProperty("o");
        var components = root.GetProperty("cmps");

        Assert.Equal("homeassistant/device/device-1/config", message.Topic);
        Assert.True(message.Retain);
        Assert.True(components.TryGetProperty("temperature", out var temperature));
        Assert.True(components.TryGetProperty("location", out _));
        Assert.True(components.TryGetProperty("wifi_ssid", out var wifiSsid));
        Assert.Equal("WaterTemperature.Api", origin.GetProperty("name").GetString());
        Assert.Equal("sensor", temperature.GetProperty("p").GetString());
        Assert.Equal("diagnostic", wifiSsid.GetProperty("entity_category").GetString());
        Assert.True(wifiSsid.GetProperty("enabled_by_default").GetBoolean());
        Assert.Equal("Pool Sensor", root.GetProperty("device").GetProperty("name").GetString());
        Assert.False(root.TryGetProperty("components", out _));
        Assert.False(temperature.TryGetProperty("platform", out _));
    }

    [Fact]
    public void BuildDiscoveryMessage_NeverEmitsExplicitJsonNulls()
    {
        // Home Assistant's MQTT discovery schema rejects an explicit JSON null for several optional
        // fields (e.g. "suggested_display_precision" expects an int or the key to be absent entirely).
        // Dictionary<string, object?> values are not affected by JsonIgnoreCondition.WhenWritingNull,
        // so unset optional fields must be stripped before serialization instead of relying on that option.
        using var provider = CreateServiceProvider();
        var service = CreateService(provider);
        var device = new Device
        {
            DeviceIdentifier = "device-1",
            Name = "Pool Sensor",
        };

        var message = service.BuildDiscoveryMessage(device);
        using var document = JsonDocument.Parse(message.Payload);

        AssertNoNullValues(document.RootElement, "$");
    }

    private static void AssertNoNullValues(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                Assert.Fail($"Unexpected explicit JSON null at '{path}'.");
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    AssertNoNullValues(property.Value, $"{path}.{property.Name}");
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    AssertNoNullValues(item, $"{path}[{index}]");
                    index++;
                }

                break;
        }
    }

    [Fact]
    public void BuildDiscoveryMessage_UsesHomeAssistantDeviceName()
    {
        using var provider = CreateServiceProvider();
        var service = CreateService(provider);
        var device = new Device
        {
            DeviceIdentifier = "device-1",
            Name = "Pool Sensor",
            HomeAssistantDeviceName = "Pool Sensor HA",
        };

        var message = service.BuildDiscoveryMessage(device);
        using var document = JsonDocument.Parse(message.Payload);

        Assert.Equal("Pool Sensor HA", document.RootElement.GetProperty("device").GetProperty("name").GetString());
    }

    [Fact]
    public void BuildStateMessages_EmitsTemperaturePositionAndDiagnosticTopics()
    {
        using var provider = CreateServiceProvider();
        var service = CreateService(provider);
        var device = new Device
        {
            DeviceIdentifier = "device-1",
            LatestTemperatureCelsius = 21.75m,
            LatestLatitude = 52.1,
            LatestLongitude = 4.3,
            LatestHdop = 0.8,
            LatestWifiSsid = "pool-wifi",
            LatestCellularNetworkConnected = true,
        };

        var messages = service.BuildStateMessages(device);

        Assert.Contains(messages, message => message.Topic == "water-temperature/backend/status" && message.Payload == "online");
        Assert.Contains(messages, message => message.Topic == "water-temperature/devices/device-1/temperature/state" && message.Payload == "21.75");
        Assert.Contains(messages, message => message.Topic == "water-temperature/devices/device-1/wifi-ssid/state" && message.Payload == "pool-wifi");
        Assert.Contains(messages, message => message.Topic == "water-temperature/devices/device-1/cellular-network-connected/state" && message.Payload == "ON");

        var locationMessage = Assert.Single(messages.Where(message => message.Topic == "water-temperature/devices/device-1/location/attributes"));
        using var locationDocument = JsonDocument.Parse(locationMessage.Payload);
        Assert.Equal(52.1, locationDocument.RootElement.GetProperty("latitude").GetDouble());
        Assert.Equal(4.3, locationDocument.RootElement.GetProperty("longitude").GetDouble());
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<ISecretProtectionService, SecretProtectionService>();
        return services.BuildServiceProvider();
    }

    private static HomeAssistantMqttSyncService CreateService(ServiceProvider provider)
    {
        return new HomeAssistantMqttSyncService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ISecretProtectionService>(),
            provider.GetRequiredService<ILogger<HomeAssistantMqttSyncService>>());
    }
}