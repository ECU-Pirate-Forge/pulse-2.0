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

- dotnet run --project src/Pulse.AppHost/Pulse.AppHost.csproj

This launches the distributed app and Aspire dashboard endpoints from the AppHost launch profile.

Run individual projects if needed:

- Web API: dotnet run --project src/Pulse.WebApi/Pulse.WebApi.csproj
- Student app: dotnet run --project src/Pulse.WebApp/Pulse.WebApp.csproj
- Admin app: dotnet run --project src/Pulse.Admin/Pulse.Admin.csproj

Current development URLs from launch settings:

- Web API: http://localhost:5062 and https://localhost:7081
- Web App mobile-test endpoint: https://localhost:7004
- Admin App: http://pulse-admin.dev.localhost:5049 and https://pulse-admin.dev.localhost:7019

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

## 7. Where to Look Next

- docs/architecture.md for deployment and activity-flow diagrams.
- docs/api.md for endpoint details (expand this as endpoints evolve).
- docs/troubleshooting.md for common setup and runtime issues.
