using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

public static class DevTunnelExtensions
{
    public static WebApplication UsePulseDevTunnels(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("DevTunnels:Enabled"))
        {
            app.UseHttpsRedirection();
            return app;
        }

        if (!app.Environment.IsDevelopment())
            throw new InvalidOperationException("Anonymous Pulse dev tunnels are only supported in Development.");
        if (!Uri.TryCreate(app.Configuration["DevTunnels:PublicUrl"], UriKind.Absolute, out var origin) ||
            origin.Scheme != "https" || !origin.Host.EndsWith(".devtunnels.ms", StringComparison.OrdinalIgnoreCase) ||
            origin.AbsolutePath != "/" || !origin.IsDefaultPort || !string.IsNullOrEmpty(origin.UserInfo) ||
            !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
            throw new InvalidOperationException("DevTunnels:PublicUrl must be the HTTPS origin of this application's dev tunnel.");
        var instanceId = app.Configuration["DevTunnels:InstanceId"];
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new InvalidOperationException("DevTunnels:InstanceId must be supplied by AppHost.");

        app.Use(async (context, next) =>
        {
            if (string.Equals(context.Request.Host.Host, origin.Host, StringComparison.OrdinalIgnoreCase))
            {
                var peer = context.Connection.RemoteIpAddress;
                if (peer is null || !IPAddress.IsLoopback(peer.IsIPv4MappedToIPv6 ? peer.MapToIPv4() : peer))
                {
                    app.Logger.LogWarning("Rejected dev tunnel request from non-loopback peer {Peer}", peer);
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                // The tunnel terminates TLS and preserves Host. Use our configured origin, not untrusted forwarded headers.
                context.Request.Scheme = origin.Scheme;
                context.Request.Host = origin.IsDefaultPort ? new HostString(origin.Host) : HostString.FromUriComponent(origin);
            }
            await next(context);
        });
        app.MapGet("/.well-known/pulse-dev-tunnel", (HttpRequest request) => Results.Json(new
        {
            instance = instanceId,
            service = app.Configuration["DevTunnels:ServiceName"],
            origin = $"{request.Scheme}://{request.Host}"
        }));
        return app;
    }
}
