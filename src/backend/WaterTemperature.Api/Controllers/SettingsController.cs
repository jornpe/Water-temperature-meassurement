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
    IHomeAssistantMqttSyncService homeAssistantMqttSyncService) : ApiControllerBase
{
    [HttpGet("home-assistant")]
    public async Task<ActionResult<HomeAssistantSettingsResponse>> GetHomeAssistantSettings()
    {
        var settings = await dbContext.HomeAssistantIntegrationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == HomeAssistantIntegrationSettings.SingletonId);

        return Ok(ToResponse(settings));
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
        settings.Password = NormalizeOptional(request.Password);
        settings.UpdatedAtUtc = DateTime.UtcNow;

        if (dbContext.Entry(settings).State == EntityState.Detached)
        {
            dbContext.HomeAssistantIntegrationSettings.Add(settings);
        }

        await dbContext.SaveChangesAsync();

        await homeAssistantMqttSyncService.RefreshAllAsync();

        return Ok(ToResponse(settings));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static HomeAssistantSettingsResponse ToResponse(HomeAssistantIntegrationSettings? settings)
    {
        return new HomeAssistantSettingsResponse(
            settings?.Enabled ?? false,
            settings?.Host,
            settings?.Port ?? 1883,
            settings?.Username,
            settings?.Password,
            settings?.UpdatedAtUtc);
    }
}
