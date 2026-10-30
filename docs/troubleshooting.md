# Troubleshooting Guide

This page covers common issues contributors may hit while following the Getting Started steps in the root README.

## Quick Checks First

Before deep debugging, run these from the repository root:

```bash
dotnet --version
dotnet restore Pulse.slnx
dotnet build Pulse.slnx -c Debug
dotnet test src/Pulse.Tests/Pulse.Tests.csproj -c Debug --no-build
```

If all four succeed, your local environment is usually healthy.

## Common Getting Started Issues

### 1. `dotnet` command is not recognized

Symptoms:

- Terminal prints "dotnet is not recognized" or similar.

Fix:

1. Install .NET SDK 10.0+.
2. Restart your terminal/IDE after install.
3. Re-run `dotnet --version`.

### 2. Wrong .NET SDK version

Symptoms:

- Build fails with target framework mismatch (projects target `net10.0`).

Fix:

1. Install .NET SDK 10.0.
2. Confirm with `dotnet --list-sdks`.
3. Ensure your shell is using the updated PATH, then retry build.

### 3. NuGet restore fails

Symptoms:

- `dotnet restore Pulse.slnx` fails with feed/network/auth errors.

Fix:

1. Check internet connectivity.
2. Retry restore: `dotnet restore Pulse.slnx --force`.
3. If using private feeds, verify `NuGet.config` credentials.
4. Clear local NuGet caches if needed:

```bash
dotnet nuget locals all --clear
dotnet restore Pulse.slnx
```

### 4. Build fails after restore

Symptoms:

- `dotnet build Pulse.slnx -c Debug` fails unexpectedly.

Fix:

1. Ensure restore completed successfully first.
2. Clean and rebuild:

```bash
dotnet clean Pulse.slnx
dotnet build Pulse.slnx -c Debug
```

3. If an Aspire resource holds build outputs, stop that exact AppHost through the
   IDE or Ctrl+C in its launching CLI before rebuilding. Do not delete output
   folders or kill all dotnet processes.

### 5. Tests fail locally but passed earlier

Symptoms:

- `dotnet test src/Pulse.Tests/Pulse.Tests.csproj -c Debug --no-build` fails.

Fix:

1. Run with build enabled once:

```bash
dotnet test src/Pulse.Tests/Pulse.Tests.csproj -c Debug
```

2. If failures persist, rerun a second time to rule out transient state.
3. Check whether local uncommitted changes altered behavior.

### 6. HTTPS/certificate trust problems when running apps

Symptoms:

- Browser warns about localhost certificate.
- HTTPS launch URLs fail while HTTP works.

Fix:

1. Trust the local dev certificate:

```bash
dotnet dev-certs https --trust
```

2. Restart browser and app process.

### 7. Port already in use

Symptoms:

- Startup fails with "address already in use".

Fix:

1. Stop the process already using the port.
2. Or run a different launch profile/port locally.
3. Retry `dotnet run` after freeing the port.

### 8. AppHost runs but UI endpoints do not load as expected

Symptoms:

- `Pulse.AppHost` starts, but one or more project endpoints are unavailable.

Fix:

1. Confirm the AppHost run command was used from repo root:

```bash
aspire run --project .\src\Pulse.AppHost\Pulse.AppHost.csproj
```

2. Watch startup logs for project-specific failures (for example, API project did not start).
3. Try running the failing project directly to isolate:

```bash
dotnet run --project src/Pulse.WebApi/Pulse.WebApi.csproj
```

### 9. VS Code or Visual Studio behaves differently than terminal

Symptoms:

- Build works in terminal but fails in IDE, or vice versa.

Fix:

1. Ensure IDE is using the same SDK as terminal (`dotnet --info`).
2. Restart IDE after SDK installs/updates.
3. Clear stale IDE state by reloading the workspace/solution.

## Persistent Dev Tunnel Issues

Start with the [tunnel setup and configuration guide](onboarding.md#7-persistent-anonymous-dev-tunnels).

| Symptom | Action |
| --- | --- |
| CLI cannot start | Install the Microsoft devtunnel CLI; check PATH or AppHost `DevTunnels:CliPath`. AppHost deliberately does not install it. |
| Cached login missing/expired or access denied | Run `devtunnel user login` (or `-g`) manually under the account that owns the configured tunnels. Authentication/network errors are not treated as missing definitions. |
| Invalid/duplicate ID or port | Set all four personal IDs; use 3-60 lowercase letters/digits/hyphens and distinct ports in `1..65535`. Do not include a URL or cluster suffix in `TunnelId`. |
| Local management lock conflict | Stop the other AppHost managing that ID, or choose a different personal ID. Lock files under `%LOCALAPPDATA%\Pulse\DevTunnels` are harmless when no process holds them; deleting a file is not a way to take over an active host. |
| Existing tunnel has other ports, a non-HTTP mapping, or deny rules | Use dedicated one-port Pulse tunnels, or explicitly fix their configuration while stopped. AppHost will not delete unrelated ports, reset access lists, or remove deny rules. |
| Existing active host fails verification | Ensure it forwards this machine's configured port and preserves Host/Origin (`--host-header unchanged --origin-header unchanged`). A host elsewhere cannot be safely reused; stop it yourself or choose another ID. |
| Startup/public verification timeout | Check host logs, CLI login, network access, fixed local port availability, and resource health. Increase `StartupTimeoutSeconds` for slow provisioning or initial Scalar image pulls. No local fallback occurs. |
| WebApi exits with `DirectoryNotFoundException` for an old static-assets directory | Stop AppHost, then run `dotnet clean .\Pulse.slnx` and rebuild. Stale static-web-assets manifests from an earlier project layout can reference directories that no longer exist. Do not create dummy source directories or disable static assets to mask this. |
| Aspire starts a dashboard but no resources are discoverable, or `NotifyAppHostReadyAsync` fails | Use an Aspire CLI matching this project's 13.1 release family. See the session-local installation example in onboarding. Newer global CLI discovery/stop commands may not interoperate with 13.1. Use the launching compatible CLI's Ctrl+C or your IDE for shutdown. |
| Public HTTPS request redirects to localhost, or Blazor/antiforgery fails | Confirm the endpoint has the AppHost-provided public origin and receives preserved Host/Origin through a loopback proxy. Do not enable arbitrary forwarded headers, disable certificate validation, or disable antiforgery. |
| First browser visit shows a tunnel warning | Anonymous dev tunnels can display a service anti-phishing interstitial. Continue only for your own trusted test tunnel. AppHost probes skip the warning with the supported `X-Tunnel-Skip-AntiPhishing-Page` header; anonymity does not mean the warning never appears. |
| QR opens the API or localhost | Both API QR handlers must receive `App:JoinBaseUrl` pointing to the public **WebApp** origin. The component's displayed URL uses the same configuration. Restart via AppHost to apply overrides; old printed QR codes cannot be changed retroactively. |
| Scalar cannot fetch documents or execute requests | Confirm Scalar's document/server URLs use the public API, its built-in proxy is disabled in tunnel mode, and the API receives the matching AppHost `DevTunnels:ScalarOrigin`. Only that origin gets CORS permission; do not enable wildcard/credentialed CORS or use localhost browser URLs. |
| Public connectivity drops | AppHost logs the failure, allows bounded recovery, and restarts only an exited host it owns. A prolonged outage fails the enabled run. Check network/login and restart AppHost rather than silently substituting local addresses. |
| Friendly name is unchanged but public hostname changed | The service can expire/recreate the underlying tunnel. Keep IDs and ports stable; use the actual metadata URL, and regenerate QR codes after a service-side recreation. |
| Host connection remains after a forced termination | Inspect the exact validation/developer tunnel ID and the corresponding host command before stopping that specific process. Do not stop hosts by executable name or delete persisted definitions to stop hosting. |

To disable tunnels, set AppHost `DevTunnels:Enabled=false`, remove any overriding
environment value, and restart. This does not delete persisted tunnel definitions.
### 10. Dev tunnel returns 502 or the Blazor app keeps reconnecting

For the default Aspire-managed workflow, inspect `pulse-devtunnel` and both
HTTPS port resources in the dashboard. Check their logs, local target URLs,
and public `/health` responses (HTTP 200, exactly `Healthy`). Confirm
`devtunnel user show` reports a signed-in user. Restart AppHost after trusting
the developer certificate. Do not host the same tunnel ID in another process.

For the alternative manual launcher:

1. Confirm `https://localhost:7004/health` returns exactly `Healthy`. If TLS
   validation fails, run `dotnet dev-certs https --trust`, restart Aspire, and
   retry.
2. Confirm the named tunnel has explicit HTTPS forwarding:
   `devtunnel port create <tunnel-id> -p 7004 --protocol https`.
3. Trust the local certificate with `dotnet dev-certs https --trust`, restart
   Aspire, and retry.
4. Do not expose port `7019`; that is the local Admin app.
5. Check that VPN, firewall, or managed-device policy permits WebSockets.
   Survey updates can fall back to polling, but the Blazor Server UI itself
   still needs a reliable circuit.
6. The launcher's automated public health request skips the anti-phishing page
   and requires the exact `Healthy` body. A physical browser may still show the
   one-time Microsoft Dev Tunnels trust screen; continue after reviewing it.

### 11. QR opens localhost, the API, or an old tunnel

The QR target is fixed when the API generates the image. In the default
workflow, confirm the student tunnel port is running and WebApi's
`App__JoinBaseUrl` and Admin's `StudentApp__BaseUrl` match its public URL.
An explicit `Pulse__PublicJoinBaseUrl` disables the managed tunnel; remove a
stale override and restart AppHost, then refresh the QR image.

For the alternative manual workflow, stop the current launcher and restart
through the checked-in launcher, which discovers the URL after hosting begins:

```powershell
.\tools\Start-MobileTest.ps1 -TunnelId <tunnel-id>
```

Prefer the named tunnel workflow in [Onboarding](onboarding.md) to avoid URL
changes. If the optional `PublicJoinBaseUrl` check is used, the URL must be
absolute HTTPS with no credentials, query, or fragment and must exactly match
the hosted tunnel's port 7004 `portUri`.

### 12. Named tunnel is unavailable or unexpectedly prompts for sign-in

For an Aspire-managed tunnel, check the dashboard's resolved tunnel ID and
anonymous access on both modeled HTTPS ports. Sign in with
`devtunnel user login` and restart AppHost. A one-time anti-phishing browser
screen is distinct from a sign-in requirement. The tunnel is retained after
shutdown but expires by default after 30 days without hosting.

For the alternative manual launcher:

Inspect it with `devtunnel show <tunnel-id> --json`. Before hosting, confirm
port `7004` uses protocol `https` and the `tunnel.accessControl` array contains
an entry with type `Anonymous` and scope `connect`; `portUri` appears only
after hosting starts. Recreate the tunnel with `--allow-anonymous` if
necessary. Anonymous mode exposes the student UI to anyone with the URL; stop
hosting after testing and delete obsolete tunnels with
`devtunnel delete <tunnel-id>`.

## Still Stuck?

When asking for help, include:

- Command you ran
- Full error text (not only a screenshot)
- OS and `dotnet --version`
- Whether issue happens in terminal, IDE, or both

Useful references:

- [Onboarding](onboarding.md)
- [Architecture](architecture.md)
- [API Reference](api.md)