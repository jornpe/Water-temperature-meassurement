using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WaterTemperature.Api.Tests;

public class HealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var client = _factory.CreateClient();
        var res = await client.GetFromJsonAsync<Dictionary<string, string>>("/health");
    Assert.NotNull(res);
    Assert.True(res!.ContainsKey("status"));
    Assert.Equal("ok", res["status"]);
    }
}
