using System.Text.Json.Serialization;
using LiteDB;

namespace Pulse.Shared.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormStatus
{
    Draft,
    Open,
    Closed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormFieldType
{
    Text,
    TextArea,
    Email,
    Phone,
    Number,
    Date,
    Dropdown,
    YesNo,
    Checkbox
}

public sealed class FormField
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = string.Empty;
    public FormFieldType FieldType { get; set; } = FormFieldType.Text;
    public bool Required { get; set; }
    public string? Placeholder { get; set; }
    public List<string> Options { get; set; } = [];
    public int Order { get; set; }
}

public sealed class Form
{
    [BsonField("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public string InstructorCode { get; set; } = string.Empty;
    public FormStatus Status { get; set; } = FormStatus.Draft;
    public List<FormField> Fields { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class FormSubmission
{
    [BsonField("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FormId { get; set; }

    /// <summary>Submitted values keyed by the string form of <see cref="FormField.Id"/>.</summary>
    public Dictionary<string, string> Values { get; set; } = [];
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
