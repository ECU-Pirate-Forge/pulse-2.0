using Pulse.Application.Services;

namespace Pulse.Tests;

public class DeviceIdValidationServiceTests
{
    private readonly DeviceIdValidationService _svc = new();

    [Fact]
    public void ValidateDeviceId_ValidUUID_ReturnsSuccess()
    {
        var result = _svc.ValidateDeviceId(Guid.NewGuid().ToString());
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateDeviceId_EmptyString_ReturnsFailure()
    {
        var result = _svc.ValidateDeviceId("");
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ValidateDeviceId_Null_ReturnsFailure()
    {
        var result = _svc.ValidateDeviceId(null);
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ValidateDeviceId_MalformedString_ReturnsFailure()
    {
        var result = _svc.ValidateDeviceId("not-a-uuid");
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ValidateDeviceId_WhitespaceOnly_ReturnsFailure()
    {
        var result = _svc.ValidateDeviceId("   ");
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }
}
