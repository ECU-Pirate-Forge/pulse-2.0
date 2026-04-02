## PBI: 39-apply-instructorcode-middleware-to-questionsession-management-endpoints

### Description
Ensure middleware is applied to all instructor-only APIs.

### Acceptance Criteria
- [ ] Missing InstructorCode returns 401; invalid returns 403.

### Implementation checklist
- [ ] Identify instructor-only routes (question/session management + results + QR as applicable).
- [ ] Apply middleware to all identified routes.
- [ ] Ensure middleware reads `InstructorCode` consistently from expected header/source.
- [ ] Verify behavior:
	- [ ] Missing code -> 401.
	- [ ] Invalid code -> 403.
	- [ ] Valid code -> request continues.
- [ ] Add/confirm tests for 401, 403, and success path.
- [ ] Ensure no student/public endpoints are accidentally protected.

## Definition of Done

### Checklist
- [ ] Middleware is wired to all intended instructor-only endpoints.
- [ ] Missing `InstructorCode` returns 401.
- [ ] Invalid `InstructorCode` returns 403.
- [ ] Valid `InstructorCode` allows endpoint execution.
- [ ] Public/student endpoints remain accessible as designed.
- [ ] Automated tests added/updated and passing.
- [ ] `dotnet build` and `dotnet test` succeed.

### Automated Verification
- Run targeted middleware tests (or equivalent endpoint-level tests):
	- `dotnet test src/Pulse.Tests/Pulse.Tests.csproj --filter InstructorCode`
- Run all tests:
	- `dotnet test`
- Build impacted projects:
	- `dotnet build src/Pulse.WebApi/Pulse.WebApi.csproj`
	- `dotnet build src/Pulse.Tests/Pulse.Tests.csproj`
