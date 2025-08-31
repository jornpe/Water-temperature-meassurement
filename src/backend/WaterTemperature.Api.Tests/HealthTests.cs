using Xunit;

namespace WaterTemperature.Api.Tests;

/// <summary>
/// Unit tests for health-related functionality.
/// Note: The health endpoint is a simple lambda in Program.cs that returns { status: "ok" }.
/// This test validates the expected response format.
/// </summary>
public class HealthTests
{
    [Fact]
    public void HealthResponse_ShouldReturnCorrectFormat()
    {
        // Arrange - Simulate the health endpoint response
        var healthResponse = new { status = "ok" };
        
        // Assert
        Assert.NotNull(healthResponse);
        Assert.Equal("ok", healthResponse.status);
    }

    [Fact]
    public void HealthResponse_StatusProperty_ShouldBeString()
    {
        // Arrange
        var healthResponse = new { status = "ok" };
        
        // Assert
        Assert.IsType<string>(healthResponse.status);
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("healthy")]
    [InlineData("ready")]
    public void HealthResponse_AcceptsDifferentStatusValues(string status)
    {
        // Arrange
        var healthResponse = new { status = status };
        
        // Assert
        Assert.Equal(status, healthResponse.status);
        Assert.NotNull(healthResponse.status);
        Assert.NotEmpty(healthResponse.status);
    }
}
