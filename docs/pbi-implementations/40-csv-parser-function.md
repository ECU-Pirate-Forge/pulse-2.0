## PBI: 40-csv-parser-function-(per-row validation)

## Description

Library function that parses CSV and returns DTOs plus row-level errors.

## Acceptance Criteria

- [x] Returns array of parsed rows and validation errors with row numbers

## Usage Example

```csharp
using Pulse.Common.Services;

var csv = string.Join('\n',
	"Name,Age",
	"Alice,30",
	"Bob,abc");

var result = CsvParser.Parse<PersonDto>(csv, (values, rowNumber) =>
{
	if (values.Count != 2)
		return (false, null, "Expected Name and Age columns.");

	var name = values[0].Trim();
	if (!int.TryParse(values[1], out var age))
		return (false, null, "Age must be a valid integer.");

	return (true, new PersonDto(name, age), null);
});

// result.Rows: valid parsed DTOs
// result.Errors: row-level errors that include RowNumber and Message

public sealed record PersonDto(string Name, int Age);
```

## Run Tests

Run only the CSV parser unit tests:

```bash
dotnet test src/Pulse.Tests/Pulse.Tests.csproj --filter "FullyQualifiedName~CsvParserTests"
```

Run the full test project:

```bash
dotnet test src/Pulse.Tests/Pulse.Tests.csproj
```