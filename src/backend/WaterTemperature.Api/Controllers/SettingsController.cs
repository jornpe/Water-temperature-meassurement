using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Models.Auth;
using WaterTemperature.Api.Models.Settings;
using WaterTemperature.Api.Services;

namespace WaterTemperature.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/settings")]
public class SettingsController(
    AppDbContext dbContext,
    ISecretProtectionService secretProtectionService,
    IHomeAssistantMqttSyncService homeAssistantMqttSyncService) : ApiControllerBase
{
    [HttpGet("home-assistant")]
    public async Task<ActionResult<HomeAssistantSettingsResponse>> GetHomeAssistantSettings()
    {
        var settings = await dbContext.HomeAssistantIntegrationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == HomeAssistantIntegrationSettings.SingletonId);

        return Ok(MapResponse(settings, secretProtectionService));
    }

    [HttpPut("home-assistant")]
    public async Task<ActionResult<HomeAssistantSettingsResponse>> UpdateHomeAssistantSettings([FromBody] UpdateHomeAssistantSettingsRequest request)
    {
        if (request.PushDataToHomeAssistant)
        {
            if (string.IsNullOrWhiteSpace(request.IpAddress))
            {
                return BadRequest(new MessageResponse("IP address is required when Home Assistant push is enabled"));
            }

            if (request.Port <= 0 || request.Port > 65535)
            {
                return BadRequest(new MessageResponse("Port must be between 1 and 65535 when Home Assistant push is enabled"));
            }
        }

        var settings = await dbContext.HomeAssistantIntegrationSettings
            .SingleOrDefaultAsync(item => item.Id == HomeAssistantIntegrationSettings.SingletonId);

        settings ??= new HomeAssistantIntegrationSettings();

        settings.Enabled = request.PushDataToHomeAssistant;
        settings.Host = NormalizeOptional(request.IpAddress);
        settings.Port = request.Port > 0 ? request.Port : 1883;
        settings.Username = NormalizeOptional(request.User);

        // Only touch the stored password when a new, non-blank value is supplied.
        // The client always resends whatever is currently in the password field, which is
        // blank if the previous value couldn't be decrypted (or was never set) - overwriting
        // unconditionally would silently wipe out a previously saved password.
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            settings.PasswordProtected = secretProtectionService.Protect(request.Password);
        }

        settings.UpdatedAtUtc = DateTime.UtcNow;

        if (dbContext.Entry(settings).State == EntityState.Detached)
        {
            dbContext.HomeAssistantIntegrationSettings.Add(settings);
        }

        await dbContext.SaveChangesAsync();

        await homeAssistantMqttSyncService.RefreshAllAsync();

        return Ok(MapResponse(settings, secretProtectionService));
    }

    private static HomeAssistantSettingsResponse MapResponse(HomeAssistantIntegrationSettings? settings, ISecretProtectionService secretProtectionService)
    {
        if (settings is null)
        {
            return new HomeAssistantSettingsResponse(false, null, 1883, null, null, null);
        }

        return new HomeAssistantSettingsResponse(
            settings.Enabled,
            settings.Host,
            settings.Port,
            settings.Username,
            secretProtectionService.TryUnprotect(settings.PasswordProtected),
            settings.UpdatedAtUtc);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}