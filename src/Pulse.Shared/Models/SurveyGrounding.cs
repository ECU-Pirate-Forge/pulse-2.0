namespace Pulse.Shared.Models;

public sealed class SurveyGrounding
{
    public Guid Id { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public string AdditionalContext { get; set; } = string.Empty;
    public List<SurveyGroundingDocument> Documents { get; set; } = [];
}

public sealed class SurveyGroundingDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public string Text { get; set; } = string.Empty;
}
