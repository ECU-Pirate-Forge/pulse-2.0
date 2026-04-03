## PBI: Instructor session list API - GET /api/sessions?instructorCode=... #28

### Description
Endpoint that returns all sessions belonging to the instructor identified by their `InstructorCode`.

### Acceptance Criteria
- [ ] Returns 200 and an array of sessions when the `InstructorCode` is valid.
- [ ] Returns 401/403 when the `InstructorCode` is missing or invalid.

### Implementation checklist
- [ ] Add/confirm endpoint handler for `GET /api/sessions` (or equivalent) in Web API.
- [ ] Extract and validate the `InstructorCode` from the request (e.g. header or query parameter).
- [ ] Return 401 when `InstructorCode` is missing.
- [ ] Return 403 when `InstructorCode` is present but invalid/unrecognised.
- [ ] Lookup all sessions associated with the valid `InstructorCode` from the repository.
- [ ] Return 200 with the array of session models on success.
- [ ] Return an empty array (not 404) if the instructor has no sessions.
- [ ] Add/confirm unit tests for valid code, missing code, and invalid code scenarios.

## Definition of Done

### PBI Checklist
- [ ] Endpoint implemented and reachable.
- [ ] Returns 200 + array of sessions for a valid `InstructorCode`.
- [ ] Returns 401 for a missing `InstructorCode`.
- [ ] Returns 403 for an invalid/unrecognised `InstructorCode`.
- [ ] Automated tests added/updated and passing.
- [ ] `dotnet build` succeeds.
- [ ] Linting passes (`dotnet format --verify-no-changes`).

### Manual Verification (API)
- Configure instructor code before starting the API (required for valid-code success path):
	- Temporary (current shell): `export Security__InstructorCode=INST001`
	- or set `"Security": { "InstructorCode": "INST001" }` in `src/Pulse.WebApi/appsettings.Development.json`
- Start the API:
	- `dotnet run --project src/Pulse.WebApi/Pulse.WebApi.csproj`
- Verify service health in browser:
	- `http://localhost:5062/` (expected: `Pulse API is running`)

### Manual Verification (cURL)
1. Valid `InstructorCode` should return HTTP 200 with an array (possibly empty).

```bash
curl -i -sS "http://localhost:5062/sessions" -H "InstructorCode: INST001"
```

2. Missing `InstructorCode` should return HTTP 401 with JSON error.

```bash
curl -i -sS "http://localhost:5062/sessions"
```

3. Invalid `InstructorCode` should return HTTP 403 with JSON error.

```bash
curl -i -sS "http://localhost:5062/sessions" -H "InstructorCode: WRONG"
```

### Automated Verification
- Run targeted unit tests for this endpoint behavior:
	- `dotnet test src/Pulse.Tests/Pulse.Tests.csproj --filter GetSessionsByInstructorCodeEndpointTests`
- Run all tests:
	- `dotnet test src/Pulse.Tests/Pulse.Tests.csproj`
- Build solution:
	- `dotnet build Pulse.slnx`
- Verify formatting:
	- `dotnet format Pulse.slnx --verify-no-changes`

### Unit Test Location
- `src/Pulse.Tests/Sessions/CreateSessionTests.cs`
  - `GetSessionsByInstructorCodeEndpointTests.GetSessionsValidInstructorCodeReturns200AndArray`
  - `GetSessionsByInstructorCodeEndpointTests.GetSessionsMissingInstructorCodeReturns401`
  - `GetSessionsByInstructorCodeEndpointTests.GetSessionsInvalidInstructorCodeReturns403`
