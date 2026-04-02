## PBI: 22-put-apiquestionsid-update-question

### Description
Endpoint to update a question's text, type, and options; validates input.

### Acceptance Criteria
- Returns 200 and updated question on valid update.
- Returns 400 on invalid payload (e.g., MC with <2 options).

### Implementation checklist
- Add/confirm request DTO for update payload (text, type, options).
- Add endpoint handler for `PUT /api/questions/{id}` in Web API.
- Validate input:
	- id is valid.
	- Required fields are present.
	- MC questions have at least 2 options.
- Lookup existing question by id; return 404 when not found.
- Apply updates and persist changes.
- Return 200 with updated question model.
- Add/confirm unit tests for valid update and invalid payload.

## Definition of Done

### Checklist
- Endpoint implemented and reachable.
- Returns 200 + updated question for valid payload.
- Returns 400 for invalid payload (including MC < 2 options).
- Returns 404 for unknown question id.
- Automated tests added/updated and passing.
- `dotnet build` succeeds.

### Manual Verification (API)
- Start the API:
	- `dotnet run --project src/Pulse.WebApi/Pulse.WebApi.csproj`
- Verify service health in browser:
	- `http://localhost:5062/` (expected: `Pulse API is running`)
	- `http://localhost:5062/questions` (expected: JSON array)

### Manual Verification (cURL)
Use these commands exactly as written. Do not escape option flags like `-H` or `-d`.

1. Create a seed question and capture its `id`.

```bash
curl -i -sS -X POST "http://localhost:5062/questions" -H "Content-Type: application/json" -d '{"sessionId":"11111111-1111-1111-1111-111111111111","text":"Original question?","type":2,"options":[],"sortOrder":1}'
```

Optional: save the returned `id` for reuse.

```bash
QUESTION_ID=$(curl -sS -X POST "http://localhost:5062/questions" -H "Content-Type: application/json" -d '{"sessionId":"11111111-1111-1111-1111-111111111112","text":"Original question?","type":2,"options":[],"sortOrder":1}' | jq -r '.id')
echo "$QUESTION_ID"
```

2. Update the question with a valid payload (expect HTTP 200 + updated model).

```bash
curl -i -sS -X PUT "http://localhost:5062/api/questions/<QUESTION_ID>" -H "Content-Type: application/json" -d '{"text":"Updated question text","type":0,"options":["Option A","Option B"]}'
```

3. Validate invalid MC payload (expect HTTP 400).

```bash
curl -i -sS -X PUT "http://localhost:5062/api/questions/<QUESTION_ID>" -H "Content-Type: application/json" -d '{"text":"Invalid MC payload","type":0,"options":["Only one option"]}'
```

4. Validate unknown id behavior (expect HTTP 404).

```bash
curl -i -sS -X PUT "http://localhost:5062/api/questions/00000000-0000-0000-0000-000000000123" -H "Content-Type: application/json" -d '{"text":"Question not found","type":2,"options":[]}'
```

### Automated Verification
- Run targeted tests for this PBI:
	- `dotnet test src/Pulse.Tests/Pulse.Tests.csproj --filter QuestionUpdateEndpointTests`
- Run all tests:
	- `dotnet test`
- Build impacted projects:
	- `dotnet build src/Pulse.WebApi/Pulse.WebApi.csproj`
	- `dotnet build src/Pulse.Tests/Pulse.Tests.csproj`