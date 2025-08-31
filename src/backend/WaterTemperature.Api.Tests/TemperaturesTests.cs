using System.Net;
using System.Net.Http.Json;
using WaterTemperature.Api.Models.Temperatures;
using WaterTemperature.Api.Tests.Infrastructure;

namespace WaterTemperature.Api.Tests;

/// <summary>
/// Integration tests for the temperatures controller.
/// </summary>
public class TemperaturesTests : IntegrationTestBase
{
    public TemperaturesTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetTemperatures_WithAuthentication_ReturnsArrayOfItems()
    {
        // Act
        var response = await AuthenticatedClient.GetAsync("/api/temperatures");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var temperatures = await DeserializeResponseAsync<List<TemperatureReading>>(response);
        Assert.NotNull(temperatures);
        Assert.NotEmpty(temperatures);
        Assert.True(temperatures.Count >= 1);
        
        // Verify structure of first item
        var firstReading = temperatures.First();
        Assert.True(firstReading.Id > 0);
        Assert.False(string.IsNullOrWhiteSpace(firstReading.Sensor));
        Assert.True(firstReading.Celsius > 0);
        Assert.True(firstReading.Timestamp <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetTemperatures_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/api/temperatures");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTemperaturesBySensor_WithAuthentication_ReturnsSpecificSensorData()
    {
        // Arrange
        const string sensorId = "test-sensor-123";
        
        // Act
        var response = await AuthenticatedClient.GetAsync($"/api/temperatures/{sensorId}");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var temperatures = await DeserializeResponseAsync<List<TemperatureReading>>(response);
        Assert.NotNull(temperatures);
        Assert.NotEmpty(temperatures);
        
        // Verify all readings are from the requested sensor
        Assert.All(temperatures, temp => Assert.Equal(sensorId, temp.Sensor));
    }

    [Fact]
    public async Task GetTemperaturesBySensor_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/api/temperatures/sensor1");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTemperatures_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");
        
        // Act
        var response = await client.GetAsync("/api/temperatures");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTemperatures_ReturnsValidTemperatureRange()
    {
        // Act
        var response = await AuthenticatedClient.GetAsync("/api/temperatures");
        var temperatures = await DeserializeResponseAsync<List<TemperatureReading>>(response);
        
        // Assert
        Assert.NotNull(temperatures);
        Assert.All(temperatures, temp => 
        {
            // Temperature should be in a reasonable range (assuming water temperature)
            Assert.True(temp.Celsius >= 0 && temp.Celsius <= 100, 
                $"Temperature {temp.Celsius}°C is outside expected range for water");
        });
    }

    [Fact]
    public async Task GetTemperatures_ReturnsConsistentStructure()
    {
        // Act
        var response = await AuthenticatedClient.GetAsync("/api/temperatures");
        var temperatures = await DeserializeResponseAsync<List<TemperatureReading>>(response);
        
        // Assert
        Assert.NotNull(temperatures);
        Assert.All(temperatures, temp => 
        {
            Assert.True(temp.Id > 0);
            Assert.False(string.IsNullOrWhiteSpace(temp.Sensor));
            Assert.StartsWith("sensor-", temp.Sensor);
            Assert.True(temp.Timestamp <= DateTimeOffset.UtcNow);
            Assert.True(temp.Timestamp > DateTimeOffset.UtcNow.AddMinutes(-1)); // Should be recent
        });
    }
}
