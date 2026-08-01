using System.ComponentModel.DataAnnotations;
using WaterTemperature.Api.Models.Devices;

namespace WaterTemperature.Api.Data;

public class DeviceBatteryHistory
{
    [Key]
    public long Id { get; init; }

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public bool ModemReadingValid { get; set; }
    public int ChargeState { get; set; }
    public BatteryState BatteryState { get; set; }
    public int ModemMillivolts { get; set; }
    public float AdcVoltage { get; set; }

    public DateTime RecordedAtUtc { get; set; }
}
