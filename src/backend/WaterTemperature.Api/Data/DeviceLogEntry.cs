using System.ComponentModel.DataAnnotations;

namespace WaterTemperature.Api.Data;

public class DeviceLogEntry
{
    [Key]
    public int Id { get; init; }

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public long SequenceNumber { get; set; }

    [MaxLength(32)]
    public string? Level { get; set; }

    [Required]
    [MaxLength(2048)]
    public string Message { get; set; } = string.Empty;

    public DateTime? DeviceTimestampUtc { get; set; }
    public long? DeviceUptimeMs { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}