namespace Pulse.Shared.Models;

public sealed class QuestionImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public int ImportedCount { get; set; }
}

public sealed record QuestionImportSummary(
    Guid Id, string FileName, DateTime ImportedAt, int ImportedCount, int RemainingCount);

public sealed record QuestionBankDeletePreview(List<Guid> QuestionIds);

public sealed record DeleteQuestionBankRequest(QuestionBankQuery? Filter, List<Guid>? ExpectedIds);

public sealed record QuestionBankDeleteResponse(int DeletedCount);
