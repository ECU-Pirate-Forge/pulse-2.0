
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
2. Instructor adds questions manually, from the question bank/CSV, or by reviewing locally generated document-grounded questions.
3. Instructor activates the session and shares the join path using a join code or QR code.
4. Students join from the web app and submit responses from their own devices.
5. Pulse validates and records responses.
6. Instructor can retrieve aggregate results.
7. Instructor can persist the unblinded-results setting.
8. Students can anonymously suggest questions; instructors review, edit,
   approve, reject, search, and filter the reusable question bank.
9. Instructors can run separate individual or instructor-defined team trivia
   games with server-timed scoring and live leaderboards.

The shared `/join/{code}` path checks surveys first, then trivia games if no
survey matches. Trivia codes continue to `/trivia/{code}` with the game already
loaded; students enter a display name and, for team games, select a team.
Students can also enter their game code directly on `/trivia`.

## Fully Implemented Features

The following are currently implemented in this repository (and covered by project tests):

- Session creation, retrieval, instructor-scoped listing, and unblind-state persistence
- Instructor access protection on management endpoints via instructor code middleware
- Instructor-scoped session listing via `GET /sessions`
- Question authoring workflow: create, fetch, update, and delete question data
- Question ordering/reorder endpoint support
- Question bank APIs and repository support for reusable question content
- Question bank CSV preview/import workflow with parser validation
- Optional draft-survey document grounding with local Ollama retrieval and instructor-reviewed multiple-choice generation
- Persisted survey answer keys and citations with separate student-safe question projections
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
- Per-question reveal with correct-answer indicators (survey-wide aggregate reveal already exists)

## Local document-grounded question generation

Ollama runs separately from Aspire on the same machine as Pulse.WebApi.
The defaults are `http://localhost:11434`, `gemma4:latest` for generation,
and `all-minilm:latest` for embeddings. Install Ollama, then prepare both models:

```powershell
ollama pull gemma4:latest
ollama pull all-minilm:latest
ollama serve
```

If the Ollama desktop service is already running, do not start a second server.
Settings are in `src/Pulse.WebApi/appsettings.json`; environment overrides are
`Ollama__BaseUrl`, `Ollama__Model`, and `Ollama__EmbeddingModel`.
Only loopback HTTP/HTTPS inference endpoints are accepted. No cloud inference
or embedding service is used.

1. Create a survey, then open **Manage Questions** while it is in Draft.
2. In **Generate questions from documents**, attach up to five TXT, Markdown,
   PDF, or DOCX files. Each file is limited to 5 MB and 50,000 extracted
   characters. Scanned PDFs need OCR before upload; password-protected PDFs
   are not supported. DOCX extraction reads body paragraphs, not embedded files.
3. Optionally enter additional grounding information/instructor focus
   (10,000 characters maximum). Free text alone is also supported.
4. Request 1-20 multiple-choice questions and generate a preview.
5. Review all choices, correct answers, explanations, and quoted source
   passages. Approve the batch to add it to the survey, or discard/regenerate.
   Saved questions can be edited using the existing question editor.

Documents, original bytes, and extracted text are persisted in local LiteDB.
Embeddings are computed locally during generation. Retrieval selects up to
12 relevant overlapping passages, including material from every attachment;
it does not promise exhaustive coverage of every document. Changing saved
grounding invalidates previews; previews expire after one hour. Approving a
preview is transactional and cannot insert that batch twice.

Generation has a five-minute inference timeout and does not automatically
retry POST requests. Missing models, invalid output, unsupported documents,
and insufficient generated question counts are reported rather than silently
substituted. Quoted-source validation does not prove factual correctness:
instructor review remains mandatory. Answer keys are stored for later
correct-answer reveal; the student results UI currently still shows aggregates
only.

The PDF text extractor adds the `PdfPig` dependency. DOCX, text, and Ollama HTTP
handling use existing .NET libraries.

Run deterministic feature tests:

```powershell
dotnet test .\src\Pulse.Tests\Pulse.Tests.csproj -c Release --filter "FullyQualifiedName~SurveyGeneration|FullyQualifiedName~SurveyGrounding|FullyQualifiedName~OllamaSurveyGenerator" '-bl:rag-tests-{}.binlog'
```

The real-inference smoke test is skipped by default. To run it against local
Ollama using synthetic biology material:

```powershell
$env:PULSE_OLLAMA_SMOKE = "1"
dotnet test .\src\Pulse.Tests\Pulse.Tests.csproj -c Release --filter "FullyQualifiedName~LocalOllamaSmoke" '-bl:rag-smoke-{}.binlog'
Remove-Item Env:\PULSE_OLLAMA_SMOKE
```

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
