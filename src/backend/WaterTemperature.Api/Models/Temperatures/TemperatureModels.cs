namespace WaterTemperature.Api.Models.Temperatures;

/// <summary>
/// Represents a temperature reading from a sensor.
/// </summary>
public record TemperatureReading(
    int Id,
    string Sensor,
    double Celsius,
    DateTimeOffset Timestamp);

/// <summary>
/// Response containing a collection of temperature readings.
/// </summary>
public record TemperatureReadingsResponse(IEnumerable<TemperatureReading> Readings);
