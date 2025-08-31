using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterTemperature.Api.Models.Temperatures;

namespace WaterTemperature.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TemperaturesController : ApiControllerBase
{
    [HttpGet]
    [Authorize]
    public ActionResult<IEnumerable<TemperatureReading>> GetTemperatures()
    {
        var random = new Random();
        var readings = Enumerable.Range(0, 5).Select(i => new TemperatureReading(
            Id: i + 1,
            Sensor: $"sensor-{i + 1}",
            Celsius: Math.Round(14 + random.NextDouble() * 10, 2),
            Timestamp: DateTimeOffset.UtcNow
        ));

        return Ok(readings);
    }

    [HttpGet("{sensorId}")]
    [Authorize]
    public ActionResult<IEnumerable<TemperatureReading>> GetTemperaturesBySensor(string sensorId)
    {
        var random = new Random();
        var readings = new[] { new TemperatureReading(
            Id: 1,
            Sensor: sensorId,
            Celsius: Math.Round(14 + random.NextDouble() * 10, 2),
            Timestamp: DateTimeOffset.UtcNow
        )};

        return Ok(readings);
    }
}
