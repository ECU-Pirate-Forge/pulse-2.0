# Pulse — Architecture

---

## Solution Architecture

This diagram is the target deployment topology for Pulse. The repository has separate WebApp, Admin, WebApi, and Aspire projects, and instructor session/question/QR workflows now live in Admin with compatibility redirects from their former WebApp routes. SignalR is not implemented yet; the approved redesign will add the dashed/planned real-time behavior without breaking existing join links.

```mermaid
graph TD
    subgraph Clients
        IB["🖥️ Instructor Browser\n(Desktop — MudBlazor pages)"]
        SB["📱 Student Browser\n(Mobile / Laptop — Student pages)"]
    end

    subgraph Aspire["Aspire AppHost (Orchestrator)"]
        Dashboard["📊 Aspire Dashboard\n(Telemetry & Health)"]

        subgraph Web["Web Project (ASP.NET Core / Blazor Server)"]
            direction TB

            subgraph Frontend["MudBlazor Blazor Server Pages"]
                InstructorUI["Instructor UI\n(Session mgmt, Questions, Results)"]
                StudentUI["Student UI\n(Join, Respond, Results view)"]
            end

            subgraph API["ASP.NET Core Minimal API Endpoints"]
                SessionAPI["Session endpoints\n(POST /api/sessions, GET /sessions, GET /api/sessions/{id}, PUT /api/sessions/{id}/unblind)"]
                QuestionAPI["Question endpoints\n(/questions and /api/sessions/{sessionId}/questions)"]
                ResponseAPI["Survey submission and results\n(POST …/submit, GET …/results)"]
                QrAPI["QR endpoints\n(GET /api/sessions/qr/{joinCode}, GET /sessions/{id}/qr)"]
            end

            Hub["🔌 SignalR Hub\n(/hubs/session)\nSessionHub — group management"]

            subgraph Services["Domain Services"]
                SessionSvc["SessionService"]
                QuestionSvc["QuestionService"]
                ResponseSvc["ResponseService"]
                QrSvc["QrCodeService"]
            end

            subgraph Repos["LiteDB Repositories"]
                SessionRepo["SessionRepository"]
                QuestionRepo["QuestionRepository"]
                ResponseRepo["ResponseRepository"]
                BankRepo["QuestionBankItemRepository"]
            end

            LiteDB[("🗄️ LiteDB\n(Embedded file DB)")]
            QRCoder["📦 QRCoder library"]
        end

        FoundryLocal["🤖 Foundry Local\n(AI Inference Service — separate process)"]
    end

    %% Client <-> Web
    IB -- "HTTPS + WebSocket (SignalR)" --> Web
    SB -- "HTTPS + WebSocket (SignalR)" --> Web

    %% Frontend -> API / Hub
    InstructorUI --> API
    StudentUI --> API
    InstructorUI --> Hub
    StudentUI --> Hub

    %% API -> Services
    SessionAPI --> SessionSvc
    QuestionAPI --> QuestionSvc
    ResponseAPI --> ResponseSvc
    QrAPI --> QrSvc

    %% Services -> Repos
    SessionSvc --> SessionRepo
    QuestionSvc --> QuestionRepo
    QuestionSvc --> BankRepo
    ResponseSvc --> ResponseRepo
    ResponseSvc --> Hub

    %% Repos -> LiteDB
    SessionRepo --> LiteDB
    QuestionRepo --> LiteDB
    ResponseRepo --> LiteDB
    BankRepo --> LiteDB

    %% QrCodeService -> QRCoder
    QrSvc --> QRCoder

    %% Aspire Dashboard monitors Web & Foundry
    Dashboard -. "telemetry / health" .-> Web
    Dashboard -. "telemetry / health" .-> FoundryLocal
```

---

## Route compatibility

The application split must preserve existing links while establishing clear ownership:

- `Pulse.WebApp` owns the canonical student route `/join/{joinCode}`.
- Existing `/quiz/{joinCode}` links redirect to `/join/{joinCode}` during the transition.
- Existing `/instructor` and `/instructor/*` WebApp routes redirect to their `Pulse.Admin` equivalents after those pages move.
- QR codes always use the configured public WebApp base URL, never the API or Admin URL.
- Redirects remain until usage data and release notes support their removal.

## Persistence compatibility

LiteDB session documents retain their existing stored field names while application code uses typed concepts:

- Existing string `Status` values map to `SessionStatus`; unknown or missing values safely default to `Draft`.
- A missing `Mode` defaults to `Survey`, so existing sessions are never interpreted as trivia.
- Existing `IsUnblinded` values map to typed `ResultsVisibility`.
- Draft-to-Active and Active-to-Closed are the only valid lifecycle transitions.
- Future schema changes must include repository tests that read representative legacy documents.

## Target Activity Flow

This sequence diagram describes the approved target classroom workflow, not the current implementation. In particular, activation/closure endpoints, SignalR events, atomic whole-survey submission, and student-side result reveal still need to be implemented.

```mermaid
sequenceDiagram
    actor IB as Instructor Browser
    participant BZ as Blazor Server
    participant API as ASP.NET Core API
    participant SH as SignalR Hub
    participant DB as LiteDB
    actor SB as Student Browser

    %% ── 1. SESSION SETUP ──────────────────────────────────────────────────────
    rect rgb(235, 245, 255)
        Note over IB,DB: 1 — Session Setup
        IB->>API: POST /api/sessions {title}
        API->>DB: Insert Session (Draft)
        DB-->>API: Session {id, JoinCode, InstructorCode}
        API-->>IB: 201 {sessionId, JoinCode, InstructorCode}

        IB->>API: POST /api/sessions/{id}/questions {text, type, options}
        API->>DB: Insert Question
        DB-->>API: Question {id}
        API-->>IB: 201 {questionId}

        IB->>API: PUT /api/sessions/{id}/activate
        API->>DB: Update Session → Active
        DB-->>API: ok
        API->>SH: Broadcast SessionActivated to session group
        API-->>IB: 200 {status: Active}
    end

    %% ── 2. STUDENT JOIN ───────────────────────────────────────────────────────
    rect rgb(235, 255, 240)
        Note over IB,SB: 2 — Student Join
        SB->>BZ: GET /join/{joinCode}
        BZ->>DB: GetByJoinCode({joinCode})
        DB-->>BZ: Session + Questions
        BZ-->>SB: Render student response page

        SB->>SH: Connect + JoinSession(joinCode)
        SH-->>SB: Joined session group

        Note over SB: deviceId = localStorage UUID\n(generated on first visit, reused thereafter)
    end

    %% ── 3. RESPONSE SUBMISSION ────────────────────────────────────────────────
    rect rgb(255, 248, 230)
        Note over IB,SB: 3 — Response Submission
        SB->>API: POST /api/sessions/{id}/submit\n{deviceId, answers[]}
        API->>DB: Atomically upsert responses and completion marker
        DB-->>API: ok
        API->>DB: Aggregate tallies for question
        DB-->>API: Updated tallies
        API->>SH: Broadcast TallyUpdated {questionId, tallies} to session group
        API-->>SB: 200 ok

        SH-->>IB: TallyUpdated event
        Note over IB: Live tally chart updates
    end

    %% ── 4. UNBLINDING RESULTS ─────────────────────────────────────────────────
    rect rgb(255, 235, 235)
        Note over IB,SB: 4 — Unblinding Results
        IB->>API: PUT /api/sessions/{id}/unblind
        API->>DB: Set Session.IsUnblinded = true
        DB-->>API: ok
        API->>SH: Broadcast ResultsUnblinded to session group
        API-->>IB: 200 {isUnblinded: true}

        SH-->>SB: ResultsUnblinded event
        Note over SB: Student page shows aggregated results view
    end
```

---

## Deferred AI Question Generation

This sequence captures an earlier AI-assisted question-generation concept. It is deferred behind the approved survey redesign, moderated question bank, and trivia phases; Foundry Local is not currently registered in the AppHost.

```mermaid
sequenceDiagram
    actor IB as Instructor Browser
    participant API as ASP.NET Core API
    participant AiSvc as AiQuestionService
    participant FL as Foundry Local (AI)
    participant DB as LiteDB

    rect rgb(245, 235, 255)
        Note over IB,DB: Phase 2 — AI Question Generation
        IB->>API: POST /api/sessions/{id}/questions/generate\n{topic, count}
        API->>AiSvc: GenerateQuestions(topic, count)
        AiSvc->>FL: HTTP POST inference request\n(structured prompt with topic)
        FL-->>AiSvc: Raw model response (JSON / text)
        AiSvc-->>API: List<QuestionDto> (not yet persisted)
        API-->>IB: 200 [suggested questions]

        Note over IB: Instructor reviews, edits, approves or rejects each question

        IB->>API: POST /api/sessions/{id}/questions\n(approved questions, one by one or bulk)
        API->>DB: Insert approved Question(s)
        DB-->>API: ok
        API-->>IB: 201 questions persisted
    end
```
