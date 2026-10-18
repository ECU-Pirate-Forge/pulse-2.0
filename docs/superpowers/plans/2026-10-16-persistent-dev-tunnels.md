# Persistent Anonymous Dev Tunnels Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make AppHost optionally expose every application HTTP service through a named, persistent, anonymously accessible dev tunnel, including the URLs used for QR generation and session joining.

**Architecture:** AppHost owns tunnel configuration, provisioning, URL discovery, and host lifecycle. A small CLI adapter checks persisted tunnel definitions and running hosts before creating or starting anything, and supplies verified public HTTPS addresses to application configuration. Local Aspire endpoints remain the forwarding targets and health-check infrastructure; tunnel-disabled mode retains the existing graph and URL behavior.

**Tech Stack:** .NET 10, Aspire 13.1.1, the installed Microsoft devtunnel CLI, Scalar.Aspire 0.8.63, existing xUnit/Moq/bUnit tests.

---

## Status and planning base

- **Implementation approved and implemented on the planning branch.** See the
  verification record below for automated/live evidence and remaining manual checks.
- Actual checked-out branch: `add-devtunnels` (plural), commit `0769f0e`.
- Worktree was clean when planning began. Do not switch branches, reset changes, or upgrade Aspire as part of this feature.
- Six clarification questions were presented twice through the question tool. Both attempts returned an unavailable-user response, not actual answers.
- The user's subsequent instruction to implement this plan accepted the proposed
  defaults below. No branch switch, commit, or Aspire package upgrade was made.
- Live validation required one focused adjustment: Scalar calls the public API
  directly with exact-origin, non-credentialed CORS in Development tunnel mode.
  The installed proxy preserves the incoming Scalar Host header and consequently
  routes cross-tunnel requests back to Scalar. A clarification was attempted;
  with the user unavailable, the recommended narrow CORS option was selected
  rather than adding a custom proxy or upgrading packages.

## Six questions and proposed answers

1. **Which endpoints are public?** WebApp, Admin, WebApi, and the AppHost-hosted Scalar documentation application. Keep the Aspire dashboard, telemetry, and management endpoints private. MigrationManager has no HTTP endpoint to tunnel.
2. **Do internal application calls use tunnels?** Yes. In tunnel mode, application HTTP clients and documentation requests use the API's public HTTPS address. Aspire's local health checks and tunnel forwarding necessarily remain local infrastructure traffic.
3. **How are tunnels named?** One persistent tunnel per service with explicit configurable IDs, following `pulse-<developer>-<service>`. Preserve the tunnel ID, cluster, and forwarded port across restarts; a display name alone does not guarantee a stable URL.
4. **How is the feature enabled?** An AppHost `DevTunnels` section, disabled by default, overridable through configuration files, environment variables, and AppHost user secrets. Disabled mode neither invokes the CLI nor alters existing URL selection.
5. **What is the lifecycle?** Reuse a correctly configured running host; create a missing persisted definition; start an unhosted tunnel. Stop only processes started by this AppHost and never delete persistent definitions. Apply bounded startup/reconnection timeouts and surface failures rather than falling back to localhost.
6. **What are the prerequisites and exposure rules?** Require an installed CLI and an already authenticated developer. Enable anonymous access for selected tunnels without removing application authorization. Do not automatically install tools or launch interactive login.

## Implementation completion record

- [x] Typed configuration and stable local forwarding ports.
- [x] Persistent anonymous provisioning, compatible active-host reuse, ownership
  locks, cleanup, exact application identity probes, and bounded recovery.
- [x] Public API/client, WebApp QR/join, dashboard-link, and Scalar wiring.
- [x] Development-only public HTTPS origin handling; existing authorization and
  antiforgery retained; tunnel-disabled behavior covered by regression tests.
- [x] Onboarding, troubleshooting, API, architecture, README/index, and changelog
  updated.
- [x] Solution build and full regression verification: **235 tests passed**.

Verification on 2026-10-16:

- `dotnet build .\Pulse.slnx -c Debug --no-restore -clp:ErrorsOnly`: passed,
  zero errors; existing package/build warnings remain. A unique `-bl` binary log
  was captured outside the checkout.
- `dotnet test .\src\Pulse.Tests\Pulse.Tests.csproj --no-build --no-restore
  --logger 'console;verbosity=minimal'`: final full rerun passed all 235 tests,
  with a unique `-bl` log. The preceding run hit an intermittent LiteDB
  collection-enumeration error in an unchanged session test (234 passed);
  no unrelated repository/mapping changes were made to mask it.
- Four dedicated anonymous validation tunnels reached their corresponding
  applications. Public URLs remained unchanged across repeated AppHost restarts.
- Both live QR routes returned PNG bytes exactly matching the PNG for the
  expected public WebApp `/join/<code>` payload. This is exact payload-based
  image comparison, not just a PNG signature check or an independent decoder.
- Desktop-browser Blazor instructor access and QR navigation worked through the
  WebApp tunnel; the displayed join URL matched the expected payload. A public
  join URL resolved to the intended disposable session.
- Scalar loaded the public OpenAPI document in a browser. Its actual "Test
  Request" action returned HTTP 200 and `Pulse API is running` from the public
  API. An unrelated browser origin did not receive CORS access.
- Terminating only the owned validation API tunnel host triggered an unhealthy
  transition and successful recovery in approximately five seconds, preserving
  the URL. Recovery now waits for stale service-side host metadata to clear
  rather than treating it as an immediate competing-host conflict.
- Fake-boundary tests additionally cover active-host reuse/survival, lock
  conflicts, persistent access setup, partial failure cleanup, blocked-command
  timeout/cancellation, wrong identities, and refusal to take over active hosts.
- Final shutdown left no validation host processes running. All four named
  definitions remained persisted with their original forwarding ports and zero
  host connections. The incidental CLI-generated root configuration file was
  removed; changes remain uncommitted on `add-devtunnels`.

Manual limitation: no physical second-device/phone scan was available. Desktop
browser checks and unauthenticated HTTP requests are not represented as a
physical-device scan. The original detailed execution checklist below is
retained as planning history; this completion record is the validation status.

## Findings affecting the implementation

- `src/Pulse.AppHost/AppHost.cs` currently exposes WebApi, Admin, WebApp, and Scalar without any tunnel provisioning.
- `src/Pulse.WebApp/Program.cs` reads `WebApi:BaseUrl` before falling back to Aspire service discovery. Development configuration currently selects a localhost API URL.
- Both API QR handlers already read `App:JoinBaseUrl` through `SessionEndpointHandlers.BuildJoinUrl`. Supply the **WebApp** public URL, not the API URL.
- `SessionQRCode.razor` displays a separate join URL derived from `NavigationManager.BaseUri`. It fetches PNG bytes through its injected API client and renders a data URI, not a direct QR-image URL. Preserve that behavior while making the displayed and encoded destinations agree.
- All three ASP.NET applications call `UseHttpsRedirection`. Public HTTPS termination with local HTTP forwarding must not redirect a browser to a localhost HTTPS port.
- Blazor's interactive connection and antiforgery behavior must work through the tunnel, not merely the initial HTML request.
- Scalar's installed package exposes `WithOpenApiRoutePattern`, `WithBaseServerUrl`, and proxy configuration. Browser-facing links, OpenAPI fetching, and "Try it" requests must not leak local addresses.
- Aspire's 13.1.1 DevTunnels integration creates or updates persistent definitions and does not delete them at shutdown. However, it unconditionally launches `devtunnel host` and can delete unmodeled ports. It does not, by itself, satisfy the requested already-running-host reuse behavior.
- Prefer a focused AppHost CLI adapter for this lifecycle rather than upgrading Aspire or depending on undocumented integration internals. No new runtime framework is required.
- Persistent does not mean permanent: the service can expire tunnels after inactivity. Recreate an expired definition under its configured ID, report resulting address changes, and never promise indefinite preservation after service-side expiry.

## Proposed configuration contract

Add this disabled example to `src/Pulse.AppHost/appsettings.json`:

```json
{
  "DevTunnels": {
    "Enabled": false,
    "CliPath": "devtunnel",
    "StartupTimeoutSeconds": 60,
    "ReconnectTimeoutSeconds": 60,
    "Services": {
      "webapi": { "TunnelId": "pulse-developer-webapi", "Port": 5062 },
      "webadmin": { "TunnelId": "pulse-developer-webadmin", "Port": 5049 },
      "webapp": { "TunnelId": "pulse-developer-webapp", "Port": 5243 },
      "scalar": { "TunnelId": "pulse-developer-scalar", "Port": 8088 }
    }
  }
}
```

The IDs are illustrative and must be personalized before enabling. In enabled mode, reject missing IDs, invalid IDs, duplicate IDs, duplicate/out-of-range local ports, and nonpositive timeouts. Disabled mode must not require valid tunnel settings.

Configure fixed Aspire proxy ports only in enabled mode. Tunnel public addresses include port identity, so dynamically assigned forwarding ports are unsuitable for URL stability.

## File responsibilities

| File | Planned responsibility |
| --- | --- |
| `src/Pulse.AppHost/AppHost.cs` | Gate tunnel mode; wire stable local endpoints, public addresses, lifecycle, and shutdown. |
| `src/Pulse.AppHost/DevTunnels/DevTunnelOptions.cs` (new) | Bind and validate the feature configuration and per-service identities. |
| `src/Pulse.AppHost/DevTunnels/DevTunnelCli.cs` (new) | Typed JSON CLI commands, cancellable process execution, and actionable command failures. |
| `src/Pulse.AppHost/DevTunnels/DevTunnelManager.cs` (new) | Idempotent provisioning, host reuse/start, readiness, reconnect, ownership, and cleanup. |
| `src/Pulse.AppHost/appsettings.json` | Disabled configuration example. |
| `src/Pulse.ServiceDefaults/DevTunnelExtensions.cs` (new) | Shared opt-in tunnel proxy/header and request-origin handling. |
| `src/Pulse.WebApi/Program.cs` | Apply shared tunnel middleware before redirection; expose public OpenAPI server information in tunnel mode. |
| `src/Pulse.WebApp/Program.cs` | Apply shared tunnel middleware before redirection; preserve existing client registration. |
| `src/Pulse.Admin/Program.cs` | Apply shared tunnel middleware before redirection; retain existing authorization and UI. |
| `src/Pulse.WebApp/Components/SessionQRCode.razor` | Prefer configured public join origin in tunnel mode; retain navigation-origin fallback otherwise. |
| `src/Pulse.Tests/Pulse.Tests.csproj` | Add AppHost project access for lifecycle/configuration tests without registering it as an application resource. |
| `src/Pulse.Tests/DevTunnels/DevTunnelOptionsTests.cs` (new) | Enabled/disabled configuration validation. |
| `src/Pulse.Tests/DevTunnels/DevTunnelManagerTests.cs` (new) | Lifecycle state matrix using a fake CLI/process boundary, without live cloud mutation. |
| `src/Pulse.Tests/DevTunnels/DevTunnelWiringTests.cs` (new) | Enabled/disabled AppHost URL, endpoint, and Scalar wiring. |
| `src/Pulse.Tests/DevTunnels/DevTunnelForwardingTests.cs` (new) | Public scheme/host handling and absence of localhost redirects. |
| `src/Pulse.Tests/Sessions/BuildJoinUrlTests.cs` | Tunnel join-origin regression cases. |
| `src/Pulse.Tests/Sessions/SessionQrEndpointTests.cs` | Both QR routes, configured origin, PNG output, and missing-session behavior. |
| `src/Pulse.Tests/WebApp/SessionQRCodeComponentTests.cs` | Exact displayed join link and API request destination. |
| `docs/onboarding.md`, `docs/troubleshooting.md`, `README.md` | Setup, configuration, lifecycle, public-access warning, and failure recovery. |

## Task 1: Configuration and stable endpoint contracts

- [ ] Write options tests first: disabled mode ignores missing tunnel details; enabled mode accepts four explicit service mappings; invalid IDs/ports, duplicate IDs/ports, and invalid timeouts fail with the configuration key in the message.
- [ ] Run the configuration test selector and confirm the tests initially fail for the missing feature.
- [ ] Implement typed configuration and validation in `DevTunnelOptions.cs`, using the existing configuration system rather than a separate settings loader.
- [ ] Add the disabled example configuration shown above.
- [ ] Add graph-level tests showing disabled mode keeps current endpoints and registers no tunnel processes.
- [ ] Run the selector again and require all configuration tests to pass.

## Task 2: CLI boundary and idempotent persisted definitions

- [ ] Write fake-command tests for installed/missing CLI, authenticated/unauthenticated user, missing/existing tunnel, malformed JSON, denied access, service failure, timeout, and cancellation.
- [ ] Implement commands with `ProcessStartInfo.ArgumentList`, redirected stdout/stderr read concurrently, cancellation, exit-code checking, and process-tree cleanup for owned child processes. Do not assemble shell command strings or log credentials/tokens.
- [ ] Preflight the CLI and cached sign-in. Fail with installation/login instructions when unavailable; do not initiate installation or login.
- [ ] Use explicit tunnel IDs on every command; never rely on the CLI's last-used tunnel.
- [ ] Parse machine-readable service metadata to distinguish a missing definition from authentication, authorization, or network failure. Never treat every nonzero exit code as "missing".
- [ ] Create absent definitions with `devtunnel create <id> --allow-anonymous --json`. Handle a concurrent-create conflict by rereading the definition once, not by deleting or overwriting it.
- [ ] Inspect ports/access policies; create the configured HTTP port only if missing, and add anonymous connect access only if absent. Do not reset unrelated policies, remove deny rules, delete unrelated ports, or mutate a running incompatible tunnel.
- [ ] Retrieve the actual cluster-qualified HTTPS URL from tunnel/port metadata. Validate the scheme and port association; do not construct an address by guessing a cluster or parsing a human log line.
- [ ] Rerun CLI-boundary tests and verify failures remain failures with actionable diagnostics.

## Task 3: Running-host reuse, ownership, and lifecycle

- [ ] Write lifecycle tests before implementation for the following state table:

| Definition | Host | Expected action |
| --- | --- | --- |
| Missing | None | Create once, configure port/access, start once, wait for readiness. |
| Existing | Stopped | Verify identity/port/access, start once, preserve public URL. |
| Existing | Running and matching this local target | Verify routing and reuse; never start a duplicate or stop it on shutdown. |
| Existing | Running elsewhere or incompatible | Report an identity/target conflict; do not kill or repoint it. |
| Existing | Disconnects after readiness | Attempt bounded reconnect for an owned host; retain URL and report health transitions. |

- [ ] Implement `DevTunnelManager` with an explicit distinction between owned processes and adopted hosts. A service-side host-connection count alone does not prove that traffic reaches this AppHost.
- [ ] For a new host, run `devtunnel host <id> --host-header unchanged --origin-header unchanged`. Keep the persisted ID and configured local forwarding port unchanged.
- [ ] Acquire a per-tunnel local ownership lock to prevent two local AppHosts racing to manage the same mapping. Record ownership outside the repository without storing credentials.
- [ ] Verify reused hosts against local ownership evidence and a per-launch origin probe after local applications become available. Reuse only when routing reaches the expected local application; fail clearly for an unknown/remote active host.
- [ ] Resolve all public addresses before injecting application settings. Start tunnel forwarding independently of application HTTP readiness to avoid a WebApi/WebApp dependency cycle.
- [ ] Declare the enabled stack ready only after the expected public endpoints reach their matching local applications within 60 seconds. Support cancellation and clean up already-started owned hosts if a later service fails.
- [ ] On runtime disconnection, publish unhealthy state immediately; allow 60 seconds for reconnection. A sustained failure must be visible and fail the enabled run rather than silently leaving localhost fallback active.
- [ ] On shutdown, stop/dispose only owned child processes and release owned locks. Never run tunnel-delete commands or terminate a process by executable name.
- [ ] Rerun the lifecycle selector and require startup-failure cleanup, adopted-host survival, stable URL reuse, and reconnection tests to pass.

## Task 4: AppHost URL wiring and Scalar

- [ ] Add tests for enabled and disabled graph/environment output before changing `AppHost.cs`.
- [ ] In enabled mode, pin existing local HTTP proxy endpoint ports to the configured values without changing standalone launch profiles. Local listeners are forwarding infrastructure, not public links.
- [ ] Preserve existing `WaitFor(webapi)`, health checks, and migration behavior. Do not make WebApi wait for WebApp readiness just because its join URL targets WebApp.
- [ ] Inject `WebApi__BaseUrl` with the API public URL into application clients, and `App__JoinBaseUrl` with the WebApp public URL into WebApi and WebApp.
- [ ] Override existing localhost development settings through AppHost-provided environment values, rather than deleting standalone settings.
- [ ] Supply each application its own public origin and opt-in tunnel-mode setting for proxy/origin handling.
- [ ] Advertise the public URLs in Aspire's resource links while retaining local forwarding/health-check endpoints as infrastructure.
- [x] Configure Scalar's document and server URLs to use the public API directly.
  Disable its built-in proxy in tunnel mode and allow only the configured Scalar
  HTTPS tunnel origin through a non-credentialed API CORS policy. Preserve
  disabled-mode behavior; do not introduce permissive/wildcard CORS.
- [ ] Keep API-hosted Scalar/OpenAPI routes functional through the API tunnel as well as the separate Scalar resource.
- [ ] Rerun graph tests; assert public links and client destinations contain no localhost, `.dev.localhost`, or logical `https+http://webapi` URLs in enabled mode.

The intended environment shape is:

```text
webapi:
  App__JoinBaseUrl = <resolved WebApp HTTPS tunnel origin>
webapp:
  WebApi__BaseUrl = <resolved WebApi HTTPS tunnel origin>
  App__JoinBaseUrl = <resolved WebApp HTTPS tunnel origin>
webadmin:
  WebApi__BaseUrl = <resolved WebApi HTTPS tunnel origin>
```

Values above describe runtime-resolved service outputs, not literal values to place in configuration.

## Task 5: HTTPS termination, Blazor, and QR consistency

- [ ] Write forwarding tests for external HTTPS scheme/host, redirects, trusted/untrusted forwarded headers, and disabled-mode behavior.
- [ ] Add a shared, explicitly opt-in request-pipeline helper and invoke it before redirection/antiforgery in all three web applications.
- [ ] Restrict forwarded-header trust to the actual local proxy topology and validate hosts against each configured public origin. Do not clear trusted proxy lists globally or trust arbitrary internet-supplied headers.
- [ ] In enabled mode, do not redirect a tunnel's local HTTP hop to a localhost HTTPS listener. Preserve existing HTTPS redirection outside tunnel mode.
- [ ] Preserve host/origin consistency for antiforgery, secure cookies, and Blazor WebSocket/long-polling connections. Do not disable antiforgery or application authorization to make forwarding work.
- [ ] Add a `BuildJoinUrl` regression using a distinct API request origin and public WebApp join origin:

```csharp
[Fact]
public void BuildJoinUrl_UsesWebAppTunnelInsteadOfApiRequestOrigin()
{
    var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
    context.Request.Scheme = "http";
    context.Request.Host =
        new Microsoft.AspNetCore.Http.HostString("localhost", 5062);

    var actual = Pulse.WebApi.SessionEndpointHandlers.BuildJoinUrl(
        "https://pulse-student-5243.use2.devtunnels.ms/",
        context.Request,
        "ABC123");

    Assert.Equal(
        "https://pulse-student-5243.use2.devtunnels.ms/join/ABC123",
        actual);
}
```

- [ ] Cover both QR routes. Preserve existing PNG response and not-found behavior; do not rewrite QR generation when configuration injection already solves the encoded destination.
- [ ] Make the QR component prefer configured `App:JoinBaseUrl` while retaining `NavigationManager.BaseUri` fallback when disabled. Keep rendering the fetched PNG as a data URI.
- [ ] Add bUnit coverage with a successful fake PNG HTTP response. Assert the exact displayed HTTPS join link and that the request to `/api/sessions/qr/<code>` uses the API tunnel client base address.
- [ ] Rerun the forwarding and QR selectors. Do not treat a PNG signature check alone as proof that the encoded destination is correct.

## Task 6: Validation and directly related documentation

- [ ] Update onboarding with manual CLI installation/sign-in, personalized persistent IDs, feature enable/disable instructions, stable-port requirements, and startup/shutdown behavior.
- [ ] Update troubleshooting for expired sign-in, missing CLI, ownership conflicts, denied anonymous access, tunnel expiration, startup timeout, reconnect exhaustion, forwarding/redirect problems, and the service's browser warning/interstitial.
- [ ] Document that anonymous access is internet exposure, including Admin/API; use test data, keep existing authorization, and never expose the Aspire management plane.
- [ ] Add a concise README link to the new onboarding section. Correct its AppHost lifecycle command only where needed to explain the supported tunnel workflow.
- [ ] Run the targeted suite:

```powershell
dotnet test .\src\Pulse.Tests\Pulse.Tests.csproj --filter "FullyQualifiedName~DevTunnel|FullyQualifiedName~BuildJoinUrlTests|FullyQualifiedName~SessionQrEndpointTests|FullyQualifiedName~SessionQRCodeComponentTests"
```

Expected: all selected configuration, CLI, lifecycle, wiring, forwarding, QR, and component tests pass without contacting the tunnel service.

- [ ] Build the solution with a unique MSBuild binary log and run the regression suite:

```powershell
dotnet build .\Pulse.slnx -c Debug /bl:.\artifacts\dev-tunnels-build.binlog
dotnet test .\src\Pulse.Tests\Pulse.Tests.csproj
```

Use a unique timestamped binlog filename for the actual run. Record exact commands, pass/fail results, and any independently reproduced pre-existing failures. If Aspire holds build outputs, stop only this AppHost through the supported lifecycle tools before rebuilding.

- [ ] Start the exact AppHost through available Aspire lifecycle tooling. Do not use broad process-kill commands. Use Aspire's resource readiness mechanism before exercising application routes.
- [ ] Perform the following live acceptance checks using a signed-in test developer account. These checks create anonymous public resources, so run them only after implementation approval:

| Check | Required evidence |
| --- | --- |
| Disabled mode | Existing local workflow works; no CLI invocation or tunnel host starts. |
| First enabled run | Four named persistent anonymous mappings are created/started and logged with their actual HTTPS URLs. |
| Anonymous device | WebApp, Admin, API, and Scalar are reachable from a second device without developer-account login; existing app-level authorization still applies. |
| HTTPS/Blazor | No redirect to localhost; interactive actions, antiforgery, and reconnect work. |
| API clients | Application API calls use the API tunnel rather than localhost/service discovery. |
| QR by ID and code | Decode an actual image from each API route; each payload equals the public WebApp `/join/<code>` link exactly. |
| QR displayed link | Component link equals the decoded payload, including HTTPS host and path. |
| Phone join | Scan the QR on a device outside the development machine and successfully join the intended session. |
| Scalar | OpenAPI fetch and "Try it" work through tunnels without mixed content, localhost requests, or broad CORS changes. |
| Restart | Tunnel IDs, cluster-qualified public URLs, and forwarded ports remain unchanged. |
| Existing running host | Verified matching host is reused without starting a duplicate; an incompatible host produces a clear conflict. |
| Disconnect | Owned host reconnects within the configured limit or produces a visible terminal failure. |
| Shutdown | Owned hosts stop, adopted hosts survive, persistent definitions remain. |
| Partial failure | Already-started owned hosts are cleaned up and startup does not claim success. |

- [ ] Stop only test hosts owned by this validation run. Preserve named persistent definitions for the next test run. Remove only explicitly identified temporary validation artifacts, not user files.
- [ ] Review the final diff for unrelated edits and secrets. Report test results and live-validation limits honestly.

## Approval gate and acceptance boundary

The user approved implementation after reviewing this plan. The six proposed
defaults were used, with the Scalar adjustment documented above.

The feature is not complete merely because tunnels start or QR endpoints return PNGs. Completion requires stable named definitions, anonymous reachability, correct running-host handling, public URL propagation through both QR paths and Scalar, unchanged disabled behavior, and verified ownership-safe shutdown.

## Reference evidence

- [Microsoft devtunnel CLI commands](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/cli-commands)
- Aspire Dev Tunnels integration documentation, retrieved through Aspire documentation tooling.
- Aspire `v13.1.1` DevTunnels resource builder and CLI-client source, inspected to distinguish persistent-definition reuse from host-process reuse.
- Installed devtunnel CLI `1.0.2094`, inspected using help/version commands only.
- Installed Scalar.Aspire `0.8.63` XML API documentation.
