using System.Text.RegularExpressions;

namespace Pulse.AppHost.DevTunnels;

public sealed class DevTunnelOptions
{
    public static readonly string[] ServiceNames = ["webapi", "webadmin", "webapp"]; // "scalar",

    public bool Enabled { get; set; }
    public string CliPath { get; set; } = "devtunnel";
    public int StartupTimeoutSeconds { get; set; } = 60;
    public int ReconnectTimeoutSeconds { get; set; } = 60;
    public Dictionary<string, DevTunnelServiceOptions> Services { get; set; } = [];

    public void Validate()
    {
        if (!Enabled)
            return;

        if (string.IsNullOrWhiteSpace(CliPath))
            throw new InvalidOperationException("DevTunnels:CliPath is required.");
        if (StartupTimeoutSeconds <= 0 || ReconnectTimeoutSeconds <= 0)
            throw new InvalidOperationException("DevTunnels startup and reconnect timeouts must be positive.");

        foreach (var (name, service) in Services)
        {
            if (!Regex.IsMatch(service.TunnelId, "^[a-z0-9][a-z0-9-]{1,58}[a-z0-9]$", RegexOptions.CultureInvariant))
                throw new InvalidOperationException(
                    $"DevTunnels:Services:{name}:TunnelId must be 3-60 lowercase letters, digits or hyphens, starting and ending with a letter or digit.");
            if (service.Port is < 1 or > 65535)
                throw new InvalidOperationException($"DevTunnels:Services:{name}:Port must be between 1 and 65535.");
        }

    }
}

public sealed class DevTunnelServiceOptions
{
    public string TunnelId { get; set; } = "";
    public int Port { get; set; }
}
