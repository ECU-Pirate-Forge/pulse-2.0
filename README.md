
# Pulse 2.0

Pulse is a classroom pulse and feedback platform built to help instructors check understanding in real time while class is in progress. Instructors create and run sessions, students join with a code or QR flow, and responses are aggregated live so teaching can be adjusted immediately.

The solution is implemented as a multi-project .NET 10 application with Blazor frontends, minimal APIs, SignalR for real-time updates, and LiteDB for local persistence.

## Project Background

Traditional end-of-class surveys are too slow for in-class decision making. Pulse addresses that gap with live, low-friction interactions that preserve student anonymity while still giving instructors actionable feedback.

Current architecture highlights:

- ASP.NET Core + Blazor Server apps for instructor and student experiences
- Minimal API endpoints for session, question, response, and results workflows
- SignalR session groups for near real-time broadcast updates
- LiteDB repositories for lightweight local data persistence
- Aspire AppHost for local orchestration and observability

## Fully Implemented Features

The following are currently implemented in this repository (and covered by project tests):

- Session lifecycle management: create, retrieve, activate, close, and unblind session state
- Instructor access protection on management endpoints via instructor code middleware
- Question authoring workflow: create, fetch, update, and delete question data
- Question bank APIs and repository support for reusable question content
- Student response submission with validation and upsert-by-device behavior
- Response aggregation service logic for live tally/result views
- QR endpoint support for session join flows
- Global exception middleware for consistent API error handling
- Admin DB export endpoint behavior

## Roadmap

Planned and in-progress future work is tracked in the sprint backlog and implementation notes.

Near-term roadmap:

- Complete and refine instructor UI workflows for session and question management
- Continue expanding student experience and responsiveness across devices
- Improve API documentation depth with request/response examples per endpoint
- Add broader integration and end-to-end test coverage for critical classroom flows

Mid-term roadmap:

- AI-assisted question generation workflow refinement and UX hardening
- Enhanced analytics/reporting views for post-session review
- Operational hardening (telemetry, diagnostics, reliability, and deployment readiness)

## Project Structure

High-level repository layout:

```text
/
|-- README.md
|-- docs/                         # Architecture, onboarding, API, troubleshooting, backlog
|-- src/
|   |-- Pulse.AppHost/            # Aspire orchestrator
|   |-- Pulse.WebApi/             # Minimal API backend
|   |-- Pulse.WebApp/             # Student-facing Blazor Server app
|   |-- Pulse.Admin/              # Instructor/admin Blazor Server app
|   |-- Pulse.Application/        # Application-layer services
|   |-- Pulse.Domain/             # Domain entities and core models
|   |-- Pulse.Shared/             # Shared models/services/common code
|   |-- Pulse.ServiceDefaults/    # Service default configuration/extensions
|   |-- Pulse.MigrationManager/   # Background migration/worker process
|   `-- Pulse.Tests/              # xUnit test project
|-- tests/                        # Additional test assets/supporting test content
|-- tools/                        # Utility scripts/tools
`-- tmp/                          # Local temporary artifacts (gitignored)
```

## Getting Started

### Prerequisites

- .NET SDK 10.0+
- Git
- (Optional) Visual Studio 2022 or VS Code with C# Dev Kit

### 1. Clone

```bash
git clone https://github.com/ECU-Pirate-Forge/pulse-2.0.git
cd pulse-2.0
```

### 2. Restore Dependencies

```bash
dotnet restore Pulse.slnx
```

### 3. Build

```bash
dotnet build Pulse.slnx -c Debug
```

### 4. Run Tests

```bash
dotnet test .\Pulse.slnx
```

### 5. Run the Full Local Stack (Recommended)

```bash
dotnet run --project src/Pulse.AppHost/Pulse.AppHost.csproj
```

### 6. Run Individual Services (Optional)

```bash
dotnet run --project src/Pulse.WebApi/Pulse.WebApi.csproj
dotnet run --project src/Pulse.WebApp/Pulse.WebApp.csproj
dotnet run --project src/Pulse.Admin/Pulse.Admin.csproj
```

## Getting Started Validation

Instruction validation was performed by a non-author reviewer (GitHub Copilot agent) on 2026-04-22 in a Windows environment:

- `dotnet --version` -> 10.0.201
- `dotnet restore Pulse.slnx` -> success
- `dotnet build .\Pulse.slnx -c Debug` -> success
- `dotnet test .\Pulse.slnx` -> 124 passed, 0 failed

## Documentation

- [Documentation index](docs/index.md)
- [Onboarding guide](docs/onboarding.md)
- [Architecture](docs/architecture.md)
- [API reference](docs/api.md)
- [Troubleshooting](docs/troubleshooting.md)
- [Sprint backlog](docs/sprint-backlog.md)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## License

MIT License. See [LICENSE](LICENSE).
