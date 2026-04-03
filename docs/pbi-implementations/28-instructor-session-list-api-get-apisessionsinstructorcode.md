## PBI: Instructor session list API - GET /sessions #28

### Description
Endpoint that returns all sessions belonging to the instructor identified by their `InstructorCode`.

The `InstructorCode` is passed as an HTTP request header (not a query parameter). Access control is enforced by `InstructorCodeMiddleware` before the endpoint handler is reached.

### Acceptance Criteria
- [x] Returns 200 and an array of sessions when the `InstructorCode` is valid.
- [x] Returns 401/403 when the `InstructorCode` is missing or invalid.

### Implementation checklist
- [x] Add/confirm endpoint handler for `GET /sessions` in Web API.
- [x] Extract and validate the `InstructorCode` from the `InstructorCode` request header via `InstructorCodeMiddleware`.
- [x] Return 401 when `InstructorCode` is missing.
- [x] Return 403 when `InstructorCode` is present but invalid/unrecognised.
- [x] Lookup all sessions associated with the valid `InstructorCode` from the repository.
- [x] Return 200 with the array of session models on success.
- [x] Return an empty array (not 404) if the instructor has no sessions.
- [x] Add/confirm unit tests for valid code and no-match scenarios.

## Definition of Done

### PBI Checklist
- [x] Endpoint implemented and reachable.
- [x] Returns 200 + array of sessions for a valid `InstructorCode`.
- [x] Returns 401 for a missing `InstructorCode`.
- [x] Returns 403 for an invalid/unrecognised `InstructorCode`.
- [x] Automated tests added/updated and passing.
- [x] `dotnet build` succeeds.
- [x] Linting passes (`dotnet format --verify-no-changes`).

### Manual Verification (API)
- Configure instructor code before starting the API (required for valid-code success path):
	- Recommended: `export Security__InstructorCode=INST001` (environment variable, not committed to source)
	- Do **not** commit `InstructorCode` values to `appsettings.Development.json`; use environment variables or user-secrets instead.
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
  - `GetSessionsByInstructorCodeEndpointTests.GetSessionsNoMatchingCodeReturnsEmptyArray`
- `src/Pulse.Tests/InstructorCodeMiddlewareTests.cs` — covers 401/403 enforcement
