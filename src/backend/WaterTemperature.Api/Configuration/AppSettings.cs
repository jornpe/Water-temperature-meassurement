using System.ComponentModel.DataAnnotations;

namespace WaterTemperature.Api.Configuration;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    [Required]
    public string Secret { get; set; } = string.Empty;

    [Range(1, 24 * 7)] // 1 hour to 7 days
    public int TokenLifetimeHours { get; set; } = 24;

    [Range(0, 300)] // 0 to 5 minutes
    public int ClockSkewSeconds { get; set; } = 30;
}

public class AuthenticationSettings
{
    public const string SectionName = "AuthenticationSettings";

    [Range(6, 128)]
    public int MinPasswordLength { get; set; } = 8;

    [Range(10, 15)]
    public int BcryptWorkFactor { get; set; } = 12;

    [Range(0, 5000)]
    public int LoginDelayMs { get; set; } = 100;
}

public class DatabaseSettings
{
    public const string SectionName = "Database";

    public bool AutoCreate { get; set; } = true;
}

public class CorsSettings
{
    public const string SectionName = "Cors";

    public string AllowedOrigins { get; set; } = string.Empty;
}
