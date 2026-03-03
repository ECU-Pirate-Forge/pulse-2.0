# Sprint 1 — Atomic Backlog

Sprint goal: Deliver the minimal backend and supporting client helpers to create sessions, join as a student, and submit responses with persistence.

Notes: Each PBI below is intentionally small and independently testable (one method, one service function, or one UI component).

### Session domain & infra (backend)

1. PBI: Create solution file and empty projects
    - User story: As a developer, I need a solution scaffold so I can add projects and run the host locally.
    - Description: Add solution file and create empty `AppHost`, `Web`, and `Shared` projects with minimal csproj files.
    - Acceptance Criteria:
       - `dotnet build` succeeds for the solution
    - Labels: feature-INFRA, area-infra

2. PBI: Add LiteDB NuGet reference to Web project
    - User story: As a developer, I want persistence available locally so I can persist sessions and responses.
    - Description: Add package reference and a configuration key for DB path.
    - Acceptance Criteria:
       - `Web` project references LiteDB
       - `appsettings.json` contains `LiteDb:Path`
    - Labels: feature-INFRA, area-backend

3. PBI: Add global error handling middleware
    - User story: As a developer, I want consistent error responses so clients receive stable API errors.
    - Description: Middleware that logs exceptions and returns generic 500 with correlation id.
    - Acceptance Criteria:
       - Middleware returns 500 + correlation id for an unhandled exception
       - Exception logged to console logger
    - Labels: feature-INFRA, area-backend

4. PBI: Register repository service in DI (single registration)
    - User story: As a developer, I want repositories registered in DI so code can request them.
    - Description: Register `ISessionRepository` implementation in the DI container.
    - Acceptance Criteria:
       - Service provider resolves `ISessionRepository` without error
    - Labels: feature-INFRA, area-backend

### Session persistence primitives

5. PBI: Implement `Session` model class
    - User story: As a developer, I want a `Session` model so session data has a schema.
    - Description: Add `Session` with Id, Title, InstructorCode, JoinCode, Status, CreatedAt, UpdatedAt.
    - Acceptance Criteria:
       - Model compiles and includes required fields
    - Labels: feature-SESSION, area-backend

6. PBI: Implement `SessionRepository.Insert(Session)`
    - User story: As a developer, I want to persist sessions so they can be retrieved later.
    - Description: Implement insert method that stores session in LiteDB.
    - Acceptance Criteria:
       - Inserted session can be retrieved by Id
    - Labels: feature-SESSION, area-backend

7. PBI: Implement `SessionRepository.GetByJoinCode(joinCode)`
    - User story: As a student, I want to lookup a session by join code so I can join.
    - Description: Add method to query session by JoinCode.
    - Acceptance Criteria:
       - Returns session when JoinCode exists; null/404 when not
    - Labels: feature-SESSION, area-backend

8. PBI: Implement JoinCode generator function
    - User story: As an instructor, I want a short JoinCode generated when creating a session.
    - Description: Implement a function that returns a 6-char alphanumeric code and handles collisions via repository check.
    - Acceptance Criteria:
       - Generated code is 6 chars and unique (checks repository for collision)
    - Labels: feature-SESSION, area-backend

### Session API endpoints (single-method PBIs)

9. PBI: POST /api/sessions — create session
    - User story: As an instructor, I want to create a session so students can join.
    - Description: Endpoint accepts { title } and returns session id, JoinCode, InstructorCode.
    - Acceptance Criteria:
       - Returns 201 with JoinCode and InstructorCode on valid request
       - Persists a Session in DB with Draft status
    - Labels: feature-SESSION, area-api

10. PBI: GET /api/sessions/{id} — get session (instructor view)
      - User story: As an instructor, I want to view my session details.
      - Description: Returns session details when provided valid session id and InstructorCode header.
      - Acceptance Criteria:
         - Returns 200 with session details when InstructorCode valid
         - Returns 401/403 when missing/invalid
      - Labels: feature-SESSION, area-api

11. PBI: GET /api/sessions/join/{joinCode} — student join lookup
      - User story: As a student, I want to look up active sessions by join code.
      - Description: Returns session title and question list when session is Active.
      - Acceptance Criteria:
         - Returns 200 with session info when Active
         - Returns 404 when join code invalid or session not Active
      - Labels: feature-SESSION, area-api

### Question primitives

12. PBI: Implement `Question` model class
      - User story: As a developer, I want a `Question` model to persist authoring data.
      - Description: Add `Question` with Id, SessionId, Text, Type, Options, SortOrder, CreatedAt.
      - Acceptance Criteria:
         - Model compiles and fields present
      - Labels: feature-QUESTION, area-backend

13. PBI: Implement `QuestionRepository.Insert(Question)`
      - User story: As an instructor, I want to save a question to my session.
      - Description: Repository insert for question document.
      - Acceptance Criteria:
         - Inserted question retrievable by SessionId
      - Labels: feature-QUESTION, area-backend

14. PBI: Validate MC question has ≥2 options (service function)
      - User story: As an instructor, I want validation so invalid questions are rejected.
      - Description: Create service function that validates question DTOs before persisting.
      - Acceptance Criteria:
         - Returns validation error for MC with <2 options; passes for valid input
      - Labels: feature-QUESTION, area-backend

### Response primitives

15. PBI: Implement `Response` model class
      - User story: As a developer, I want a `Response` model to store student answers.
      - Description: Add `Response` with Id, QuestionId, SessionId, DeviceId, Value, SubmittedAt.
      - Acceptance Criteria:
         - Model compiles and fields present
      - Labels: feature-RESPONSE, area-backend

16. PBI: Implement `ResponseRepository.UpsertByQuestionAndDevice(questionId, deviceId, value)`
      - User story: As a student, I want my latest submission to replace previous one.
      - Description: Upsert implementation that uses a compound index to replace prior responses from same device.
      - Acceptance Criteria:
         - Subsequent upsert by same device/question replaces previous value
      - Labels: feature-RESPONSE, area-backend

17. PBI: POST /api/sessions/{sessionId}/questions/{questionId}/respond
      - User story: As a student's device, I need an endpoint to submit a response with deviceId and value.
      - Description: Accepts JSON { deviceId, value } and upserts the response.
      - Acceptance Criteria:
         - Returns 200 on success with submittedAt timestamp
         - Returns 400 for missing/invalid deviceId or value
         - Returns 409 if session not Active
      - Labels: feature-RESPONSE, area-api

18. PBI: Validate DeviceId format (service function)
      - User story: As a developer, I want server-side DeviceId validation to protect data integrity.
      - Description: Implement a function that validates UUID format of deviceId used by the respond endpoint.
      - Acceptance Criteria:
         - Returns validation error for invalid UUIDs
      - Labels: feature-TRACKING, area-backend

### Frontend helper primitives

19. PBI: Client helper to generate/store DeviceId (JS module)
      - User story: As a student, I want a stable anonymous device id persisted in my browser.
      - Description: Small JS helper that generates a UUID on first visit and stores it in localStorage; exposes `getDeviceId()`.
      - Acceptance Criteria:
         - Returns the same UUID across reloads in same browser
         - Generates UUID when none exists
      - Labels: feature-TRACKING, area-frontend

20. PBI: Minimal student join page component (UI)
      - User story: As a student, I want a simple join page where I input a JoinCode.
      - Description: UI component with a text input and button that calls `GET /api/sessions/join/{joinCode}`.
      - Acceptance Criteria:
         - Input accepts code and shows session title on success
         - Shows error message when join fails
      - Labels: feature-SESSION, area-frontend

---

### Sprint 2 and Sprint 3 sections remain in the document; they will be refactored similarly on request.

Next step: I can continue and atomize Sprint 2 and Sprint 3 PBIs the same way — should I proceed to expand those now?

## Sprint 2 — Atomized Backlog

Sprint 2 goal: Deliver instructor and student UI flows, complete Question CRUD, results aggregation, and realtime updates so the system is usable end-to-end in a classroom.

Notes: Each item below is an atomic PBI (single API method, single service function, or single UI component). User stories and acceptance criteria are included.

### Question CRUD (API & service)

1. PBI: PUT /api/questions/{id} — update question
   - User story: As an instructor, I want to update a question so I can correct text or options.
   - Description: Endpoint to update a question's text, type and options; validates input.
   - Acceptance Criteria:
     - Returns 200 and updated question on valid update
     - Returns 400 on invalid payload (e.g., MC with <2 options)
   - Labels: feature-QUESTION, area-api

2. PBI: DELETE /api/questions/{id} — delete question
   - User story: As an instructor, I want to delete a question so I can remove it from a session.
   - Description: Endpoint to delete a question by id.
   - Acceptance Criteria:
     - Returns 204 on success
     - Returns 404 when question id not found
   - Labels: feature-QUESTION, area-api

3. PBI: PUT /api/sessions/{sessionId}/questions/reorder — reorder questions
   - User story: As an instructor, I want to reorder questions so the session flow is correct.
   - Description: Endpoint accepts ordered list of question IDs and updates SortOrder.
   - Acceptance Criteria:
     - Returns 200 on success and persisted SortOrder reflects request
     - Invalid IDs return 400
   - Labels: feature-QUESTION, area-api

### Question UI components

4. PBI: Question editor form component (UI)
   - User story: As an instructor, I want a form to create/edit questions.
   - Description: Component with Type selector, Text input, and options area; calls create/update endpoints.
   - Acceptance Criteria:
     - Renders fields for types and options
     - Submits to API and shows success/error
   - Labels: feature-QUESTION, area-frontend

5. PBI: Options list subcomponent
   - User story: As an instructor, I want to add/remove MC options quickly.
   - Description: Reusable UI control to add, edit, and remove option items.
   - Acceptance Criteria:
     - Allows adding/removing options and exposes current options to parent
   - Labels: feature-QUESTION, area-frontend

6. PBI: Question list component with drag handle (UI only)
   - User story: As an instructor, I want to visually reorder questions with drag handles.
   - Description: UI list that supports drag interactions; persistence via reorder endpoint is separate.
   - Acceptance Criteria:
     - Supports drag ordering in the UI and emits new order to caller
   - Labels: feature-QUESTION, area-frontend

### Instructor session management (UI)

7. PBI: Instructor session list API — GET /api/sessions?instructorCode=...
   - User story: As an instructor, I want to list my sessions to pick one to manage.
   - Description: Returns sessions for the instructor identified by InstructorCode.
   - Acceptance Criteria:
     - Returns 200 and array of sessions when InstructorCode valid
     - Returns 401/403 when missing/invalid
   - Labels: feature-SESSION, area-api

8. PBI: Instructor session list page component
   - User story: As an instructor, I want a page listing my sessions with actions.
   - Description: Page that calls the sessions list API and displays actions (Open, Activate, Close, Delete).
   - Acceptance Criteria:
     - Lists sessions and shows action buttons
     - Buttons call corresponding APIs
   - Labels: feature-SESSION, area-frontend

9. PBI: Session create dialog component (UI)
   - User story: As an instructor, I want a dialog to create a session and display codes.
   - Description: Dialog with Title input that calls POST /api/sessions and displays JoinCode/InstructorCode.
   - Acceptance Criteria:
     - Creates session and shows codes on success
     - Validates non-empty title
   - Labels: feature-SESSION, area-frontend

### Student join UX

10. PBI: Student join page route `/join/{joinCode}` (client behavior)
    - User story: As a student, I want a join URL that auto-loads the session.
    - Description: Route reads `joinCode` param and calls `GET /api/sessions/join/{joinCode}`.
    - Acceptance Criteria:
      - Auto-loads session info on route load
      - Shows error when join fails
    - Labels: feature-SESSION, area-frontend

11. PBI: Student response UI component (single-question view)
    - User story: As a student, I want a UI to submit an answer to a single question.
    - Description: Component renders question and options; posts to respond endpoint.
    - Acceptance Criteria:
      - Submits response and shows confirmation
      - Validates required fields before submit
    - Labels: feature-RESPONSE, area-frontend

### Results aggregation & realtime

12. PBI: Aggregation function — tally counts for MC/Likert
    - User story: As an instructor, I want tallies per option so I can see group trends.
    - Description: Service function that computes counts per option for a question.
    - Acceptance Criteria:
      - Returns correct counts given a set of Response records
    - Labels: feature-RESPONSE, area-backend

13. PBI: GET /api/sessions/{sessionId}/results — instructor aggregates
    - User story: As an instructor, I want an endpoint to fetch aggregated results.
    - Description: Returns per-question tallies and counts for the session (requires InstructorCode).
    - Acceptance Criteria:
      - Returns aggregation data and totals
      - Requires valid InstructorCode
    - Labels: feature-RESPONSE, area-api

14. PBI: SignalR server broadcast on tally update (integration)
    - User story: As an instructor, I want live updates when tallies change.
    - Description: After response upsert, server publishes `TallyUpdated` to session group with questionId and tallies.
    - Acceptance Criteria:
      - Broadcast sent after successful upsert for Active sessions
    - Labels: feature-REALTIME, area-realtime

15. PBI: SignalR client handler for `TallyUpdated` (instructor UI)
    - User story: As an instructor, I want the results page to update in real time without refresh.
    - Description: Client subscribes to hub and updates UI on `TallyUpdated` payloads.
    - Acceptance Criteria:
      - UI updates counts on event and indicates connection status
    - Labels: feature-REALTIME, area-frontend

### QR and small integrations

16. PBI: GET /api/sessions/{sessionId}/qr — QR image endpoint
    - User story: As an instructor, I want a QR image for my session so students can scan to join.
    - Description: Returns PNG encoding `https://host/join/{joinCode}`.
    - Acceptance Criteria:
      - Returns `image/png` with valid QR
      - Returns 404 for missing session
    - Labels: feature-QR, area-api

17. PBI: Display QR component on instructor session page
    - User story: As an instructor, I want to see a large QR on the session page.
    - Description: UI component that requests QR image endpoint and shows join URL text.
    - Acceptance Criteria:
      - Shows QR image and join text; refreshes when session activated
    - Labels: feature-QR, area-frontend

18. PBI: Apply InstructorCode middleware to question/session management endpoints
    - User story: As a developer, I want protected endpoints to require InstructorCode.
    - Description: Ensure middleware is applied to all instructor-only APIs.
    - Acceptance Criteria:
      - Missing InstructorCode returns 401; invalid returns 403
    - Labels: feature-AUTH, area-backend

---

## Sprint 3 — Atomized Backlog

Sprint 3 goal: Add import/question-bank features, results unblinding workflow, CI/DevOps improvements, telemetry, and admin tooling.

### Import & question bank

1. PBI: CSV parser function (per-row validation)
   - User story: As an instructor, I want CSV rows validated so I can fix errors before import.
   - Description: Library function that parses CSV and returns DTOs plus row-level errors.
   - Acceptance Criteria:
     - Returns array of parsed rows and validation errors with row numbers
   - Labels: feature-IMPORT, area-backend

2. PBI: POST /api/sessions/{sessionId}/questions/import — preview parse endpoint
   - User story: As an instructor, I want to upload a CSV and preview parsed rows before commit.
   - Description: Accepts multipart file and returns parsed rows with validation messages.
   - Acceptance Criteria:
     - Returns parsed preview and per-row validation results
   - Labels: feature-IMPORT, area-api

3. PBI: Import commit endpoint (from preview)
   - User story: As an instructor, I want to commit validated CSV rows to my session.
   - Description: Endpoint accepts validated payload and inserts questions.
   - Acceptance Criteria:
     - Inserts only valid rows and returns count + errors for failures
   - Labels: feature-IMPORT, area-api

4. PBI: QuestionBank model and repository
   - User story: As an instructor, I want to save reusable questions so I can reuse them.
   - Description: Add `QuestionBankItem` model and repository CRUD.
   - Acceptance Criteria:
     - Saved bank items retrievable and searchable by type/text
   - Labels: feature-QUESTION, area-backend

5. PBI: POST /api/questionbank — save question to bank
   - User story: As an instructor, I want to save a session question to my bank.
   - Description: API to create a bank item from a question DTO.
   - Acceptance Criteria:
     - Returns 201 and bank item id
   - Labels: feature-QUESTION, area-api

6. PBI: GET /api/questionbank — list/search bank items
   - User story: As an instructor, I want to browse my bank items to import them.
   - Description: Endpoint supports text/type filters and pagination.
   - Acceptance Criteria:
     - Returns filtered list and total count
   - Labels: feature-QUESTION, area-api

7. PBI: Question bank import endpoint (into session)
   - User story: As an instructor, I want to import selected bank items into current session.
   - Description: Accepts bank item ids and creates independent session questions.
   - Acceptance Criteria:
     - Imported questions are added to session and independent from bank edits
   - Labels: feature-QUESTION, area-api

### Results unblinding, admin, telemetry

8. PBI: PUT /api/sessions/{sessionId}/unblind — set IsUnblinded
   - User story: As an instructor, I want to unblind results so students can see aggregates.
   - Description: Endpoint toggles `IsUnblinded` on a session and emits SignalR event.
   - Acceptance Criteria:
     - Sets `IsUnblinded=true` and emits `ResultsUnblinded` to session group
   - Labels: feature-RESULTS, area-api

9. PBI: LiteDB export endpoint `/api/admin/export-db`
   - User story: As an admin, I want to download a DB export for debugging/backups.
   - Description: Returns a zipped export of the LiteDB file(s).
   - Acceptance Criteria:
     - Returns `application/zip` with DB export
     - Protected by a simple admin auth mechanism
   - Labels: feature-INFRA, area-api

10. PBI: Add structured logging for key events (service-level calls)
    - User story: As an operator, I want structured logs for create session/submit response events.
    - Description: Add ILogger calls with structured fields at service boundaries.
    - Acceptance Criteria:
      - Create session and response submission emit structured logs with correlation id
    - Labels: feature-INFRA, area-backend

11. PBI: CI workflow — run unit tests and lint on push
    - User story: As a developer, I want CI to run tests and lint to catch regressions early.
    - Description: Add GitHub Actions workflow that runs `dotnet test` and a linter.
    - Acceptance Criteria:
      - Workflow triggers on push and PR and reports status
    - Labels: feature-INFRA, area-devops

12. PBI: End-to-end automated test script (happy path)
    - User story: As a QA engineer, I want an automated test that verifies session create → student respond → instructor sees tally.
    - Description: Integration test or script that exercises API endpoints and asserts expected aggregates.
    - Acceptance Criteria:
      - Test run completes and asserts expected tallies
    - Labels: feature-RESPONSE, area-testing

---

Sprint 3 items focus on import, bank reuse, admin tooling, telemetry, CI, and end-to-end verification.

