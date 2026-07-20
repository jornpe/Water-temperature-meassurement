using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using WaterTemperature.Api.Data;

namespace WaterTemperature.Api.Services;

public static class DeviceAuthenticationHeaders
{
    public const string ApiKeyHeaderName = "X-Api-Key";
}

public interface IDeviceApiKeyService
{
    DeviceCredentialMaterial CreateCredential();
    bool Verify(string apiKey, string? apiKeyHash);
    string? ReadPendingApiKey(Device device);
    void ClearPendingApiKey(Device device);
}

public sealed record DeviceCredentialMaterial(
    string PlainTextApiKey,
    string ApiKeyHash,
    string ProtectedApiKey,
    DateTime CreatedAtUtc);

public class DeviceApiKeyService(IDataProtectionProvider dataProtectionProvider) : IDeviceApiKeyService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("device-api-keys");

    public DeviceCredentialMaterial CreateCredential()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var plainTextApiKey = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var createdAtUtc = DateTime.UtcNow;

        return new DeviceCredentialMaterial(
            plainTextApiKey,
            BCrypt.Net.BCrypt.HashPassword(plainTextApiKey),
            _protector.Protect(plainTextApiKey),
            createdAtUtc);
    }

    public bool Verify(string apiKey, string? apiKeyHash)
    {
        return !string.IsNullOrWhiteSpace(apiKeyHash) && BCrypt.Net.BCrypt.Verify(apiKey, apiKeyHash);
    }

    public string? ReadPendingApiKey(Device device)
    {
        if (string.IsNullOrWhiteSpace(device.PendingApiKeyProtected))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(device.PendingApiKeyProtected);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public void ClearPendingApiKey(Device device)
    {
        device.PendingApiKeyProtected = null;
    }
}