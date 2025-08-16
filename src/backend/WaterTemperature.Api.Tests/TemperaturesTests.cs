using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WaterTemperature.Api.Tests;

public class TemperaturesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TemperaturesTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task Temperatures_returns_array_of_items()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/temperatures");
    Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var items = await res.Content.ReadFromJsonAsync<List<TemperatureDto>>();
    Assert.NotNull(items);
    Assert.NotEmpty(items!);
    Assert.False(string.IsNullOrWhiteSpace(items![0].Sensor));
    }

    private record TemperatureDto(int Id, string Sensor, double Celsius, DateTimeOffset Timestamp);
}
