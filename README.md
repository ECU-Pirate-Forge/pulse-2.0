
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

Activation, closure, live instructor updates, and student-side result reveal are planned work rather than completed behavior.


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
- Shared accessible ECU purple-and-gold design tokens used by both Blazor apps
- Global exception middleware for consistent API error handling
- Structured logging behavior coverage
- Admin DB export endpoint behavior

## Roadmap

Planned and in-progress future work is tracked in the sprint backlog and implementation notes.

Near-term roadmap:

- Reframe Pulse around anonymous surveys rather than quiz administration
- Establish an accessible ECU purple-and-gold design system
- Separate student workflows into `Pulse.WebApp` and instructor workflows into `Pulse.Admin`
- Add multiple-answer, true/false, and matching survey questions
- Complete activate/close lifecycle behavior and atomic survey submission
- Expand the moderated question bank and add individual/team trivia competition flows
- Add a repeatable devtunnel workflow for real-device testing
- Improve API documentation depth with request/response examples per endpoint
- Add broader integration and end-to-end test coverage for critical classroom flows

Mid-term roadmap:

- Add a moderated question bank with student suggestions
- Add separate individual and instructor-defined team trivia experiences
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

Instruction validation was performed by a non-author reviewer (GitHub Copilot agent) on 2026-04-26 in a Windows environment:

- `dotnet --version` -> 10.0.201
- `dotnet restore Pulse.slnx` -> success
- `dotnet build .\Pulse.slnx -c Debug` -> success
- `dotnet test .\Pulse.slnx` -> 181 passed, 0 failed

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
