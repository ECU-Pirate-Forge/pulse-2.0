using Pulse.Common.Services;

namespace Pulse.Tests;

public class CsvParserTests
{
    [Fact]
    public void Parse_ReturnsParsedRowsAndValidationErrorsWithRowNumbers()
    {
        var csv = string.Join('\n',
            "Name,Age",
            "Alice,30",
            "Bob,",
            "Charlie,22",
            "Dana,abc");

        var result = CsvParser.Parse<PersonRow>(csv, (values, _) =>
        {
            if (values.Count != 2)
            {
                return (false, null, "Expected Name and Age columns.");
            }

            var name = values[0].Trim();
            var ageText = values[1].Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                return (false, null, "Name is required.");
            }

            if (!int.TryParse(ageText, out var age))
            {
                return (false, null, "Age must be a valid integer.");
            }

            return (true, new PersonRow(name, age), null);
        });

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("Alice", result.Rows[0].Name);
        Assert.Equal(30, result.Rows[0].Age);
        Assert.Equal("Charlie", result.Rows[1].Name);
        Assert.Equal(22, result.Rows[1].Age);

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(3, result.Errors[0].RowNumber);
        Assert.Equal("Age must be a valid integer.", result.Errors[0].Message);
        Assert.Equal(5, result.Errors[1].RowNumber);
        Assert.Equal("Age must be a valid integer.", result.Errors[1].Message);
    }

    [Fact]
    public void Parse_HandlesQuotedCommasInFieldValues()
    {
        var csv = string.Join('\n',
            "Name,Age",
            "\"Smith, John\",40");

        var result = CsvParser.Parse<PersonRow>(csv, (values, _) =>
        {
            if (values.Count != 2)
            {
                return (false, null, "Expected Name and Age columns.");
            }

            if (!int.TryParse(values[1], out var age))
            {
                return (false, null, "Age must be a valid integer.");
            }

            return (true, new PersonRow(values[0], age), null);
        });

        Assert.Single(result.Rows);
        Assert.Equal("Smith, John", result.Rows[0].Name);
        Assert.Equal(40, result.Rows[0].Age);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_ConvertsRowParserExceptionsToRowErrors()
    {
        var csv = string.Join('\n',
            "Name,Age",
            "Alice,30",
            "Bob,20");

        var result = CsvParser.Parse<PersonRow>(csv, (values, _) =>
        {
            if (values[0] == "Bob")
            {
                throw new InvalidOperationException("Simulated parse failure.");
            }

            return (true, new PersonRow(values[0], int.Parse(values[1])), null);
        });

        Assert.Single(result.Rows);
        Assert.Equal("Alice", result.Rows[0].Name);

        Assert.Single(result.Errors);
        Assert.Equal(3, result.Errors[0].RowNumber);
        Assert.Equal("Simulated parse failure.", result.Errors[0].Message);
    }

    private sealed record PersonRow(string Name, int Age);
}