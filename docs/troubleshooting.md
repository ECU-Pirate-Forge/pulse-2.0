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

3. Make sure no stale long-running process is locking files from a previous run.

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
dotnet run --project src/Pulse.AppHost/Pulse.AppHost.csproj
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