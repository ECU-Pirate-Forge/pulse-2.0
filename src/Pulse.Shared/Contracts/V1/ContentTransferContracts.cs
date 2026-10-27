using System.Text.Json;
using System.Text.Json.Serialization;
using Pulse.Domain.Entities;

namespace Pulse.Shared.Contracts.V1;

public sealed class ContentTransferDocument
{
    public required string Format { get; init; }
    public required int Version { get; init; }
    public required string Kind { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public List<PortableQuestion>? Questions { get; init; }
    public List<PortableFormField>? Fields { get; init; }
}

public sealed class PortableQuestion
{
    public required string Text { get; init; }
    public required string Type { get; init; }
    public List<string> Options { get; init; } = [];
    public List<MatchingPair> MatchingPairs { get; init; } = [];
    public List<string> CorrectAnswers { get; init; } = [];
    public string Explanation { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string SourceQuote { get; init; } = string.Empty;
    public string Course { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = [];
    public bool TriviaEligible { get; init; }
    public bool IsDraft { get; init; }
    public string ModerationStatus { get; init; } = "Approved";
}

public sealed class PortableFormField
{
    public required string Label { get; init; }
    public required string FieldType { get; init; }
    public bool Required { get; init; }
    public string? Placeholder { get; init; }
    public List<string> Options { get; init; } = [];
}

public sealed record ContentImportResponse(string Kind, Guid? Id, int ImportedCount, Guid? ImportBatchId = null);

public static class ContentTransferJson
{
    public const int MaxFileBytes = 2 * 1024 * 1024;
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
