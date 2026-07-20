namespace WaterTemperature.Api.Models.Settings;

public record HomeAssistantSettingsResponse(
    bool PushDataToHomeAssistant,
    string? IpAddress,
    int Port,
    string? User,
    string? Password,
    DateTime? UpdatedAtUtc);

public record UpdateHomeAssistantSettingsRequest(
    bool PushDataToHomeAssistant,
    string? IpAddress,
    int Port,
    string? User,
    string? Password);