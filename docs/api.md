# API Reference

This document describes the currently implemented HTTP API surface in Pulse.WebApi.

Base URL (local, default):

- http://localhost:5062
- https://localhost:7081

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
questions and submitted responses are deleted in the same transaction.

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

## Approved survey V1 contracts

The shared V1 contracts are defined for the survey redesign but are not all wired to HTTP endpoints yet. They establish these rules for subsequent phases:

- A whole-survey submission contains a device id and a collection of answers.
- Each answer contains one question id and a list of selected values.
- Multiple-answer selections are one atomic answer, not separate response records.
- Persistence must maintain at most one current response per device and question; changing an answer replaces the prior value.
- Instructor results include unique participant and completed-submission counts.
- A blinded student result-status response contains visibility only and cannot contain aggregate tallies.

### POST /api/sessions/{sessionId}/submit

Submit every answer for an Active survey in one atomic request.

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

Each question must appear exactly once. Single-choice and true/false answers
contain one value, multiple-answer responses contain the selected option set,
and matching responses contain one selected match per prompt in prompt order.
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

Return only `resultsVisibility` while a survey is blinded. After the instructor
reveals results, return the aggregate question tallies. This endpoint never
returns participant identifiers or individual answers.

Protected: No

### SignalR /hubs/session

Clients call `JoinSession(sessionId)` to join a session-scoped group. The hub
publishes `SubmissionUpdated`, `SessionClosed`, and `ResultsRevealed` events.
The Blazor clients also poll the corresponding results endpoint so a temporary
WebSocket interruption does not leave the classroom view stale.

## Questions

## Moderated question bank

Question-bank items include `course`, `topic`, `tags`, `type`,
`moderationStatus`, `source`, `contributorAttribution`, `createdAt`, and
`updatedAt`. Existing bank records remain approved for compatibility.

- `POST /api/questionbank` creates an approved instructor-authored item.
- `POST /api/questionbank/suggestions` accepts an anonymous student suggestion
  and always stores it as `Pending`.
- `PUT /api/questionbank/{id}` lets an instructor edit and approve or reject an
  item.
- `GET /api/questionbank` supports `text`, `type`, `course`, `topic`, `tag`,
  `moderationStatus`, `source`, `sort`, `page`, and `pageSize`.

Only approved items can be imported into a draft survey. Student suggestions
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

Protected: No (current implementation)

Response codes:

- 200 OK (array of question objects ordered by sortOrder)

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

Get all questions.

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

Protected: No

Query params:

- text (optional)
- type (optional, int)
- page (optional, default 1)
- pageSize (optional, default 20)

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