# API Reference

This document describes the currently implemented HTTP API surface in Pulse.WebApi.

Base URL (local, default):

- http://localhost:5062
- https://localhost:7081

When AppHost dev tunnels are enabled, use the **WebApi public HTTPS tunnel URL**
shown by AppHost as the base instead. Routes and existing instructor authorization
are unchanged. See [tunnel configuration](onboarding.md#7-persistent-anonymous-dev-tunnels).
OpenAPI server URLs also use that public API origin in tunnel mode.
The separate Scalar application fetches documents and executes requests directly
against that origin. Only its configured tunnel origin receives CORS permission,
and only in Development tunnel mode; instructor authorization remains required.

## Authentication and Headers

Some endpoints are protected by instructor middleware and require this header:

- InstructorCode: your configured instructor code

If missing or invalid on protected routes:

- 401 Unauthorized when header is missing
- 403 Forbidden when header is invalid

## Health

### GET /

Simple API health text.

Response 200:

```text
Pulse API is running
```

## Portable JSON import and export

Use **Export JSON** on survey/form cards or on the question-bank and survey-question
pages. Use the corresponding **Import** file picker to create a copy. Question-bank
exports include the entire bank (not just the currently filtered page); individual
questions also have an export control. JSON definitions are separate from the
existing form response CSV export and database backup.

All routes below require a valid `InstructorCode` header. Survey/form exports and
imports into an existing survey also check ownership. Files use UTF-8 JSON, camelCase
property names, case-sensitive string type names, and a versioned envelope.

| Method | Route | Behavior |
| --- | --- | --- |
| GET | `/api/content/surveys/{id}` | Download one survey with ordered questions and answer keys |
| GET | `/api/content/forms/{id}` | Download one form with ordered fields and Markdown source |
| GET | `/api/content/questions` | Download all bank questions, including moderation statuses |
| GET | `/api/content/questions?questionId={id}` | Download one bank question |
| GET | `/api/content/questions?sessionId={id}` | Download ordered survey questions |
| GET | `/api/content/questions?sessionId={id}&questionId={questionId}` | Download one question from that survey |
| POST | `/api/content/import/survey` | Create a new draft survey |
| POST | `/api/content/import/form` | Create a new draft form |
| POST | `/api/content/import/questions` | Create new question-bank items |
| POST | `/api/content/import/questions?sessionId={id}` | Append questions to an owned draft survey |

POST bodies are the JSON file itself (`Content-Type: application/json`), not multipart
uploads. Every export is directly importable using the matching kind. New surveys,
forms, questions, and form fields receive fresh IDs; new surveys/forms receive fresh
join codes and belong to the importing instructor. Existing content is never replaced.
Responses, participant data, instructor credentials, live/reveal state, and timestamps
are not transferred. Source IDs/quotes and explanations are preserved, but document
grounding uploads themselves are not included. Bank course/topic/tags, trivia
eligibility, draft state, and moderation status are preserved for bank imports.
Tags also travel with survey questions; other bank-only properties do not apply
when questions are appended to a survey. Draft bank questions cannot be imported
into a survey until reviewed and approved. Imported bank
questions are attributed to the instructor, not to the original contributor.

Files must be at most **2 MiB**. Exports over that limit return 413 instead of producing
a file that cannot be reimported; export questions individually to split a large bank
or survey. An entire file is validated before saving and all writes share a database
transaction, so invalid files or write failures do not leave partially imported content.
Repeated successful imports intentionally create additional copies.

Successful imports return 200:

```json
{ "kind": "survey", "id": "11111111-1111-1111-1111-111111111111", "importedCount": 4 }
```

`id` is the new survey/form ID or the destination survey ID; it is null for bank
imports. `importedCount` counts questions or form fields. Errors: 400 for malformed,
unsupported-version, invalid-definition, unknown-property, or wrong-kind files; 401/403
for authentication/ownership failures; 404 for missing targets; 409 for non-draft
survey destinations; 413 for oversized files. Error responses include a description
when a file fails validation. Question files cannot be imported as form fields.

Bank imports also return `importBatchId`. Each successful bank import creates a
distinct, persistent batch with its UTC timestamp, original question count, and
filename. Send the optional `X-Import-Filename` header as a URI-escaped filename
(the instructor UI does this automatically). Paths are stripped and decoded names
must be at most 255 characters without control characters; omitted names default
to `questions.json`. Batch metadata and questions commit or roll back together.
Import IDs are local provenance, not portable JSON properties: exporting and
reimporting questions creates a new batch, not a link to the original batch.
Survey/form imports and questions appended directly to surveys do not create bank
import batches. Existing questions remain usable but cannot be retroactively
attributed to an import. CSV preview validates only; it does not persist a batch.

### Expected question JSON schema

The machine-readable [JSON Schema](schemas/questions.schema.json) and a ready-to-import
[example question file](examples/questions.json) describe the supported format:

```json
{
  "format": "pulse",
  "version": 1,
  "kind": "questions",
  "questions": [
    {
      "text": "Which protocol is used for web requests?",
      "type": "MultipleChoice",
      "options": ["HTTP", "SMTP"],
      "correctAnswers": ["HTTP"]
    }
  ]
}
```

Each question requires `text` and `type`. Supported authoring types are
`MultipleChoice`, `MultipleAnswer`, `TrueFalse`, and `Matching`. Choice types need
at least two nonblank options. Matching needs at least two complete
`{"prompt":"...","match":"..."}` pairs with unique prompts (case-insensitive after
trimming). Legacy `LegacyLikertScale`/`LegacyOpenEnded` (and their old aliases
`LikertScale`/`OpenEnded`) can be transferred for compatibility, but remain unavailable
in authoring editors. Type names and property names are case-sensitive; numeric
question types are not accepted.

Optional question properties: `options`, `matchingPairs`, `correctAnswers`,
`explanation`, `sourceId`, `sourceQuote`, `course`, `topic`, `tags`, `triviaEligible`,
and `moderationStatus` (`Approved`, `Pending`, or `Rejected`; defaults to `Approved`).
Omitted lists default to empty and omitted text metadata to empty strings; null lists,
null metadata, null array entries, and blank list values are rejected. Trivia-eligible
questions require a nonempty answer key whose values exactly match available options
(or `True`/`False`); matching questions cannot be trivia-eligible. Unique matching
prompts and answer membership are checked by the API in addition to JSON Schema.

### Survey and form envelopes

Survey files use `kind: "survey"`, require `title`, and contain the same ordered
`questions` array as question files:

```json
{
  "format": "pulse", "version": 1, "kind": "survey",
  "title": "Networking review",
  "questions": [
    { "text": "HTTP is a protocol.", "type": "TrueFalse" }
  ]
}
```

Form files use `kind: "form"`, require `title` and ordered `fields`, and may include
`description` (up to 5000 characters):

```json
{
  "format": "pulse", "version": 1, "kind": "form",
  "title": "Project interest", "description": "## Tell us about yourself",
  "fields": [
    { "label": "Email", "fieldType": "Email", "required": true },
    { "label": "Track", "fieldType": "Dropdown", "options": ["Web", "Mobile"] }
  ]
}
```

Form fields require `label` and `fieldType`; optional properties are `required`,
`placeholder`, and `options`. Field types: `Text`, `TextArea`, `Email`, `Phone`,
`Number`, `Date`, `Dropdown`, `YesNo`, and `Checkbox`. A form supports at most 50 fields.
Dropdowns need at least one nonblank option. Normal form-definition normalization
applies: labels/placeholders are trimmed and dropdown options are trimmed and
deduplicated; non-dropdown options are implicit rather than stored.

## Sessions

Session payloads use string enum values:

- `mode`: `Survey` or `Trivia`
- `status`: `Draft`, `Active`, or `Closed`
- `resultsVisibility`: `Blinded` or `Revealed`

Existing LiteDB records with no mode are treated as `Survey`.

### POST /api/sessions

Create a new draft session.

Protected: Yes (InstructorCode required)

Request body:

```json
{
	"title": "Week 5 Quick Check"
}
```

Response 201:

```json
{
	"id": "11111111-1111-1111-1111-111111111111",
	"joinCode": "ABC123",
	"instructorCode": "INSTRUCTOR001"
}
```

### GET /api/sessions/{id}

Get session details by id.

Protected: Yes (InstructorCode required)

Response codes:

- 200 OK
- 401 Unauthorized
- 403 Forbidden
- 404 Not Found

### GET /sessions

List sessions for the current instructor.

Protected: Yes (InstructorCode required)

Response 200: array of session objects.

### DELETE /api/sessions/{id}

Permanently delete a session owned by the current instructor. The session's
questions, submitted responses, attached grounding documents, and generation
previews are deleted in the same transaction.

Protected: Yes (InstructorCode required)

Response codes:

- 204 No Content
- 401 Unauthorized
- 403 Forbidden
- 404 Not Found

### GET /api/sessions/join/{joinCode}

Public student join lookup by join code. Only Active surveys can be joined.

Protected: No

Response 200:

```json
{
	"title": "Biology Quiz - Chapter 5"
}
```

Response errors:

- 400 when join code is missing/empty
- 409 when the survey is Draft or Closed
- 404 when no session exists for the code

### GET /api/sessions/qr/{joinCode}

Generate a PNG QR code for a join code.

The payload is `<App:JoinBaseUrl>/join/<joinCode>`. AppHost supplies the public
**WebApp** tunnel origin in tunnel mode, not the API tunnel origin. With no
configured join base, the API retains its request-origin fallback.

Protected: No

Response 200:

- Content-Type: image/png

The encoded target is
`{App:JoinBaseUrl}/join/{joinCode}` when `App:JoinBaseUrl` is configured;
otherwise it uses the API request origin. Aspire mobile testing sets this from
`Pulse:PublicJoinBaseUrl`. Use an absolute public HTTPS student-app URL—never
the API/Admin URL or a localhost URL—before generating a QR for another device.

### GET /sessions/{id}/qr

Generate a PNG QR code for a session id.

This route uses the same configured join origin as the join-code QR route. The
WebApp QR component displays that origin too and fetches the PNG through its
API client; it renders a data URI rather than exposing a separate image host.

Protected: Yes (InstructorCode required)

Response 200:

- Content-Type: image/png

This endpoint uses the same `App:JoinBaseUrl` behavior as the join-code route.

### PUT /api/sessions/{id}/unblind

Set session `resultsVisibility` to `Revealed`.

Protected: Yes (InstructorCode required)

Response codes:

- 200 OK (updated session)
- 401 Unauthorized
- 403 Forbidden
- 404 Not Found

### PUT /api/sessions/{id}/activate

Transition an owned Draft survey to Active.

Protected: Yes (InstructorCode required)

Response codes:

- 200 OK (updated session)
- 401 Unauthorized
- 403 Forbidden
- 404 Not Found
- 409 Conflict (invalid lifecycle transition)

### PUT /api/sessions/{id}/close

Transition an owned Active survey to Closed.

Protected: Yes (InstructorCode required)

Response codes:

- 200 OK (updated session)
- 401 Unauthorized
- 403 Forbidden
- 404 Not Found
- 409 Conflict (invalid lifecycle transition)

## Survey submissions and results

The V1 contracts define these rules:

- A whole-survey submission contains a device id and a collection of answers.
- Each answer contains one question id and a list of selected values.
- Multiple-answer selections are one atomic answer, not separate response records.
- Persistence must maintain at most one current response per device and question; changing an answer replaces the prior value.
- Instructor results include unique participant and completed-submission counts.
- A blinded student result-status response contains visibility only and cannot contain aggregate tallies.

### POST /api/sessions/{sessionId}/submit

Submit all questions for an Active survey in one atomic request. If the instructor
has shared a single question, submit that question's answer; subsequent shared
questions can be submitted separately and accumulate under the same anonymous device.

Protected: No

Request body:

```json
{
	"deviceId": "7ed9232b-e5ac-4588-8690-8cc50e4c650c",
	"answers": [
		{
			"questionId": "22222222-2222-2222-2222-222222222222",
			"values": ["Option A", "Option C"]
		}
	]
}
```

Each question in the current submission must appear exactly once. Single-choice
and true/false answers contain one value; multiple-answer responses contain the
selected option set; matching responses contain one selected match per prompt in
prompt order.
The API validates the complete payload before writing. Repeated submissions
from the same device replace its current answers and completion marker rather
than adding duplicate rows.

Response codes:

- 200 OK
- 400 Bad Request (invalid device, incomplete survey, or invalid answer)
- 404 Not Found
- 409 Conflict (session is not Active)

### GET /api/sessions/{id}/results

Get aggregated results for all questions in a session.

Tallies include every configured option in authored order, including zero counts.
True/false questions include both choices; matching questions include each
prompt/choice combination. Counts represent distinct students selecting an option.
Both instructor monitoring and student-revealed results display each count and
its percentage of students who answered that question (0% before any responses).
Multi-select percentages may total more than 100%. Correct options are marked
inline using `correctAnswers`, without a second copy of the answer key.

Protected: Yes (InstructorCode required)

Response 200 shape:

```json
{
	"sessionId": "11111111-1111-1111-1111-111111111111",
	"title": "Week 5 Quick Check",
	"participantCount": 10,
	"completedCount": 9,
	"questions": [
		{
			"questionId": "22222222-2222-2222-2222-222222222222",
			"text": "How confident are you with this topic?",
			"type": "MultipleChoice",
			"tallies": [
				{ "value": "Low", "count": 2 },
				{ "value": "High", "count": 8 }
			],
			"responseCount": 10
		}
	]
}
```

### GET /api/sessions/{id}/results/student

Return only `resultsVisibility` while no results have been revealed. After the
instructor reveals an individual question, return aggregate tallies for revealed
questions only. The existing session-wide reveal action returns all question
results. This endpoint never returns participant identifiers or individual answers.

### Per-question sharing and results

| Method | Route | Behavior |
| --- | --- | --- |
| PUT | `/api/sessions/{sessionId}/questions/{questionId}/share` | Share one question with students; replaces the previously shared question |
| DELETE | `/api/sessions/{sessionId}/questions/shared` | Stop sharing a single question; students return to the full survey |
| PUT | `/api/sessions/{sessionId}/questions/{questionId}/reveal` | Reveal aggregate results for one question |

These instructor routes require `InstructorCode` and ownership of the survey.
Sharing is available only while a survey is Active. Until a question is selected,
the existing full-survey flow remains unchanged. In staged mode, each submitted
answer is retained while students receive subsequent shared questions. Completed
survey counts increase after responses exist for every question.

Protected: No

### SignalR /hubs/session

Clients call `JoinSession(sessionId)` to join a session-scoped group. The hub
publishes `SubmissionUpdated`, `SessionClosed`, `ResultsRevealed`, and
`QuestionsUpdated` events.
The Blazor clients also poll the corresponding results endpoint so a temporary
WebSocket interruption does not leave the classroom view stale.

## Questions

### Student and instructor question contracts

`GET /questions` and `GET /api/sessions/{sessionId}/questions` return
student-safe projections without answer keys, explanations, or source quotes.
The session-specific route returns every question until the instructor shares one;
while a question is shared, it returns only that question.
For matching questions, `matchingPairs` contains prompts with empty `match`
values; `options` contains independently sorted match choices. Clients must
not interpret array alignment as the correct pairing.

`GET /api/sessions/{sessionId}/questions/instructor` requires the owning
instructor's code and returns full questions, including optional
`correctAnswers`, `explanation`, `sourceId`, and `sourceQuote`.
Question creation and updates accept optional answer keys; keys must match
the options. Single-choice and True/False questions have one correct answer
when an answer key is supplied. Feedback-only questions can omit the key.

## Document-grounded question generation

All routes below require `InstructorCode` and ownership of the survey.
Writes and generation additionally require Draft status.

| Method | Route suffix under `/api/sessions/{sessionId}` | Purpose |
|---|---|---|
| GET | `/grounding` | Return revision, additional context, and document metadata (not document contents). |
| PUT | `/grounding/context` | Save `{ "additionalContext": "..." }`, up to 10,000 characters. |
| POST | `/grounding/documents` | Append multipart document files; maximum five attachments total. |
| DELETE | `/grounding/documents/{documentId}` | Remove one attachment and invalidate previews. |
| POST | `/questions/generate` | Generate `{ "count": 5 }` using local Ollama; count must be 1-20. |
| PUT | `/questions/generated/{previewId}` | Save edited/selected preview questions as `{ "questions": [...] }` before approval. |
| POST | `/questions/generated/{previewId}/accept` | Approve the server-stored preview and atomically append its questions. |

Uploads support TXT/MD (UTF-8), text PDFs (maximum 100 pages), and DOCX body
paragraphs. Each file is limited to 5 MB and 50,000 extracted characters.
Invalid files reject the entire upload batch. Original bytes and extracted
text are stored in LiteDB; embeddings are computed locally at generation time.

Generation returns:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "questions": [
    {
      "id": "22222222-2222-2222-2222-222222222222",
      "text": "Which pigment absorbs light during photosynthesis?",
      "options": ["Chlorophyll", "Hemoglobin", "Melanin", "Keratin"],
      "correctAnswer": "Chlorophyll",
      "explanation": "The source identifies chlorophyll as the light-absorbing pigment.",
      "sourceId": "context:1",
      "sourceQuote": "Chlorophyll absorbs light.",
      "tags": ["biology"]
    }
  ]
}
```

Document source IDs use `{documentId without hyphens}:{chunkNumber}`; free-text
sources use `context:{chunkNumber}`. The model must supply four distinct
choices, one matching correct answer, and an exact quote from a retrieved
passage. The application rejects invalid citations and wrong question counts.
No survey questions are inserted by generation itself.

The instructor preview supports editing question text, adding, removing, and editing
individual multiline choices, the correct answer, explanation, and comma-separated
tags, plus removing unwanted questions. Multiple-choice options use A, B, C, D labels
in the student survey and authoring views. Text and choices render with the existing
safe Markdown renderer, including fenced code blocks; raw HTML and unsafe link
destinations are not rendered.
Stored/submitted answer values remain the original Markdown source, not rendered HTML.
Matching choices use radio groups rather than native dropdowns so Markdown and code
blocks can be displayed for every choice.

Preview updates must retain the generated question IDs, contain 1-20 distinct
questions, and provide at least two distinct nonempty choices with a matching
correct answer. Source IDs and quotes are preserved from the original server-side
preview, not overwritten by submitted edits. Approval uses the edited preview.
Individual **Save question to bank** and **Save as draft** actions store the edited
question, tags, answer key, explanation, and citation in the question bank and remove
it from the current preview. They do not add the question to the survey.

Approval returns an array of saved questions. Previews expire after one hour,
are replaced by a newer preview for the same survey, and are invalidated when
saved grounding changes. A second approval of the same preview returns 409.

Errors: 400 invalid document/input; 401 missing instructor code; 403 invalid
code or ownership; 404 missing survey/document; 409 non-Draft survey or stale
preview/grounding; 502 local inference failure, malformed model output, or
inference timeout. Request aborts cancel inference. No automatic inference
POST retries are configured.

## Moderated question bank

Question-bank items include `course`, `topic`, `tags`, `type`,
`moderationStatus`, `source`, `contributorAttribution`, `createdAt`, and
`updatedAt`. Existing bank records remain approved for compatibility.

- `POST /api/questionbank` creates an approved instructor-authored item, or an
  unfinished draft when `isDraft: true` is supplied. Drafts require question text
  and a supported type, but may have incomplete choices or trivia configuration.
  Optional `explanation`, `sourceId`, and `sourceQuote` preserve generated metadata.
- `POST /api/questionbank/suggestions` accepts an anonymous student suggestion
  and always stores it as `Pending`.
- `PUT /api/questionbank/{id}` lets an instructor edit and approve or reject an
  item. Supply `isDraft: true` to keep instructor work as a draft, or `isDraft: false`
  with `moderationStatus: 0` (Approved) to publish it after full validation.
  Student suggestions cannot be converted into instructor drafts.
- `GET /api/questionbank` supports `text`, `type`, `course`, `topic`, `tag`,
  `moderationStatus`, `source`, `sort`, `page`, `pageSize`, `isDraft`,
  `triviaEligible`, `importBatchId`, `correctAnswer`, `correctAnswerPosition`, and `option`.
  `isDraft=true` lists unfinished work. Approved queries exclude drafts unless
  explicitly filtering by `isDraft=true`. Tag matching is case-insensitive; tags
  are trimmed and deduplicated.

The Question Bank page offers a Draft filter, editable Markdown text and individual
choices, tags, answer keys, and **Save as draft** / **Approve and save** actions.
Drafts persist in LiteDB for later review and are separate from moderation status;
existing records default to non-draft without a migration.

Only approved, non-draft items can be imported into a draft survey or used for trivia. Student suggestions
cannot update or publish existing records.

CSV preview accepts the columns `Text`, `Type`, `Options`, `MatchingPairs`,
`Course`, `Topic`, and `Tags`. Use `|` between options and tags and encode
matching pairs as `Prompt=>Match`.

## Trivia

Trivia is stored separately from anonymous survey sessions. Instructor routes
require `InstructorCode`; join, player-state, and answer routes are public.

- `GET|POST /api/trivia` lists or creates games.
- `POST /api/trivia/{gameId}/teams` creates an instructor-defined team.
- `POST /api/trivia/{gameId}/questions` copies an approved, trivia-eligible
  bank item into the game.
- `GET|POST /api/trivia/join/{joinCode}` returns join information or joins a
  player. Reusing a device ID reconnects to the original participant.
- `PUT /api/trivia/{gameId}/questions/{questionId}/open` starts the
  server-authoritative timer.
- `POST /api/trivia/{gameId}/questions/{questionId}/answers` accepts one
  answer per participant and calculates correctness, base points, and time
  bonus from the server receipt time.
- `PUT /api/trivia/{gameId}/question/close` and
  `PUT /api/trivia/{gameId}/finish` advance the game.
- `GET /api/trivia/{gameId}/state/{participantId}` returns the player-safe
  question and leaderboard without correct-answer data.
- `GET /api/trivia/{gameId}/control` returns instructor game controls and
  state.

Instructor and player pages poll live state so reconnecting clients recover
without trusting client clocks or previously cached leaderboard values.

### GET /api/sessions/{sessionId}/questions

Get ordered questions for a specific session.

Protected: No (student-safe projection)

Response codes:

- 200 OK (array of student-safe questions ordered by sortOrder)

### POST /api/sessions/{sessionId}/questions

Create a question scoped to a specific session.

Protected: Yes (InstructorCode required). The session must be owned by the
instructor and remain in Draft.

Request body (example):

```json
{
	"text": "What is 2 + 2?",
	"type": 0,
	"options": ["3", "4", "5"],
	"sortOrder": 0
}
```

Response codes:

- 201 Created (created question)
- 400 Bad Request (validation errors)
- 401 Unauthorized
- 403 Forbidden
- 404 Not Found
- 409 Conflict (session is not Draft)

### GET /questions

Get all questions using the student-safe contract described above.

Protected: No

### PUT /questions/{id}

Update question text and type-specific data while its survey remains in Draft.

Protected: Yes (InstructorCode required)

Request body:

```json
{
	"text": "Updated prompt",
	"type": 0,
	"options": ["Option A", "Option B"],
	"matchingPairs": []
}
```

Response codes:

- 200 OK
- 400 Bad Request
- 401 Unauthorized
- 403 Forbidden
- 404 Not Found
- 409 Conflict (session is not Draft)

### DELETE /questions/{id}

Delete a question.

Protected: Yes (InstructorCode required)

Response codes:

- 204 No Content
- 400 Bad Request
- 401 Unauthorized
- 403 Forbidden
- 404 Not Found
- 409 Conflict (session is not Draft)

### PUT /api/questions/reorder

Reorder questions by providing ordered question ids.

Protected: Yes (InstructorCode required). All questions must belong to the
same owned Draft survey.

Request body:

```json
{
	"questionIds": [
		"22222222-2222-2222-2222-222222222222",
		"33333333-3333-3333-3333-333333333333"
	]
}
```

Response codes:

- 200 OK (updated ordered questions)
- 400 Bad Request
- 401 Unauthorized
- 403 Forbidden
- 404 Not Found
- 409 Conflict (session is not Draft)

Supported authored question types:

- `0` / `MultipleChoice`: at least two options.
- `3` / `MultipleAnswer`: at least two options.
- `4` / `TrueFalse`: no authored options.
- `5` / `Matching`: at least two unique prompts with complete
  `prompt`/`match` pairs.

Persisted legacy type values `1` and `2` remain readable but cannot be newly
authored or imported.

## Question Bank

### POST /api/questionbank

Create a reusable question bank item.

Protected: Yes (InstructorCode required)

Request body:

```json
{
	"text": "Which concept needs review?",
	"type": 0,
	"options": ["Concept A", "Concept B"]
}
```

Response codes:

- 201 Created
- 400 Bad Request

### GET /api/questionbank

List/search question bank items.

Protected: Yes (InstructorCode required)

Query params:

- text (optional)
- type (optional, int)
- course, topic, tag (optional, case-insensitive exact match)
- moderationStatus (optional: Approved, Pending, Rejected)
- source (optional: Legacy, InstructorAuthored, StudentSuggestion, CsvImport, JsonImport)
- isDraft, triviaEligible (optional booleans)
- importBatchId (optional, non-empty GUID)
- correctAnswer (optional, case-insensitive substring in any correct answer)
- option (optional, case-insensitive substring in any answer option)
- correctAnswerPosition (optional, 1-based position of any correct answer; 1 = A,
  2 = B, etc.; True/False uses True at 1 and False at 2)
- sort (optional: Newest, Oldest, Text)
- page (optional, default 1)
- pageSize (optional, default 20)

All supplied filters are combined with AND. Multiple-answer questions match any
correct answer at the requested position. Missing/out-of-range answer positions
do not match; positions below 1 and invalid enum/empty import ID values return 400.
Question text also uses case-insensitive substring matching.
The bank and trivia picker both support pagination and these filters. The trivia
picker always adds `moderationStatus=Approved&isDraft=false&triviaEligible=true`;
filtering or clearing filters cannot broaden this eligibility requirement.

Response 200:

```json
{
	"items": [
		{
			"id": "44444444-4444-4444-4444-444444444444",
			"text": "Which concept needs review?",
			"type": 0,
			"options": ["Concept A", "Concept B"],
			"createdAt": "2026-04-22T00:00:00Z"
		}
	],
	"totalCount": 1
}
```

### GET /api/questionbank/imports

Protected: Yes (InstructorCode required)

Returns an array ordered by newest import first. Each entry contains `id`,
`fileName`, `importedAt` (UTC), `importedCount`, and `remainingCount`. History remains
after all associated questions are deleted. New JSON imports use source
`JsonImport` (numeric value 4); existing source values are unchanged.

### POST /api/questionbank/delete/preview

Protected: Yes (InstructorCode required)

Submit a JSON filter using the same property names and semantics as the list query,
except pagination is not supported. At least one nonblank filter is required;
sorting alone is not a filter. Enum values in JSON use their numeric values.

```json
{ "correctAnswerPosition": 1, "source": 4 }
```

Returns 200 with `{ "questionIds": ["..."] }` for **all** matches across pages
(possibly an empty list). The UI displays the count and an explicit permanent
deletion confirmation. To review a complete prior import, use `importBatchId`
alone; combining it with other filters deletes only the matching subset.

### POST /api/questionbank/delete

Protected: Yes (InstructorCode required)

Submit the applied filter and the exact non-empty list from the preview:

```json
{
  "filter": { "correctAnswerPosition": 1, "source": 4 },
  "expectedIds": ["44444444-4444-4444-4444-444444444444"]
}
```

Returns 200 with `{ "deletedCount": 1 }`. Missing/invalid filters, missing or empty
confirmation lists, and duplicate/empty IDs return 400. If the matching ID set has
changed since preview, returns 409 and deletes nothing; preview again to continue.
Deletion is transactional: a write failure rolls back the entire operation.
Only reusable bank items are deleted, not copies already placed in surveys or
trivia games. Deletion is permanent; import history is retained with updated
remaining counts.

### POST /api/questionbank/import/preview

Preview and validate CSV content before import.

Protected: Yes (InstructorCode required)

Expected request: multipart/form-data with a CSV file. Columns are
`text,type,options,matchingPairs`. Options use `|` separators. Matching pairs
use `Prompt=>Match|Prompt=>Match`. Type accepts the four names above or their
numeric values.

Response codes:

- 200 OK (rows + row-level validation issues)
- 400 Bad Request

### POST /api/sessions/{sessionId}/questions/import

Import validated question bank content into a session.

Protected: Yes (InstructorCode required). The destination survey must be an
owned Draft survey, and every bank item must use a supported authored type.

Response codes:

- 200 OK
- 400 Bad Request

## Admin

### GET /api/admin/export-db

Export the current LiteDB database file.

Protected: Yes (InstructorCode required)

Response 200:

- Database file stream download

## Notes

- OpenAPI is available in development environments.
- Some routes currently use a mix of /api-prefixed and non-prefixed paths.
- Endpoint behavior is documented from current implementation in Pulse.WebApi.