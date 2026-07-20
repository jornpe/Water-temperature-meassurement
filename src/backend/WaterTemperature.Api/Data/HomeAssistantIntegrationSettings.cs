using System.ComponentModel.DataAnnotations;

namespace WaterTemperature.Api.Data;

public class HomeAssistantIntegrationSettings
{
    public const int SingletonId = 1;

    [Key]
    public int Id { get; set; } = SingletonId;

    public bool Enabled { get; set; }

    [MaxLength(255)]
    public string? Host { get; set; }

    public int Port { get; set; } = 1883;

    [MaxLength(255)]
    public string? Username { get; set; }

    [MaxLength(4096)]
    public string? PasswordProtected { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}