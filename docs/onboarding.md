# Pulse Onboarding Guide

This guide helps new contributors get the Pulse solution running locally and explains the current dependency injection setup across services.

## 1. What This Solution Contains

Pulse is organized as a multi-project .NET solution with Aspire orchestration:

- Pulse.AppHost: local orchestrator for running solution services together.
- Pulse.WebApi: minimal API backend for question data.
- Pulse.WebApp: student-facing Blazor Server app.
- Pulse.Admin: instructor/admin-facing Blazor Server app.
- Pulse.MigrationManager: background worker project.
- Pulse.Shared: shared models and repositories.
- Pulse.Tests: xUnit test project.

Solution file: Pulse.slnx

## 2. Prerequisites

- .NET SDK 10.0 (the projects target net10.0).
- Git.
- A local development certificate trusted for HTTPS endpoints if you plan to run HTTPS launch profiles.

Optional but recommended:

- Visual Studio 2022 or VS Code with C# Dev Kit.
- Microsoft Dev Tunnels CLI for real-device testing (`winget install Microsoft.devtunnel`).

## 3. First-Time Setup

From the repository root:

1. Restore packages
	 dotnet restore Pulse.slnx
2. Build the solution
	 dotnet build Pulse.slnx
3. Run tests
	 dotnet test src/Pulse.Tests/Pulse.Tests.csproj

## 4. Running the Solution

Recommended path (full local experience): run AppHost

- `aspire run --project .\src\Pulse.AppHost\Pulse.AppHost.csproj` (Aspire CLI 13.1.x)

This launches the distributed app and Aspire dashboard endpoints from the AppHost launch profile.

Run individual projects if needed:

- Web API: dotnet run --project src/Pulse.WebApi/Pulse.WebApi.csproj
- Student app: dotnet run --project src/Pulse.WebApp/Pulse.WebApp.csproj
- Admin app: dotnet run --project src/Pulse.Admin/Pulse.Admin.csproj

Current development URLs from launch settings:

- Web API: http://localhost:5062 and https://localhost:7081
- Web App mobile-test endpoint: https://localhost:7004
- Admin App: http://pulse-admin.dev.localhost:5049 and https://pulse-admin.dev.localhost:7019

These remain the standalone/local defaults. When dev tunnels are enabled, use the
public links printed by AppHost and shown in the Aspire dashboard instead.

See [persistent anonymous dev tunnels](#7-persistent-anonymous-dev-tunnels) for remote-device testing.
### Mobile testing with a public dev tunnel

AppHost uses `Aspire.Hosting.DevTunnels` to host `pulse-devtunnel` automatically
in run mode when `Pulse:PublicJoinBaseUrl` is not supplied. It exposes only
the HTTPS endpoints of `webapi` and `webapp` as separate public URLs. Never
tunnel Admin or put developer credentials in configuration. Anonymous access
exposes API management routes as well as student routes; use synthetic test
data, keep instructor codes private, and stop hosting after testing.

#### Recommended: Aspire-managed persistent tunnel

Sign in once and start AppHost from the repository root:

```powershell
devtunnel user login
aspire start --apphost .\src\Pulse.AppHost\Pulse.AppHost.csproj
```

The default tunnel ID is derived from `pulse-devtunnel` and Aspire's AppHost
identity, and is reused on subsequent starts from the same checkout. For a
custom stable ID, set `Pulse__DevTunnelId` before starting (lowercase letters,
numbers, and hyphens; 1-58 characters; start/end with a letter or number).
Choose a unique ID belonging to this AppHost: Aspire manages its ports and
removes ports not modeled here. Do not reuse a tunnel belonging to another app.
Do not run two hosts against the same tunnel ID simultaneously.

AppHost obtains the public student HTTPS endpoint through
`tunnel.GetEndpoint(webapp, "https")`. Aspire allocates this endpoint before
starting consumers; it supplies `App__JoinBaseUrl` to WebApi for both QR
endpoints and `StudentApp__BaseUrl` to Admin for the displayed join link.
Internal service calls still use local Aspire service discovery. The API
public URL is for API clients, not student QR navigation. Tunnels are not
included in published deployments.

Verify in the Aspire dashboard:

1. `pulse-devtunnel` and both HTTPS port resources are running/healthy.
2. Each port shows its public tunnel URL and the correct local target.
3. Both public `/health` URLs return HTTP 200 with exactly `Healthy` without
   credentials. For automated probes, send
   `X-Tunnel-Skip-AntiPhishing-Page: true`; a trust/interstitial page is not a
   successful health check.
4. Create and activate a test survey in Admin. Its displayed link and decoded
   QR must both use the student tunnel host with `/join/{code}`.
5. Open that link on a phone and submit a response. A one-time Dev Tunnels
   trust page may appear; review it before continuing.
6. Stop AppHost from the editor or with
   `aspire stop --apphost .\src\Pulse.AppHost\Pulse.AppHost.csproj`.
   Hosting stops, but the tunnel is retained. Restart and verify the same public
   URLs. By default a tunnel expires after 30 days without hosting; persistence
   does not mean an always-running service or permanent reservation.

#### Alternative: manually hosted reusable named tunnel

A named tunnel keeps the same public URL while it exists, which lets Aspire
receive the URL before QR codes are generated. This student-only legacy
launcher sets `Pulse:PublicJoinBaseUrl`, disabling the Aspire-managed tunnel
so the two hosts do not compete. Create it once:

```powershell
devtunnel user login
devtunnel create pulse-student-yourname --allow-anonymous
devtunnel port create pulse-student-yourname -p 7004 --protocol https
```

Run from the repository root:

```powershell
.\tools\Start-MobileTest.ps1 -TunnelId pulse-student-yourname
```

`TunnelId` is mandatory. The launcher validates port 7004, HTTPS, and
`Anonymous` `connect` access before hosting. It starts the named tunnel first,
polls `devtunnel show --json` until the hosted port exposes `portUri`, and uses
that discovered URL for Aspire and QR generation. An optional
`-PublicJoinBaseUrl https://...` may be supplied only as an additional
exact-match check against the discovered `portUri`.

The launcher also refuses to reuse an already-listening local port, waits for
the new student app's `https://localhost:7004/health` response, and verifies the
discovered public `/health` response is exactly `Healthy` without credentials.
Its public probe sends `X-Tunnel-Skip-AntiPhishing-Page: true` so a tunnel
interstitial cannot be mistaken for application readiness. This prevents an
old process or unrelated tunnel from making a stale QR URL appear valid.

The launcher sets `Pulse__PublicJoinBaseUrl` from the discovered `portUri` only
for its Aspire process.
AppHost forwards it as `App__JoinBaseUrl` to the API (QR encoding) and
`StudentApp__BaseUrl` to Admin (displayed join URL). The script rejects
non-HTTPS URLs. It then hosts the named tunnel and cleans up Aspire when the
tunnel exits. It attempts graceful shutdown first, falls back to stopping only
the exact tracked process IDs, and checks whether port 7004 was released. Do
not store the generated URL or any login token in source.

The setup deliberately does not support an ephemeral/random tunnel: its URL
cannot be safely known before Aspire configures QR generation. The explicit
HTTPS port on the named tunnel also avoids protocol auto-detection forwarding
HTTP to the local TLS endpoint and returning 502. Dev tunnels support HTTPS and
WebSockets, so the Blazor Server circuit and survey SignalR hub can connect;
survey polling remains the fallback and trivia uses polling.

#### Validate and clean up

1. Create and activate a survey in Admin.
2. Confirm the displayed join URL begins with the configured public URL.
3. Scan its QR with a physical phone; verify the browser opens
   `https://<public-host>/join/<code>` and not localhost, the API, or Admin.
   A one-time Microsoft Dev Tunnels anti-phishing/trust screen can still appear
   for a physical browser; review it and continue to the student page.
4. Submit the survey, then run the relevant rows in the
   [mobile acceptance matrix](mobile-acceptance.md).
5. Press Ctrl+C in the launcher window. It stops the exact tunnel and Aspire
   process IDs it started, restores its environment, and checks port release.
   If an Aspire child does not exit with its tracked parent, the script warns
   that port 7004 remains occupied; close that process from its Aspire
   dashboard/terminal before restarting rather than killing processes by name.
6. Delete a named tunnel when it is no longer needed:
   `devtunnel delete pulse-student-yourname`.

## 5. Current Dependency Injection Snapshot

This section reflects current registrations in code.

### Pulse.WebApi

In Program.cs, the API registers core services through AddPulseWebApiCoreServices(connectionString).

The extension currently registers:

- LiteDatabase as Singleton
	- Registration: AddSingleton<LiteDatabase>(_ => new LiteDatabase(connectionString))
- IJoinCodeGenerator to JoinCodeGenerator as Singleton
	- Registration: AddSingleton<IJoinCodeGenerator, JoinCodeGenerator>()
- ISessionRepository to SessionRepository as Singleton
	- Registration: AddSingleton<ISessionRepository, SessionRepository>()
- QuestionRepository as Singleton
	- Registration: AddSingleton<QuestionRepository>()
- QuestionService as Singleton
	- Registration: AddSingleton<QuestionService>()
- IQuestionBankRepository to QuestionBankRepository as Singleton
	- Registration: AddSingleton<IQuestionBankRepository, QuestionBankRepository>()
- IResponseRepository to ResponseRepository as Singleton
	- Registration: AddSingleton<IResponseRepository, ResponseRepository>()
- DeviceIdValidationService as Singleton
	- Registration: AddSingleton<DeviceIdValidationService>()

Additional framework registrations in WebApi:

- AddOpenApi()
- Default service provider validation on build is enabled in development.

### Pulse.WebApp

Current registrations:

- AddRazorComponents().AddInteractiveServerComponents()
- AddMudServices()

### Pulse.Admin

Current registrations:

- AddRazorComponents().AddInteractiveServerComponents()
- AddMudServices()

### Pulse.MigrationManager

Current registrations:

- AddHostedService<Worker>()

## 6. Testing Notes

Pulse.Tests uses xUnit v3.

Current smoke coverage includes a DI registration test that verifies ISessionRepository resolves from the container:

- SessionRepositorySmokeTests.AddPulseWebApiCoreServices_RegistersSessionRepository

The bUnit suite also drives the critical student survey submit/reveal flow,
student trivia join/answer flow, instructor live results/reveal action, and
instructor trivia question-open action. API/repository tests cover atomic
survey persistence, reconnect identity, server scoring, and event publication.

Run it with:

- dotnet test src/Pulse.Tests/Pulse.Tests.csproj
- `.\tests\Validate-DevTunnelMetadata.ps1` for deterministic validation of the
  current wrapped `devtunnel show --json` metadata shape and legacy top-level
  shape; the fixture contains no real URL, credentials, or access tokens.

## 7. Persistent Anonymous Dev Tunnels

AppHost can expose **WebApp, Admin, WebApi, and Scalar** through four named,
persistent Microsoft dev tunnels. The Aspire dashboard, telemetry, management
endpoints, and non-HTTP MigrationManager are not tunneled. The feature is disabled
by default and is restricted to local Development runs.

**Anonymous means internet-accessible**, including Admin and API endpoints. Use
test data, retain existing instructor authorization, and do not expose production
databases, credentials, or other sensitive data. A tunnel is not application
authentication.

### Prerequisites

1. Install the [Microsoft devtunnel CLI](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started).
2. Run `devtunnel user login` (or `devtunnel user login -g` for GitHub) yourself.
   AppHost checks the cached login; it does not install tools or open a login flow.
3. Use an Aspire CLI in the project's **13.1 release family**, or launch AppHost
   through your IDE. A newer global CLI can have incompatible backchannel/resource
   discovery behavior. To install a matching CLI without replacing your global CLI:

```powershell
$cliDirectory = Join-Path $env:LOCALAPPDATA 'Pulse\AspireCli-13.1'
dotnet tool install Aspire.Cli --version 13.1.2 --tool-path $cliDirectory
$aspire = Join-Path $cliDirectory 'aspire.exe'
```

### Configure AppHost

Settings live under `DevTunnels` in `src/Pulse.AppHost/appsettings.json`.
Use Development settings, environment variables, or AppHost user secrets for
personal values. For example, from the repository root:

```powershell
$project = '.\src\Pulse.AppHost\Pulse.AppHost.csproj'
$prefix = 'pulse-yourname' # Choose your own lowercase letters/digits/hyphens.
foreach ($service in @('webapi', 'webadmin', 'webapp', 'scalar')) {
    dotnet user-secrets set "DevTunnels:Services:$($service):TunnelId" "$prefix-$service" --project $project
}
dotnet user-secrets set 'DevTunnels:Enabled' 'true' --project $project
& $aspire run --project $project
```

If using an already installed compatible CLI, replace the final line with
`aspire run --project $project`. Aspire 13.1 uses `--project`, not newer CLI flags.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `false` | Enables public tunnels for Development runs only. |
| `CliPath` | `devtunnel` | CLI executable name or absolute executable path. |
| `StartupTimeoutSeconds` | `60` | Bounds provisioning, Aspire startup, and public verification phases individually. Increase for slow networks/first container image downloads. |
| `ReconnectTimeoutSeconds` | `60` | Maximum sustained public outage before the enabled run fails. |
| `Services:<service>:TunnelId` | `pulse-developer-<service>` | Replace the example with a personal, dedicated persistent tunnel ID (3-60 lowercase letters/digits/hyphens, no leading/trailing hyphen). |
| `Services:<service>:Port` | API `5062`, Admin `5049`, WebApp `5243`, Scalar `8088` | Fixed local HTTP forwarding/proxy port. All four must be distinct and available. |

An environment override uses double underscores, for example
`$env:DevTunnels__Enabled = 'true'` or
`$env:DevTunnels__Services__webapi__TunnelId = 'pulse-alex-webapi'`.
Do not commit account-specific IDs or credentials. Remove conflicting environment
overrides before relying on user secrets or configuration files.

### Startup, URLs, and shutdown

- AppHost checks for persisted definitions, creates missing definitions with
  anonymous connect access, and configures a single HTTP forwarding port.
- It starts unhosted tunnels and reuses compatible active hosts instead of
  starting duplicates. Reused hosts must preserve Host/Origin headers and forward
  the configured port to this machine. Per-launch public probes verify the
  application identity and effective HTTPS origin before declaring readiness.
- Another local AppHost cannot concurrently manage the same tunnel ID. An active
  host on another machine, unrelated ports, incompatible protocols, or deny rules
  cause an explicit conflict. AppHost never deletes ports or clears access rules.
- Use the **actual public HTTPS URLs** printed by AppHost and shown in the
  dashboard. Public hostnames may use service-generated IDs rather than the
  configured friendly name. AppHost reads `portUri` from CLI metadata; it does not
  guess a cluster or hostname.
- The API tunnel is injected as `WebApi:BaseUrl`; the WebApp tunnel is injected as
  `App:JoinBaseUrl` into the API and WebApp. Both QR routes encode the public
  **WebApp** `/join/<code>` URL. The QR component displays that same join origin
  and still renders the fetched image as a PNG data URI.
- Scalar's document fetch and "Try it" call the public API directly. In tunnel
  mode only, the API enables CORS for the exact configured Scalar tunnel origin,
  without credentialed CORS or wildcard origins. Scalar 0.8.63's built-in proxy
  is disabled in this mode because it preserves the Scalar Host header when
  forwarding to a different tunnel. Application API calls use tunnels; Aspire
  health checks and forwarding hops remain local infrastructure.
- Public TLS terminates at the tunnel. The local hop uses HTTP with preserved
  Host/Origin; the applications restore the configured HTTPS origin only for
  loopback-proxy requests. No broad forwarded-header trust or antiforgery
  disabling is required.
- Failed readiness never silently falls back to localhost. Public connectivity
  is monitored, exited owned hosts are restarted, and a sustained outage fails
  the run. Existing live hosts are never taken over or stopped.
- Use **Ctrl+C in the CLI that launched AppHost**, or stop that AppHost in your
  IDE. Only AppHost-owned host processes are stopped. Persistent definitions are
  retained. Do not force-kill AppHost during normal shutdown: forced termination
  can leave child CLI processes requiring manual cleanup.

Keep each tunnel's identity and forwarding port unchanged to preserve QR/link
URLs across restarts. Microsoft can expire tunnels after inactivity (normally
30 days); persistence is not an indefinite reservation. An expired tunnel can be
recreated under its configured name, but its service-generated public URL can
change, so regenerate/share QR codes after recreation.

To return to the original local behavior, stop AppHost, set
`DevTunnels:Enabled=false`, remove an enabling environment override if present,
and restart. Standalone launch settings are unchanged.

### Validation

```powershell
dotnet test .\src\Pulse.Tests\Pulse.Tests.csproj --filter "FullyQualifiedName~DevTunnel|FullyQualifiedName~BuildJoinUrlTests|FullyQualifiedName~SessionQrEndpointTests|FullyQualifiedName~SessionQRCodeComponentTests"
```

These tests use fake CLI/HTTP boundaries and do not create public resources.
For a live check, start with personalized IDs and test data, open each public URL
without signing into the tunnel service, create a test session, scan a QR on a
second device, and confirm it opens the public WebApp join route. Exercise a
Blazor action and Scalar "Try it". Restart AppHost and confirm all public URLs
remain unchanged. After stopping, `devtunnel show <id> --json` should retain the
definition but report no owned host connection (service status can lag briefly).

See [tunnel troubleshooting](troubleshooting.md#persistent-dev-tunnel-issues).

## 8. Where to Look Next

- docs/architecture.md for deployment and activity-flow diagrams.
- docs/api.md for endpoint details (expand this as endpoints evolve).
- docs/troubleshooting.md for common setup and runtime issues.
