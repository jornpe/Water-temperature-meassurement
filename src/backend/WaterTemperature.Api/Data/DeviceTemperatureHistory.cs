using System.ComponentModel.DataAnnotations;

namespace WaterTemperature.Api.Data;

public class DeviceTemperatureHistory
{
    [Key]
    public long Id { get; init; }

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public decimal TemperatureCelsius { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}