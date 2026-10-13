using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;

namespace Pulse.Common.Services;

public static partial class FormService
{
    public const int MaxFields = 50;
    public const int MaxValueLength = 4000;

    public static readonly IReadOnlyList<string> YesNoOptions = ["Yes", "No"];

    /// <summary>
    /// Validates a create/update request and returns an error message, or null when valid.
    /// </summary>
    public static string? ValidateDefinition(SaveFormRequest? request)
    {
        if (request is null)
            return "Request body is required.";
        if (string.IsNullOrWhiteSpace(request.Title))
            return "Form title is required.";

        var fields = request.Fields ?? [];
        if (fields.Count > MaxFields)
            return $"A form can have at most {MaxFields} fields.";

        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (field is null || string.IsNullOrWhiteSpace(field.Label))
                return $"Field {i + 1} needs a label.";
            if (!Enum.IsDefined(field.FieldType))
                return $"Field '{field.Label}' has an unknown type.";
            if (field.FieldType == FormFieldType.Dropdown &&
                !(field.Options ?? []).Any(option => !string.IsNullOrWhiteSpace(option)))
                return $"Dropdown field '{field.Label}' needs at least one option.";
        }

        var duplicateIds = fields
            .Where(field => field?.Id is not null)
            .GroupBy(field => field!.Id)
            .Any(group => group.Count() > 1);
        if (duplicateIds)
            return "Field ids must be unique.";

        return null;
    }

    /// <summary>
    /// Applies a validated request to a form, preserving existing field ids so prior submissions stay linked.
    /// </summary>
    public static void ApplyDefinition(Form form, SaveFormRequest request)
    {
        form.Title = request.Title.Trim();
        form.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        form.Fields = (request.Fields ?? [])
            .Select((field, index) => new FormField
            {
                Id = field.Id ?? Guid.NewGuid(),
                Label = field.Label.Trim(),
                FieldType = field.FieldType,
                Required = field.Required,
                Placeholder = string.IsNullOrWhiteSpace(field.Placeholder) ? null : field.Placeholder.Trim(),
                Options = field.FieldType == FormFieldType.Dropdown
                    ? (field.Options ?? [])
                        .Where(option => !string.IsNullOrWhiteSpace(option))
                        .Select(option => option.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                    : [],
                Order = index
            })
            .ToList();
    }

    /// <summary>
    /// Validates submitted values against the form definition.
    /// Returns per-field errors keyed by field id; on success <paramref name="normalized"/> holds the cleaned values.
    /// </summary>
    public static Dictionary<string, string> ValidateSubmission(
        Form form,
        IReadOnlyDictionary<string, string>? values,
        out Dictionary<string, string> normalized)
    {
        var errors = new Dictionary<string, string>();
        normalized = [];
        values ??= new Dictionary<string, string>();

        foreach (var field in form.Fields.OrderBy(field => field.Order))
        {
            var key = field.Id.ToString();
            values.TryGetValue(key, out var raw);
            var value = raw?.Trim() ?? string.Empty;

            if (field.FieldType == FormFieldType.Checkbox)
            {
                var isChecked = IsTruthy(value);
                if (field.Required && !isChecked)
                {
                    errors[key] = $"{field.Label} must be checked.";
                    continue;
                }
                normalized[key] = isChecked ? "Yes" : "No";
                continue;
            }

            if (value.Length == 0)
            {
                if (field.Required)
                    errors[key] = $"{field.Label} is required.";
                continue;
            }

            if (value.Length > MaxValueLength)
            {
                errors[key] = $"{field.Label} must be {MaxValueLength} characters or fewer.";
                continue;
            }

            var error = ValidateValue(field, ref value);
            if (error is not null)
            {
                errors[key] = error;
                continue;
            }

            normalized[key] = value;
        }

        return errors;
    }

    private static string? ValidateValue(FormField field, ref string value)
    {
        var input = value;
        switch (field.FieldType)
        {
            case FormFieldType.Email:
                if (!IsValidEmail(value))
                    return $"{field.Label} must be a valid email address.";
                break;
            case FormFieldType.Phone:
                if (!PhoneRegex().IsMatch(value) || value.Count(char.IsDigit) < 7)
                    return $"{field.Label} must be a valid phone number.";
                break;
            case FormFieldType.Number:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    return $"{field.Label} must be a number.";
                value = number.ToString(CultureInfo.InvariantCulture);
                break;
            case FormFieldType.Date:
                if (!DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var date))
                    return $"{field.Label} must be a valid date.";
                value = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                break;
            case FormFieldType.Dropdown:
                var match = field.Options.FirstOrDefault(option =>                 string.Equals(option, input, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                    return $"{field.Label} must be one of the listed options.";
                value = match;
                break;
            case FormFieldType.YesNo:
                var yesNo = YesNoOptions.FirstOrDefault(option =>                 string.Equals(option, input, StringComparison.OrdinalIgnoreCase));
                if (yesNo is null)
                    return $"{field.Label} must be Yes or No.";
                value = yesNo;
                break;
        }

        return null;
    }

    private static bool IsTruthy(string value) =>
        value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
        value == "1";

    private static bool IsValidEmail(string value)
    {
        if (value.Contains(' ') || !value.Contains('@'))
            return false;
        try
        {
            var address = new MailAddress(value);
            return address.Address == value && address.Host.Contains('.');
        }
        catch (FormatException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"^\+?[0-9 ()\-.]+$")]
    private static partial Regex PhoneRegex();

    public static PublicFormResponse ToPublic(Form form) =>
        new(
            form.Id,
            form.Title,
            form.Description,
            form.Fields
                .OrderBy(field => field.Order)
                .Select(field => new PublicFormField(
                    field.Id,
                    field.Label,
                    field.FieldType,
                    field.Required,
                    field.Placeholder,
                    field.FieldType == FormFieldType.YesNo ? YesNoOptions : field.Options))
                .ToList());

    /// <summary>
    /// Builds an RFC 4180 CSV export with one column per field (in form order) and one row per submission.
    /// </summary>
    public static string ExportCsv(Form form, IEnumerable<FormSubmission> submissions)
    {
        var fields = form.Fields.OrderBy(field => field.Order).ToList();
        var builder = new StringBuilder();

        AppendRow(builder, ["Submitted At (UTC)", .. fields.Select(field => field.Label)]);
        foreach (var submission in submissions.OrderBy(submission => submission.SubmittedAt))
        {
            var row = new List<string>
            {
                submission.SubmittedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            };
            row.AddRange(fields.Select(field =>
                submission.Values.TryGetValue(field.Id.ToString(), out var value) ? value : string.Empty));
            AppendRow(builder, row);
        }

        return builder.ToString();
    }

    public static string ExportFileName(Form form)
    {
        var safe = new string(form.Title.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        if (string.IsNullOrEmpty(safe))
            safe = "form";
        return $"{safe}-submissions.csv";
    }

    private static void AppendRow(StringBuilder builder, IEnumerable<string> cells)
    {
        builder.Append(string.Join(',', cells.Select(EscapeCsv)));
        builder.Append("\r\n");
    }

    public static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        // Neutralize spreadsheet formula injection.
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]) && !IsPlainNumber(value))
            value = "'" + value;
        if (value.IndexOfAny([',', '"', '\r', '\n']) >= 0)
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static bool IsPlainNumber(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
}
