using Microsoft.JSInterop;
using Moq;
using Pulse.WebApp.Services;
using Xunit;

namespace Pulse.Tests.WebApp;

public class DeviceIdServiceTests
{
    private readonly Mock<IJSRuntime> _jsMock = new();
    private readonly DeviceIdService _sut;

    public DeviceIdServiceTests()
    {
        _sut = new DeviceIdService(_jsMock.Object);
    }

    [Fact]
    public async Task GetDeviceIdAsync_NoExistingId_GeneratesAndStoresNewId()
    {
        _jsMock.Setup(js => js.InvokeAsync<string?>("localStorage.getItem",
            It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsMock.Setup(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("localStorage.setItem",
            It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

        var id = await _sut.GetDeviceIdAsync();

        Assert.False(string.IsNullOrEmpty(id));
        Assert.True(Guid.TryParse(id, out _), "ID should be a valid GUID");
    }

    [Fact]
    public async Task GetDeviceIdAsync_ExistingId_ReturnsSameId()
    {
        var existingId = Guid.NewGuid().ToString();
        _jsMock.Setup(js => js.InvokeAsync<string?>("localStorage.getItem",
            It.IsAny<object[]>()))
            .ReturnsAsync(existingId);

        var id = await _sut.GetDeviceIdAsync();

        Assert.Equal(existingId, id);
    }

    [Fact]
    public async Task GetDeviceIdAsync_GeneratedId_IsValidGuidFormat()
    {
        _jsMock.Setup(js => js.InvokeAsync<string?>("localStorage.getItem",
            It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsMock.Setup(js => js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("localStorage.setItem",
            It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());

        var id = await _sut.GetDeviceIdAsync();

        Assert.Matches(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", id);
    }
}
