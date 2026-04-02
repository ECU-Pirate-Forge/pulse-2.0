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
