
# Pulse 2.0

Pulse is a classroom pulse and feedback platform built to help instructors check understanding while class is in progress. Instructors create sessions, students join with a code or QR flow, and responses are aggregated for instructor review. Real-time updates are part of the approved redesign work.

The solution is implemented as a multi-project .NET 10 application with Blazor frontends, minimal APIs, LiteDB for local persistence, and SignalR for live classroom updates.

## Project Background

Traditional end-of-class surveys are too slow for in-class decision making. Pulse addresses that gap with live, low-friction interactions that preserve student anonymity while still giving instructors actionable feedback.

Current architecture highlights:

- Separate ASP.NET Core + Blazor Server apps for instructor and student experiences
- Minimal API endpoints for session, question, response, and results workflows
- LiteDB repositories for lightweight local data persistence
- Aspire AppHost for local orchestration and observability

## User Workflow

Typical classroom usage flow:

1. Instructor creates a session using the current instructor workflow.
2. Instructor adds questions (manually, from question bank content, or via CSV import workflows).
3. Instructor activates the session and shares the join path using a join code or QR code.
4. Students join from the web app and submit responses from their own devices.
5. Pulse validates and records responses.
6. Instructor can retrieve aggregate results.
7. Instructor can persist the unblinded-results setting.
8. Students can anonymously suggest questions; instructors review, edit,
   approve, reject, search, and filter the reusable question bank.
9. Instructors can run separate individual or instructor-defined team trivia
   games with server-timed scoring and live leaderboards.

## Fully Implemented Features

The following are currently implemented in this repository (and covered by project tests):

- Session creation, retrieval, instructor-scoped listing, and unblind-state persistence
- Instructor access protection on management endpoints via instructor code middleware
- Instructor-scoped session listing via `GET /sessions`
- Question authoring workflow: create, fetch, update, and delete question data
- Question ordering/reorder endpoint support
- Question bank APIs and repository support for reusable question content
- Question bank CSV preview/import workflow with parser validation
- Student response submission with validation and upsert-by-device behavior
- Response aggregation service logic for instructor tally/result retrieval
- QR endpoint support for session join flows (by join code and by session id)
- Survey-first student landing/join experience and dedicated instructor workspace navigation
- Atomic whole-survey submission, lifecycle transitions, SignalR progress updates with polling fallback, and instructor-controlled result reveal
- Individual and instructor-defined team trivia with server-side timing, scoring, polling, and leaderboards
- Shared accessible ECU purple-and-gold design tokens used by both Blazor apps
- Global exception middleware for consistent API error handling
- Privacy-safe structured logging that omits student response content and persistent device identifiers
- Admin DB export endpoint behavior
- Repeatable Windows dev-tunnel workflow for real-device testing
- bUnit functional coverage for student survey submission/reveal, student trivia,
  and instructor live-survey/trivia controls

## Roadmap

Planned and in-progress future work is tracked in the sprint backlog and implementation notes.

Current future-work themes:

- Enhanced analytics/reporting views for post-session review
- Deployment readiness beyond the documented local/Aspire development environment

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

### 6. Test on a Phone through a Dev Tunnel

Create a named anonymous HTTPS tunnel once, then use the checked-in launcher:

```powershell
devtunnel user login
devtunnel create pulse-student-yourname --allow-anonymous
devtunnel port create pulse-student-yourname -p 7004 --protocol https
.\tools\Start-MobileTest.ps1 -TunnelId pulse-student-yourname
```

The script hosts the tunnel, discovers its validated public student URL, starts
Aspire with that URL explicitly configured for QR links, and stops both
processes when testing ends. See the
[onboarding guide](docs/onboarding.md) for QR checks, cleanup, and named-tunnel
safety checks, and use the [mobile acceptance matrix](docs/mobile-acceptance.md).

### 7. Run Individual Services (Optional)

```bash
dotnet run --project src/Pulse.WebApi/Pulse.WebApi.csproj
dotnet run --project src/Pulse.WebApp/Pulse.WebApp.csproj
dotnet run --project src/Pulse.Admin/Pulse.Admin.csproj
```

## Getting Started Validation

Latest Windows validation (2026-09-23):

- `dotnet build Pulse.slnx --no-restore` -> success
- `dotnet test Pulse.slnx --no-build --no-restore` -> 271 passed, 0 failed

## Documentation

- [Documentation index](docs/index.md)
- [Onboarding guide](docs/onboarding.md)
- [Architecture](docs/architecture.md)
- [API reference](docs/api.md)
- [Troubleshooting](docs/troubleshooting.md)
- [Mobile acceptance matrix](docs/mobile-acceptance.md)
- [Sprint backlog](docs/sprint-backlog.md)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## License

MIT License. See [LICENSE](LICENSE).
