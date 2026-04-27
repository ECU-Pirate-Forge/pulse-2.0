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

### GET /api/sessions/join/{joinCode}

Public student join lookup by join code.

Protected: No

Response 200:

```json
{
	"title": "Biology Quiz - Chapter 5"
}
```

Response errors:

- 400 when join code is missing/empty
- 404 when no session exists for the code

### GET /api/sessions/qr/{joinCode}

Generate a PNG QR code for a join code.

Protected: No

Response 200:

- Content-Type: image/png

### GET /sessions/{id}/qr

Generate a PNG QR code for a session id.

Protected: Yes (InstructorCode required)

Response 200:

- Content-Type: image/png

### PUT /api/sessions/{id}/unblind

Set session IsUnblinded to true.

Protected: Yes (InstructorCode required)

Response codes:

- 200 OK (updated session)
- 401 Unauthorized
- 403 Forbidden
- 404 Not Found

### GET /api/sessions/{id}/results

Get aggregated results for all questions in a session.

Protected: Yes (InstructorCode required)

Response 200 shape:

```json
{
	"sessionId": "11111111-1111-1111-1111-111111111111",
	"title": "Week 5 Quick Check",
	"questions": [
		{
			"questionId": "22222222-2222-2222-2222-222222222222",
			"text": "How confident are you with this topic?",
			"type": "Likert",
			"tallies": [
				{ "option": "Low", "count": 2 },
				{ "option": "High", "count": 8 }
			],
			"totalResponses": 10
		}
	],
	"totalResponses": 10
}
```

## Questions

### GET /api/sessions/{sessionId}/questions

Get ordered questions for a specific session.

Protected: No (current implementation)

Response codes:

- 200 OK (array of question objects ordered by sortOrder)

### POST /api/sessions/{sessionId}/questions

Create a question scoped to a specific session.

Protected: No (current implementation)

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

### GET /questions

Get all questions.

Protected: No

### POST /questions

Create a question.

Protected: Yes (InstructorCode required)

Request body (example):

```json
{
	"sessionId": "11111111-1111-1111-1111-111111111111",
	"text": "What is 2 + 2?",
	"type": 0,
	"options": ["3", "4", "5"],
	"sortOrder": 0
}
```

Response codes:

- 200 OK (created question)
- 400 Bad Request (validation errors)

### PUT /questions/{id}

Update question text/type/options.

Protected: Yes (InstructorCode required)

Request body:

```json
{
	"text": "Updated prompt",
	"type": 0,
	"options": ["Option A", "Option B"]
}
```

Response codes:

- 200 OK
- 400 Bad Request
- 404 Not Found

### DELETE /questions/{id}

Delete a question.

Protected: Yes (InstructorCode required)

Response codes:

- 204 No Content
- 400 Bad Request
- 404 Not Found

### PUT /api/questions/reorder

Reorder questions by providing ordered question ids.

Protected: No (current implementation)

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

## Question Bank

### POST /api/questionbank

Create a reusable question bank item.

Protected: No (current implementation)

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

Protected: No (current implementation)

Expected request: multipart/form-data with a CSV file.

Response codes:

- 200 OK (rows + row-level validation issues)
- 400 Bad Request

### POST /api/sessions/{sessionId}/questions/import

Import validated question bank content into a session.

Protected: No (current implementation)

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