using Pulse.Common.Services;
using Pulse.Application.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;

namespace Pulse.WebApi;

public static class CsvPreviewEndpointHandlers
{
    public static async Task<IResult> PreviewImport(HttpRequest request, QuestionService questionService)
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
            var matchingPairsRaw = values.Count > 3 ? values[3].Trim() : string.Empty;
            var course = values.Count > 4 ? values[4].Trim() : string.Empty;
            var topic = values.Count > 5 ? values[5].Trim() : string.Empty;
            var tagsRaw = values.Count > 6 ? values[6].Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(text))
                errors.Add("Text is required.");

            var typeIsValid = TryParseSupportedType(typeRaw, out var questionType);
            if (!typeIsValid)
                errors.Add($"Type '{typeRaw}' is not a supported question type (MultipleChoice, MultipleAnswer, TrueFalse, or Matching).");

            var options = string.IsNullOrWhiteSpace(optionsRaw)
                ? new List<string>()
                : optionsRaw.Split('|').Select(o => o.Trim()).Where(o => !string.IsNullOrWhiteSpace(o)).ToList();

            var matchingPairs = ParseMatchingPairs(matchingPairsRaw);

            if (typeIsValid)
            {
                var validation = questionService.ValidateQuestion(new QuestionDTO
                {
                    Type = questionType,
                    Options = options,
                    MatchingPairs = matchingPairs
                });
                if (!validation.IsValid)
                    errors.Add(validation.ErrorMessage!);
            }

            if (errors.Count > 0)
                return (false, null, string.Join(" ", errors));

            return (true, new CsvQuestionRow
            {
                RowNumber = rowNumber,
                Text = text,
                Type = questionType,
                Options = options,
                MatchingPairs = matchingPairs,
                Course = course,
                Topic = topic,
                Tags = tagsRaw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                Source = QuestionSource.CsvImport,
                ModerationStatus = QuestionModerationStatus.Approved,
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
            MatchingPairs = [],
            ValidationErrors = [e.Message]
        }).ToList();

        var allRows = previewRows.Concat(errorRows).OrderBy(r => r.RowNumber).ToList();

        return Results.Ok(new CsvPreviewResponse(allRows, result.Errors.Count == 0));
    }

    private static bool TryParseSupportedType(string value, out QuestionType type)
    {
        if (Enum.TryParse(value, ignoreCase: true, out type) &&
            type is QuestionType.MultipleChoice or
                QuestionType.MultipleAnswer or
                QuestionType.TrueFalse or
                QuestionType.Matching)
        {
            return true;
        }

        type = default;
        return false;
    }

    private static List<MatchingPair> ParseMatchingPairs(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(encodedPair => encodedPair.Split("=>", 2, StringSplitOptions.TrimEntries))
            .Select(parts => new MatchingPair
            {
                Prompt = parts.ElementAtOrDefault(0) ?? string.Empty,
                Match = parts.ElementAtOrDefault(1) ?? string.Empty
            })
            .ToList();
    }
}

public sealed class CsvQuestionRow
{
    public int RowNumber { get; init; }
    public string Text { get; init; } = string.Empty;
    public QuestionType Type { get; init; }
    public List<string> Options { get; init; } = [];
    public List<MatchingPair> MatchingPairs { get; init; } = [];
    public string Course { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = [];
    public QuestionSource Source { get; init; } = QuestionSource.CsvImport;
    public QuestionModerationStatus ModerationStatus { get; init; } = QuestionModerationStatus.Approved;
    public List<string> ValidationErrors { get; init; } = [];
    public bool IsValid => ValidationErrors.Count == 0;
}

public sealed record CsvPreviewResponse(List<CsvQuestionRow> Rows, bool AllRowsValid);
