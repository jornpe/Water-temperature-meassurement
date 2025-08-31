using System.ComponentModel.DataAnnotations;

namespace WaterTemperature.Api.Configuration;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    [Required]
    public string Secret { get; init; } = string.Empty;

    [Range(1, 24 * 7)] // 1 hour to 7 days
    public int TokenLifetimeHours { get; init; } = 24;
}

public class AuthenticationSettings
{
    public const string SectionName = "AuthenticationSettings";

    [Range(6, 128)]
    public int MinPasswordLength { get; init; } = 3;
}

public class CorsSettings
{
    public const string SectionName = "Cors";

    public string AllowedOrigin { get; init; } = string.Empty;
}
