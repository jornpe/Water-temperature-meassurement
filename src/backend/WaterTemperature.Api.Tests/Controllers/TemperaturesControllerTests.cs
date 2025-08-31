using Microsoft.AspNetCore.Mvc;
using WaterTemperature.Api.Controllers;
using WaterTemperature.Api.Models.Temperatures;

namespace WaterTemperature.Api.Tests.Controllers;

public class TemperaturesControllerTests
{
    private readonly TemperaturesController _controller;

    public TemperaturesControllerTests()
    {
        _controller = new TemperaturesController();
    }

    [Fact]
    public void GetTemperatures_ReturnsOkWithTemperatureReadings()
    {
        // Act
        var result = _controller.GetTemperatures();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var readings = Assert.IsAssignableFrom<IEnumerable<TemperatureReading>>(okResult.Value);
        var readingsArray = readings.ToArray(); // Convert to array for testing
        
        Assert.Equal(5, readingsArray.Length);
        Assert.All(readingsArray, reading =>
        {
            Assert.True(reading.Id > 0);
            Assert.StartsWith("sensor-", reading.Sensor);
            Assert.InRange(reading.Celsius, 14.0, 24.0);
            Assert.True(reading.Timestamp <= DateTimeOffset.UtcNow);
            Assert.True(reading.Timestamp > DateTimeOffset.UtcNow.AddMinutes(-1)); // Recent timestamp
        });
    }

    [Fact]
    public void GetTemperaturesBySensor_ValidSensorId_ReturnsOkWithSingleReading()
    {
        // Arrange
        const string sensorId = "test-sensor-123";

        // Act
        var result = _controller.GetTemperaturesBySensor(sensorId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var readings = Assert.IsAssignableFrom<IEnumerable<TemperatureReading>>(okResult.Value);
        var readingsArray = readings.ToArray(); // Convert to array for testing
        
        Assert.Single(readingsArray);
        var reading = readingsArray[0];
        
        Assert.Equal(1, reading.Id);
        Assert.Equal(sensorId, reading.Sensor);
        Assert.InRange(reading.Celsius, 14.0, 24.0);
        Assert.True(reading.Timestamp <= DateTimeOffset.UtcNow);
        Assert.True(reading.Timestamp > DateTimeOffset.UtcNow.AddMinutes(-1)); // Recent timestamp
    }

    [Theory]
    [InlineData("sensor-1")]
    [InlineData("sensor-abc")]
    [InlineData("temp-sensor-999")]
    [InlineData("")]
    public void GetTemperaturesBySensor_DifferentSensorIds_ReturnsCorrectSensorId(string sensorId)
    {
        // Act
        var result = _controller.GetTemperaturesBySensor(sensorId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var readings = Assert.IsAssignableFrom<IEnumerable<TemperatureReading>>(okResult.Value);
        var readingsArray = readings.ToArray(); // Convert to array for testing
        
        Assert.Single(readingsArray);
        Assert.Equal(sensorId, readingsArray[0].Sensor);
    }

    [Fact]
    public void GetTemperatures_ConsistentStructure()
    {
        // Act
        var result = _controller.GetTemperatures();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var readings = Assert.IsAssignableFrom<IEnumerable<TemperatureReading>>(okResult.Value);
        var readingsArray = readings.ToArray();

        // Verify structure is consistent
        Assert.Equal(5, readingsArray.Length);
        
        // Verify each reading has expected structure
        Assert.All(readingsArray, reading =>
        {
            Assert.True(reading.Id > 0);
            Assert.StartsWith("sensor-", reading.Sensor);
            Assert.InRange(reading.Celsius, 14.0, 24.0);
            Assert.True(reading.Timestamp <= DateTimeOffset.UtcNow);
            Assert.True(reading.Timestamp > DateTimeOffset.UtcNow.AddMinutes(-1)); // Recent timestamp
        });
    }

    [Fact]
    public void GetTemperaturesBySensor_ConsistentBehavior()
    {
        // Arrange
        const string sensorId = "test-sensor";

        // Act
        var result = _controller.GetTemperaturesBySensor(sensorId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var readings = Assert.IsAssignableFrom<IEnumerable<TemperatureReading>>(okResult.Value);

        // Verify the response is consistent in structure
        Assert.Single(readings);
        var reading = readings.First();
        Assert.Equal(sensorId, reading.Sensor);
        Assert.Equal(1, reading.Id);
        Assert.InRange(reading.Celsius, 14.0, 24.0);
    }

    [Fact]
    public void GetTemperatures_ReturnsCorrectSensorNaming()
    {
        // Act
        var result = _controller.GetTemperatures();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var readings = Assert.IsAssignableFrom<IEnumerable<TemperatureReading>>(okResult.Value);
        var readingsArray = readings.ToArray(); // Convert to array for testing
        
        // Verify sensor naming pattern
        for (int i = 0; i < readingsArray.Length; i++)
        {
            Assert.Equal($"sensor-{i + 1}", readingsArray[i].Sensor);
            Assert.Equal(i + 1, readingsArray[i].Id);
        }
    }
}
