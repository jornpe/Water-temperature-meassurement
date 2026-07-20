using Microsoft.AspNetCore.DataProtection;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Services;
using Xunit;

namespace WaterTemperature.Api.Tests.UnitTests;

public class DeviceApiKeyServiceTests
{
    private readonly IDeviceApiKeyService _service = new DeviceApiKeyService(DataProtectionProvider.Create("WaterTemperature.Api.Tests"));

    [Fact]
    public void CreateCredential_VerifyAndReadPending_ReturnExpectedValues()
    {
        var credential = _service.CreateCredential();
        var device = new Device { PendingApiKeyProtected = credential.ProtectedApiKey };

        Assert.False(string.IsNullOrWhiteSpace(credential.PlainTextApiKey));
        Assert.True(_service.Verify(credential.PlainTextApiKey, credential.ApiKeyHash));
        Assert.Equal(credential.PlainTextApiKey, _service.ReadPendingApiKey(device));
    }

    [Fact]
    public void CreateCredential_WhenRegenerated_OldKeyDoesNotMatchNewHash()
    {
        var first = _service.CreateCredential();
        var second = _service.CreateCredential();

        Assert.True(_service.Verify(first.PlainTextApiKey, first.ApiKeyHash));
        Assert.True(_service.Verify(second.PlainTextApiKey, second.ApiKeyHash));
        Assert.False(_service.Verify(first.PlainTextApiKey, second.ApiKeyHash));
    }
}