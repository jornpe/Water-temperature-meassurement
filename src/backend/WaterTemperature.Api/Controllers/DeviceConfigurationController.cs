using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Models.Auth;
using WaterTemperature.Api.Models.Devices;
using WaterTemperature.Api.Services;

namespace WaterTemperature.Api.Controllers;

[ApiController]
[Route("api/devices")]
public class DeviceConfigurationController(
    AppDbContext dbContext,
    IDeviceApiKeyService deviceApiKeyService,
    IHomeAssistantMqttSyncService homeAssistantMqttSyncService) : ApiControllerBase
{
    [HttpPost("{deviceId}/configuration")]
    public async Task<ActionResult<DeviceConfigurationSyncResponse>> Sync(
        string deviceId,
        [FromBody] DeviceConfigurationSyncRequest request)
    {
        if (!Request.Headers.TryGetValue(DeviceAuthenticationHeaders.ApiKeyHeaderName, out var apiKeyValues))
        {
            return Unauthorized();
        }

        var device = await dbContext.Devices.SingleOrDefaultAsync(item => item.DeviceIdentifier == deviceId);
        if (device is null || !deviceApiKeyService.Verify(apiKeyValues.ToString(), device.ApiKeyHash))
        {
            return Unauthorized();
        }

        if (request.AppliedConfigurationVersion < 0)
        {
            return BadRequest(new MessageResponse("Applied configuration version cannot be negative"));
        }

        if (request.AppliedReportIntervalSeconds <= 0)
        {
            return BadRequest(new MessageResponse("Applied report interval must be greater than zero"));
        }

        if (request.SyncAttempt is < 1 or > DeviceConfigurationSyncPolicy.MaximumAttempts)
        {
            return BadRequest(new MessageResponse(
                $"Configuration sync attempt must be between 1 and {DeviceConfigurationSyncPolicy.MaximumAttempts}"));
        }

        var now = DateTime.UtcNow;
        var valuesMatchDesired =
            request.AppliedConfigurationVersion == device.DesiredConfigurationVersion
            && request.AppliedReportIntervalSeconds == device.ReportIntervalSeconds;
        var isSynchronized = request.StorageVerified && valuesMatchDesired;

        string? syncError = null;
        if (!isSynchronized)
        {
            syncError = !request.StorageVerified
                ? "The device could not verify that its stored configuration matches its in-memory configuration."
                : $"The device reported configuration version {request.AppliedConfigurationVersion} with interval "
                    + $"{request.AppliedReportIntervalSeconds} seconds; desired version is "
                    + $"{device.DesiredConfigurationVersion} with interval {device.ReportIntervalSeconds} seconds.";
        }

        device.ReportedConfigurationVersion = request.AppliedConfigurationVersion;
        device.ReportedReportIntervalSeconds = request.AppliedReportIntervalSeconds;
        device.RuntimeConfigurationReportedAtUtc = now;
        device.ConfigurationSyncAttemptCount = request.SyncAttempt;
        device.ConfigurationSyncStatus = isSynchronized
            ? DeviceConfigurationSyncStatus.Synchronized
            : request.IsConfirmation && request.SyncAttempt == DeviceConfigurationSyncPolicy.MaximumAttempts
                ? DeviceConfigurationSyncStatus.Failed
                : DeviceConfigurationSyncStatus.Pending;
        device.ConfigurationSyncError = device.ConfigurationSyncStatus == DeviceConfigurationSyncStatus.Failed
            ? syncError
            : null;
        device.ConfigurationSyncStatusUpdatedAtUtc = now;
        device.LastSeenAtUtc = now;
        device.ApiKeyLastUsedAtUtc = now;
        deviceApiKeyService.ClearPendingApiKey(device);

        await dbContext.SaveChangesAsync();

        if (device.PushToHomeAssistant)
        {
            await homeAssistantMqttSyncService.PublishDeviceStateAsync(device.Id);
        }

        return Ok(new DeviceConfigurationSyncResponse(
            device.DeviceIdentifier,
            new DeviceConfigurationResponse(device.ReportIntervalSeconds, device.DesiredConfigurationVersion),
            isSynchronized,
            DeviceConfigurationSyncPolicy.MaximumAttempts,
            now));
    }
}
