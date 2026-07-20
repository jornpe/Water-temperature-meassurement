using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace WaterTemperature.Api.Services;

public interface ISecretProtectionService
{
    string Protect(string value);
    string? TryUnprotect(string? protectedValue);
}

public class SecretProtectionService(IDataProtectionProvider dataProtectionProvider) : ISecretProtectionService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("application-secrets");

    public string Protect(string value)
    {
        return _protector.Protect(value);
    }

    public string? TryUnprotect(string? protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}