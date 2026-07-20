using System.ComponentModel.DataAnnotations;

namespace WaterTemperature.Api.Data;

public class DevicePositionHistory
{
    [Key]
    public long Id { get; init; }

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AltitudeMeters { get; set; }
    public DateTime? GpsTimeUtc { get; set; }
    public double? SpeedKnots { get; set; }
    public double? Hdop { get; set; }
    public int? SatellitesVisible { get; set; }
    public int? SatellitesUsed { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}