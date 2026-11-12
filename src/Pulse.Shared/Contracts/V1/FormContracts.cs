using Pulse.Shared.Models;

namespace Pulse.Shared.Contracts.V1;

public sealed record FormFieldRequest(
    Guid? Id,
    string Label,
    FormFieldType FieldType,
    bool Required = false,
    string? Placeholder = null,
    IReadOnlyList<string>? Options = null);

public sealed record SaveFormRequest(
    string Title,
    string? Description = null,
    IReadOnlyList<FormFieldRequest>? Fields = null);

public sealed record SubmitFormRequest(Dictionary<string, string>? Values);

public sealed record SubmitFormResponse(Guid SubmissionId);

public sealed record FormSummary(
    Guid Id,
    string Title,
    string JoinCode,
    FormStatus Status,
    int FieldCount,
    int SubmissionCount,
    DateTime CreatedAt);

public sealed record PublicFormField(
    Guid Id,
    string Label,
    FormFieldType FieldType,
    bool Required,
    string? Placeholder,
    IReadOnlyList<string> Options);

public sealed record PublicFormResponse(
    Guid Id,
    string Title,
    string? Description,
    IReadOnlyList<PublicFormField> Fields);

public sealed record FormValidationErrorResponse(string Error, Dictionary<string, string> FieldErrors);
