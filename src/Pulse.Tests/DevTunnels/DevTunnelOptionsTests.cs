using Pulse.AppHost.DevTunnels;

namespace Pulse.Tests.DevTunnels;

public class DevTunnelOptionsTests
{
    [Fact]
    public void DisabledModeDoesNotRequireTunnelConfiguration()
    {
        new DevTunnelOptions().Validate();
    }

    [Fact]
    public void EnabledModeAcceptsFourDistinctNamedMappings()
    {
        CreateOptions().Validate();
    }

    [Theory]
    [InlineData("")]
    [InlineData("UPPERCASE")]
    [InlineData("-invalid")]
    [InlineData("with spaces")]
    public void EnabledModeRejectsInvalidIds(string id)
    {
        var options = CreateOptions();
        options.Services["webapi"].TunnelId = id;
        Assert.Contains("webapi:TunnelId", Assert.Throws<InvalidOperationException>(options.Validate).Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void EnabledModeRejectsInvalidPorts(int port)
    {
        var options = CreateOptions();
        options.Services["webapi"].Port = port;
        Assert.Contains("webapi:Port", Assert.Throws<InvalidOperationException>(options.Validate).Message);
    }

    [Fact]
    public void EnabledModeRejectsDuplicateIdsAndPorts()
    {
        var options = CreateOptions();
        options.Services["webapp"].TunnelId = options.Services["webapi"].TunnelId;
        Assert.Throws<InvalidOperationException>(options.Validate);
        options = CreateOptions();
        options.Services["webapp"].Port = options.Services["webapi"].Port;
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void EnabledModeRejectsMissingServicesAndInvalidTimeouts()
    {
        var options = CreateOptions();
        options.Services.Remove("scalar");
        Assert.Contains("scalar", Assert.Throws<InvalidOperationException>(options.Validate).Message);
        options = CreateOptions();
        options.StartupTimeoutSeconds = 0;
        Assert.Throws<InvalidOperationException>(options.Validate);
        options = CreateOptions();
        options.ReconnectTimeoutSeconds = 0;
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    internal static DevTunnelOptions CreateOptions() => new()
    {
        Enabled = true,
        Services = new()
        {
            ["webapi"] = new() { TunnelId = "pulse-test-api", Port = 5062 },
            ["webadmin"] = new() { TunnelId = "pulse-test-admin", Port = 5049 },
            ["webapp"] = new() { TunnelId = "pulse-test-app", Port = 5243 },
            ["scalar"] = new() { TunnelId = "pulse-test-scalar", Port = 8088 }
        }
    };
}
