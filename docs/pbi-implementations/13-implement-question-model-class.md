## PBI: Implement Question model class

### Summary
Create a new `Question` domain entity for Pulse 2.0 to support classroom pulse questions tied to class sessions. This model will represent the core structure for questions sent by faculty during live sessions and reused later.

### Work to be completed
- Add a new `Question` entity in `src/Pulse.Domain/Entities/Question.cs` (or the equivalent domain/entities folder).
- Include the following fields with appropriate types based on existing project conventions:
  - `Id`
  - `SessionId`
  - `Text`
  - `Type`
  - `Options`
  - `SortOrder`
  - `CreatedAt`
- Ensure `Text` is required.
- Ensure `Type` supports the expected question formats such as multiple choice, Likert-scale, and open-ended.
- Ensure `Options` supports storing multiple answer choices when applicable.
- Set `CreatedAt` with a sensible default value of `DateTime.UtcNow`.
- Update the EF Core `DbContext` to include a `DbSet<Question>` if the project uses Entity Framework.
- Apply any needed entity configuration, validation, annotations, or fluent API mappings so the entity aligns with database and domain rules.
- Verify the solution builds successfully.
- Add unit tests to confirm:
  - the model can be instantiated,
  - all properties can be assigned correctly,
  - default values behave as expected,
  - and no regressions are introduced by the new entity.

### Expected outcome
A fully implemented `Question` model exists in the domain layer, compiles successfully, is wired into persistence if applicable, and is covered by unit tests to validate structure and behavior.


## Definition of Done

### Functional
- A `Question` entity class exists in the correct domain location.
- The entity includes all required fields:
  - `Id`
  - `SessionId`
  - `Text`
  - `Type`
  - `Options`
  - `SortOrder`
  - `CreatedAt`
- All properties use the correct types based on project conventions.
- `Text` is required.
- `CreatedAt` is initialized with an appropriate default value.
- `Options` supports multiple values for question types that require selectable answers.
- `Question` is added to the `DbContext` as a `DbSet<Question>` if EF Core is used.

### Quality
- The implementation follows project naming conventions, folder structure, and coding standards.
- Any validation, annotations, or fluent configuration required by the project are included.
- The solution compiles without warnings or errors related to the new model.
- The implementation does not break existing domain, persistence, or test behavior.

### Verification
- `dotnet build` completes successfully for the solution.
- Unit tests are added and passing.
- Unit Tests must verify the implementation is working successfully without breaking other parts of the software.
- Tests validate:
  - entity instantiation,
  - property assignment,
  - required field expectations,
  - collection handling for `Options`,
  - default value behavior for `CreatedAt`.
- If migrations are part of the workflow, the generated migration correctly adds the `Question` table/columns.

### Verification Commands (API + Tests)
- Start the API:
  - `dotnet run --project src/Pulse.WebApi/Pulse.WebApi.csproj`
- Browser/manual endpoint checks:
  - `http://localhost:5062/` (expected: `Pulse API is running`)
  - `http://localhost:5062/questions` (expected: JSON array, often `[]` initially)
  - `http://localhost:5062/health` (development health endpoint)
  - `http://localhost:5062/alive` (development liveness endpoint)
- CLI endpoint checks:
  - `curl http://localhost:5062/questions`
  - `curl -X POST "http://localhost:5062/questions" -H "Content-Type: application/json" -d '{"sessionId":"11111111-1111-1111-1111-111111111111","text":"Is this live?","type":0,"options":[],"sortOrder":1}'`
- Unit tests:
  - `dotnet test src/Pulse.Tests/Pulse.Tests.csproj`
- Optional: run all tests in the repo:
  - `dotnet test`

### Documentation / PR Evidence
- PR includes the new `Question` entity.
- PR includes any `DbContext` or EF configuration updates.
- PR includes unit tests for the new entity.
- PR description or checklist includes evidence of:
  - successful build,
  - passing tests,
  - and migration output if applicable.
