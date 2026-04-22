namespace Pulse.Common.Services;

public sealed class CsvRowValidationError
{
    public int RowNumber { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class CsvParseResult<T>
{
    public CsvParseResult(IReadOnlyList<T> rows, IReadOnlyList<CsvRowValidationError> errors)
    {
        Rows = rows;
        Errors = errors;
    }

    public IReadOnlyList<T> Rows { get; }
    public IReadOnlyList<CsvRowValidationError> Errors { get; }
}

public static class CsvParser
{
    public static CsvParseResult<T> Parse<T>(
        string csv,
        Func<IReadOnlyList<string>, int, (bool IsValid, T? ParsedRow, string? ErrorMessage)> rowParser,
        bool hasHeader = true)
    {
        ArgumentNullException.ThrowIfNull(csv);
        ArgumentNullException.ThrowIfNull(rowParser);

        var rows = new List<T>();
        var errors = new List<CsvRowValidationError>();

        using var reader = new System.IO.StringReader(csv);
        string? line;
        var rowNumber = 0;

        while ((line = reader.ReadLine()) is not null)
        {
            rowNumber++;

            if (hasHeader && rowNumber == 1)
            {
                continue;
            }

            if (line.Length == 0)
            {
                continue;
            }

            List<string> values;
            try
            {
                values = SplitCsvLine(line);
            }
            catch (FormatException ex)
            {
                errors.Add(new CsvRowValidationError { RowNumber = rowNumber, Message = ex.Message });
                continue;
            }

            (bool IsValid, T? ParsedRow, string? ErrorMessage) result;
            try
            {
                result = rowParser(values, rowNumber);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(new CsvRowValidationError { RowNumber = rowNumber, Message = ex.Message });
                continue;
            }

            if (result.IsValid)
            {
                if (result.ParsedRow is null)
                {
                    throw new InvalidOperationException(
                        $"Row parser returned a valid result without providing a row object at row {rowNumber}. This indicates a bug in the row parser implementation.");
                }

                rows.Add(result.ParsedRow);
            }
            else
            {
                errors.Add(new CsvRowValidationError
                {
                    RowNumber = rowNumber,
                    Message = string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? "Row validation failed."
                        : result.ErrorMessage
                });
            }
        }

        return new CsvParseResult<T>(rows, errors);
    }

    private static List<string> SplitCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                // Escaped quote inside quoted field.
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        if (inQuotes)
        {
            throw new FormatException("Unmatched quote in CSV field.");
        }

        values.Add(current.ToString());
        return values;
    }
}