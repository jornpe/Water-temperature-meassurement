using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Models.Auth;
using WaterTemperature.Api.Models.Devices;
using WaterTemperature.Api.Services;

namespace WaterTemperature.Api.Controllers;

[ApiController]
[Route("api/devices")]
public class DeviceDiscoveryController(AppDbContext dbContext, IDeviceApiKeyService deviceApiKeyService) : ApiControllerBase
{
    [HttpPost("discover")]
    public async Task<ActionResult<DeviceDiscoveryResponse>> Discover([FromBody] DeviceDiscoveryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return BadRequest(new MessageResponse("Device ID is required"));
        }

        var now = DateTime.UtcNow;
        var normalizedDeviceId = request.DeviceId.Trim();

        var device = await dbContext.Devices.SingleOrDefaultAsync(item => item.DeviceIdentifier == normalizedDeviceId);
        if (device is null)
        {
            device = new Device
            {
                DeviceIdentifier = normalizedDeviceId,
                FirmwareVersion = request.FirmwareVersion?.Trim(),
                LastDiscoveryTransport = request.NetworkTransport?.Trim(),
                LastDiscoveredAtUtc = now,
                LastSeenAtUtc = now,
            };

            dbContext.Devices.Add(device);
        }
        else
        {
            device.FirmwareVersion = string.IsNullOrWhiteSpace(request.FirmwareVersion)
                ? device.FirmwareVersion
                : request.FirmwareVersion.Trim();
            device.LastDiscoveryTransport = string.IsNullOrWhiteSpace(request.NetworkTransport)
                ? device.LastDiscoveryTransport
                : request.NetworkTransport.Trim();
            device.LastDiscoveredAtUtc = now;
            device.LastSeenAtUtc = now;
        }

        await dbContext.SaveChangesAsync();

        var pendingApiKey = deviceApiKeyService.ReadPendingApiKey(device);
        var status = device.Status switch
        {
            DeviceRegistrationStatus.Unregistered => "unregistered",
            DeviceRegistrationStatus.Registered when !string.IsNullOrWhiteSpace(pendingApiKey) => "registered_pending_credentials",
            _ => "registered"
        };

        return Ok(new DeviceDiscoveryResponse(
            device.DeviceIdentifier,
            status,
            new DeviceConfigurationResponse(device.ReportIntervalSeconds, device.DesiredConfigurationVersion),
            pendingApiKey,
            device.ApiKeyIssuedAtUtc));
    }
}