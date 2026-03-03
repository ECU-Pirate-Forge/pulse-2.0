# Pulse — Architecture

---

## Solution Architecture

This diagram shows the full deployment topology of Pulse: how the Aspire AppHost orchestrates the Web server and Foundry Local, what lives inside the Web process, and how instructor and student browsers interact with it.

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
                SessionAPI["Session endpoints\n(POST /api/sessions, PUT activate/close/unblind)"]
                QuestionAPI["Question endpoints\n(POST/GET/PUT/DELETE /api/sessions/{id}/questions)"]
                ResponseAPI["Response endpoint\n(POST …/respond, GET …/results)"]
                QrAPI["QR endpoint\n(GET /api/sessions/{id}/qr)"]
                AIGenAPI["AI Generate endpoint\n(POST …/questions/generate)"]
            end

            Hub["🔌 SignalR Hub\n(/hubs/session)\nSessionHub — group management"]

            subgraph Services["Domain Services"]
                SessionSvc["SessionService"]
                QuestionSvc["QuestionService"]
                ResponseSvc["ResponseService"]
                QrSvc["QrCodeService"]
                AiSvc["AiQuestionService"]
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
    AIGenAPI --> AiSvc

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

    %% AiQuestionService -> Foundry Local
    AiSvc -- "HTTP inference request" --> FoundryLocal

    %% Aspire Dashboard monitors Web & Foundry
    Dashboard -. "telemetry / health" .-> Web
    Dashboard -. "telemetry / health" .-> FoundryLocal
```

---

## Activity Flow

This sequence diagram traces the core classroom workflow end-to-end: an instructor sets up a session, students join via QR code, submit responses, and the instructor views live tallies — culminating in the optional unblinding of results to students.

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
        SB->>API: POST /api/sessions/{id}/questions/{qid}/respond\n{deviceId, value}
        API->>DB: Upsert Response (by questionId + deviceId)
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

## Phase 2 — AI Question Generation

This short sequence shows the Phase 2 AI-assisted question generation flow, where an instructor enters a topic and Foundry Local returns suggested questions for review before they are persisted.

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
