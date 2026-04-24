using Pulse.Common.Services;
using Pulse.Domain.Entities;

namespace Pulse.WebApi;

public static class CsvPreviewEndpointHandlers
{
    public static async Task<IResult> PreviewImport(HttpRequest request)
    {
        IFormFile? file;
        try
        {
            file = request.Form.Files.GetFile("file");
        }
        catch
        {
            file = null;
        }
        if (file is null)
            return Results.BadRequest("A CSV file is required.");

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest("File must be a .csv file.");

        if (file.Length == 0)
            return Results.BadRequest("File is empty.");

        string csvContent;
        using (var reader = new StreamReader(file.OpenReadStream()))
        {
            csvContent = await reader.ReadToEndAsync();
        }

        var result = CsvParser.Parse<CsvQuestionRow>(csvContent, (values, rowNumber) =>
        {
            var errors = new List<string>();

            var text = values.Count > 0 ? values[0].Trim() : string.Empty;
            var typeRaw = values.Count > 1 ? values[1].Trim() : string.Empty;
            var optionsRaw = values.Count > 2 ? values[2].Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(text))
                errors.Add("Text is required.");

            var typeIsValid = int.TryParse(typeRaw, out var typeInt) && Enum.IsDefined(typeof(QuestionType), typeInt);
            if (!typeIsValid)
                errors.Add($"Type '{typeRaw}' is not a valid question type (0=MultipleChoice, 1=LikertScale, 2=OpenEnded).");

            var options = string.IsNullOrWhiteSpace(optionsRaw)
                ? new List<string>()
                : optionsRaw.Split('|').Select(o => o.Trim()).Where(o => !string.IsNullOrWhiteSpace(o)).ToList();

            if (typeIsValid && (QuestionType)typeInt == QuestionType.MultipleChoice && options.Count < 2)
                errors.Add("Multiple choice questions require at least 2 options.");

            if (errors.Count > 0)
                return (false, null, string.Join(" ", errors));

            return (true, new CsvQuestionRow
            {
                RowNumber = rowNumber,
                Text = text,
                Type = (QuestionType)typeInt,
                Options = options,
                ValidationErrors = []
            }, null);
        });

        var previewRows = result.Rows.ToList();
        var errorRows = result.Errors.Select(e => new CsvQuestionRow
        {
            RowNumber = e.RowNumber,
            Text = string.Empty,
            Type = QuestionType.MultipleChoice,
            Options = [],
            ValidationErrors = [e.Message]
        }).ToList();

        var allRows = previewRows.Concat(errorRows).OrderBy(r => r.RowNumber).ToList();

        return Results.Ok(new CsvPreviewResponse(allRows, result.Errors.Count == 0));
    }
}

public sealed class CsvQuestionRow
{
    public int RowNumber { get; init; }
    public string Text { get; init; } = string.Empty;
    public QuestionType Type { get; init; }
    public List<string> Options { get; init; } = [];
    public List<string> ValidationErrors { get; init; } = [];
    public bool IsValid => ValidationErrors.Count == 0;
}

public sealed record CsvPreviewResponse(List<CsvQuestionRow> Rows, bool AllRowsValid);
