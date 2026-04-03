## PBI: GET /sessions/{sessionId}/qr - QR image endpoint #37

### Description
Endpoint that generates and returns a PNG-encoded QR code containing the session join URL (`https://host/join/{joinCode}`).

### Acceptance Criteria
- [ ] Returns a valid `image/png` QR code encoding the join URL.
- [ ] Returns 404 when the session is not found.

### Implementation checklist
- [ ] Add/confirm a QR code generation library to the project (e.g. QRCoder).
- [ ] Add/confirm endpoint handler for `GET /sessions/{id}/qr` in Web API.
- [ ] Lookup the session by id; return 404 when not found.
- [ ] Construct the join URL string (e.g. `https://host/join/{joinCode}`) using the session's `JoinCode` and the configured host.
- [ ] Pass the join URL to the QR code library to generate a PNG-encoded QR code.
- [ ] Return the PNG as a File result with content type `image/png`.
- [ ] Add/confirm unit tests for valid QR generation and unknown session id.

## Definition of Done

### PBI Checklist
- [ ] Endpoint implemented and reachable.
- [ ] Returns a valid `image/png` QR code encoding the correct join URL on success.
- [ ] Returns 404 for an unknown session id.
- [ ] Returned image content type is correctly set to `image/png`.
- [ ] Automated unit tests added/updated and passing.
- [ ] `dotnet build` succeeds.
- [ ] Linting passes (`dotnet format --verify-no-changes`).

### Automated Verification
- Run targeted QR endpoint tests:
	- `dotnet test src/Pulse.Tests/Pulse.Tests.csproj --filter SessionQrEndpointTests`
- Run all tests:
	- `dotnet test src/Pulse.Tests/Pulse.Tests.csproj`
- Build impacted projects:
	- `dotnet build src/Pulse.WebApi/Pulse.WebApi.csproj`
	- `dotnet build src/Pulse.Tests/Pulse.Tests.csproj`
- Verify formatting:
	- `dotnet format Pulse.slnx --verify-no-changes`
