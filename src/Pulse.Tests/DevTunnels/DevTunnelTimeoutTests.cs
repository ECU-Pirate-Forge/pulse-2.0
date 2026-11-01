using Microsoft.Extensions.Configuration;
using Pulse.AppHost.DevTunnels;

namespace Pulse.Tests.DevTunnels;

public class DevTunnelTimeoutTests
{
    [Fact]
    public void StartupDefaultIsFifteenSecondsLongerAndReconnectIsUnchanged()
    {
        var options = new DevTunnelOptions();
        Assert.Equal(60 + 15, options.StartupTimeoutSeconds);
        Assert.Equal(60, options.ReconnectTimeoutSeconds);
    }

    [Fact]
    public void StartupTimeoutCanBeOverriddenThroughConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevTunnels:StartupTimeoutSeconds"] = "120"
            })
            .Build();

        var options = configuration.GetSection("DevTunnels").Get<DevTunnelOptions>();

        Assert.NotNull(options);
        Assert.Equal(120, options.StartupTimeoutSeconds);
        Assert.Equal(60, options.ReconnectTimeoutSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EnabledModeRejectsNonPositiveStartupTimeout(int seconds)
    {
        var options = new DevTunnelOptions { Enabled = true, StartupTimeoutSeconds = seconds };
        Assert.Contains("timeouts must be positive", Assert.Throws<InvalidOperationException>(options.Validate).Message);
    }
}
