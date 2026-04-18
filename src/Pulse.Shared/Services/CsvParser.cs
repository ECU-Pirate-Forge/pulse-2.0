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

        var lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            var rowNumber = i + 1;

            if (hasHeader && rowNumber == 1)
            {
                continue;
            }

            var values = SplitCsvLine(lines[i]);

            try
            {
                var result = rowParser(values, rowNumber);
                if (result.IsValid && result.ParsedRow is not null)
                {
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
            catch (Exception ex)
            {
                errors.Add(new CsvRowValidationError
                {
                    RowNumber = rowNumber,
                    Message = ex.Message
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

        values.Add(current.ToString());
        return values;
    }
}