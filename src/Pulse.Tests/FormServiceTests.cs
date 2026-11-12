using Pulse.Common.Services;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;

namespace Pulse.Tests.Tests;

public class FormServiceTests
{
    [Fact]
    public void ValidateDefinition_RequiresTitle()
    {
        Assert.Equal("Form title is required.", FormService.ValidateDefinition(new SaveFormRequest("  ")));
        Assert.Equal("Request body is required.", FormService.ValidateDefinition(null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2000)]
    [InlineData(2001)]
    [InlineData(4999)]
    [InlineData(5000)]
    public void ValidateDefinition_AcceptsDescriptionWithinMaximum(int length)
    {
        var request = new SaveFormRequest("Signup", new string('a', length));

        Assert.Null(FormService.ValidateDefinition(request));
    }

    [Fact]
    public void ValidateDefinition_AcceptsNullDescription()
    {
        var request = new SaveFormRequest("Signup", null);

        Assert.Null(FormService.ValidateDefinition(request));
    }

    [Theory]
    [InlineData(5001)]
    [InlineData(6000)]
    public void ValidateDefinition_RejectsDescriptionAboveMaximum(int length)
    {
        var request = new SaveFormRequest("Signup", new string('a', length));

        Assert.Equal("Description must be 5000 characters or fewer.", FormService.ValidateDefinition(request));
    }

    [Fact]
    public void ValidateDefinition_RequiresFieldLabels()
    {
        var request = new SaveFormRequest("Signup", Fields: [new FormFieldRequest(null, " ", FormFieldType.Text)]);

        Assert.Equal("Field 1 needs a label.", FormService.ValidateDefinition(request));
    }

    [Fact]
    public void ValidateDefinition_DropdownNeedsOptions()
    {
        var request = new SaveFormRequest("Signup",
            Fields: [new FormFieldRequest(null, "Major", FormFieldType.Dropdown, Options: [" ", ""])]);

        Assert.Contains("needs at least one option", FormService.ValidateDefinition(request));
    }

    [Fact]
    public void ValidateDefinition_RejectsTooManyFieldsAndDuplicateIds()
    {
        var many = Enumerable.Range(0, FormService.MaxFields + 1)
            .Select(i => new FormFieldRequest(null, $"F{i}", FormFieldType.Text))
            .ToList();
        var id = Guid.NewGuid();
        var duplicates = new List<FormFieldRequest>
        {
            new(id, "A", FormFieldType.Text),
            new(id, "B", FormFieldType.Text)
        };

        Assert.Contains("at most", FormService.ValidateDefinition(new SaveFormRequest("X", Fields: many)));
        Assert.Equal("Field ids must be unique.", FormService.ValidateDefinition(new SaveFormRequest("X", Fields: duplicates)));
    }

    [Fact]
    public void ValidateDefinition_AcceptsStarterPresets()
    {
        Assert.Null(FormService.ValidateDefinition(new SaveFormRequest("Signup", Fields: FormFieldPresets.Starter())));
    }

    [Fact]
    public void ApplyDefinition_TrimsValuesPreservesIdsAndOrdersFields()
    {
        var existingId = Guid.NewGuid();
        var form = new Form();
        var request = new SaveFormRequest(" Signup ", "  ",
        [
            new FormFieldRequest(existingId, " Email ", FormFieldType.Email, true, "  "),
            new FormFieldRequest(null, "Major", FormFieldType.Dropdown, Options: ["CS", " cs ", "", "IT"]),
            new FormFieldRequest(null, "Name", FormFieldType.Text, Options: ["ignored"])
        ]);

        FormService.ApplyDefinition(form, request);

        Assert.Equal("Signup", form.Title);
        Assert.Null(form.Description);
        Assert.Equal(existingId, form.Fields[0].Id);
        Assert.Equal("Email", form.Fields[0].Label);
        Assert.Null(form.Fields[0].Placeholder);
        Assert.Equal(["CS", "IT"], form.Fields[1].Options);
        Assert.Empty(form.Fields[2].Options);
        Assert.Equal([0, 1, 2], form.Fields.Select(f => f.Order));
        Assert.NotEqual(Guid.Empty, form.Fields[2].Id);
    }

    [Fact]
    public void ValidateSubmission_ReportsRequiredAndInvalidFields()
    {
        var form = BuildForm(
            Field("Name", FormFieldType.Text, required: true),
            Field("Email", FormFieldType.Email),
            Field("Phone", FormFieldType.Phone),
            Field("Age", FormFieldType.Number),
            Field("Birthday", FormFieldType.Date),
            Field("Major", FormFieldType.Dropdown, options: ["CS", "IT"]),
            Field("Open Source", FormFieldType.YesNo),
            Field("Agree", FormFieldType.Checkbox, required: true));
        var values = Values(form,
            ("Email", "not-an-email"),
            ("Phone", "abc"),
            ("Age", "ten"),
            ("Birthday", "not a date"),
            ("Major", "Art"),
            ("Open Source", "maybe"),
            ("Agree", "false"));

        var errors = FormService.ValidateSubmission(form, values, out _);

        Assert.Equal(8, errors.Count);
        Assert.Equal("Name is required.", errors[Key(form, "Name")]);
        Assert.Equal("Agree must be checked.", errors[Key(form, "Agree")]);
        Assert.Contains("valid email", errors[Key(form, "Email")]);
    }

    [Fact]
    public void ValidateSubmission_NormalizesValidValues()
    {
        var form = BuildForm(
            Field("Name", FormFieldType.Text, required: true),
            Field("Email", FormFieldType.Email),
            Field("Phone", FormFieldType.Phone),
            Field("Age", FormFieldType.Number),
            Field("Birthday", FormFieldType.Date),
            Field("Major", FormFieldType.Dropdown, options: ["Computer Science", "IT"]),
            Field("Open Source", FormFieldType.YesNo),
            Field("Newsletter", FormFieldType.Checkbox),
            Field("Notes", FormFieldType.TextArea));
        var values = Values(form,
            ("Name", "  Ada  "),
            ("Email", "ada@example.edu"),
            ("Phone", "+1 (252) 555-1234"),
            ("Age", "21.50"),
            ("Birthday", "2004-02-29"),
            ("Major", "computer science"),
            ("Open Source", "yes"),
            ("Newsletter", "true"));

        var errors = FormService.ValidateSubmission(form, values, out var normalized);

        Assert.Empty(errors);
        Assert.Equal("Ada", normalized[Key(form, "Name")]);
        Assert.Equal("21.50", normalized[Key(form, "Age")]);
        Assert.Equal("2004-02-29", normalized[Key(form, "Birthday")]);
        Assert.Equal("Computer Science", normalized[Key(form, "Major")]);
        Assert.Equal("Yes", normalized[Key(form, "Open Source")]);
        Assert.Equal("Yes", normalized[Key(form, "Newsletter")]);
        Assert.False(normalized.ContainsKey(Key(form, "Notes")));
    }

    [Fact]
    public void ValidateSubmission_IgnoresUnknownKeysAndLimitsLength()
    {
        var form = BuildForm(Field("Name", FormFieldType.Text));
        var values = new Dictionary<string, string>
        {
            [Key(form, "Name")] = new string('a', FormService.MaxValueLength + 1),
            ["unknown"] = "x"
        };

        var errors = FormService.ValidateSubmission(form, values, out var normalized);

        Assert.Single(errors);
        Assert.DoesNotContain("unknown", normalized.Keys);
    }

    [Fact]
    public void ValidateSubmission_UncheckedOptionalCheckboxIsNo()
    {
        var form = BuildForm(Field("Newsletter", FormFieldType.Checkbox));

        var errors = FormService.ValidateSubmission(form, null, out var normalized);

        Assert.Empty(errors);
        Assert.Equal("No", normalized[Key(form, "Newsletter")]);
    }

    [Fact]
    public void ToPublic_OrdersFieldsAndSuppliesYesNoOptions()
    {
        var form = BuildForm(Field("Second", FormFieldType.YesNo), Field("First", FormFieldType.Text));
        form.Fields[0].Order = 1;
        form.Fields[1].Order = 0;

        var result = FormService.ToPublic(form);

        Assert.Equal(["First", "Second"], result.Fields.Select(f => f.Label));
        Assert.Equal(["Yes", "No"], result.Fields[1].Options);
    }

    [Fact]
    public void ExportCsv_WritesHeaderAndRowsInFormOrder()
    {
        var form = BuildForm(Field("First Name", FormFieldType.Text), Field("Notes", FormFieldType.TextArea));
        var older = new FormSubmission
        {
            FormId = form.Id,
            SubmittedAt = new DateTime(2025, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            Values = Values(form, ("First Name", "Ada"), ("Notes", "Likes \"Rust\", C#"))
        };
        var newer = new FormSubmission
        {
            FormId = form.Id,
            SubmittedAt = new DateTime(2025, 1, 2, 9, 30, 0, DateTimeKind.Utc),
            Values = Values(form, ("First Name", "Grace"))
        };

        var csv = FormService.ExportCsv(form, [newer, older]);

        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Submitted At (UTC),First Name,Notes", lines[0]);
        Assert.Equal("2025-01-01 08:00:00,Ada,\"Likes \"\"Rust\"\", C#\"", lines[1]);
        Assert.Equal("2025-01-02 09:30:00,Grace,", lines[2]);
    }

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("-5", "-5")]
    [InlineData("+1 252 555 1234", "'+1 252 555 1234")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("line\nbreak", "\"line\nbreak\"")]
    [InlineData(null, "")]
    public void EscapeCsv_QuotesAndGuardsFormulas(string? input, string expected)
    {
        Assert.Equal(expected, FormService.EscapeCsv(input));
    }

    [Theory]
    [InlineData("Club Signup!", "Club-Signup-submissions.csv")]
    [InlineData("???", "form-submissions.csv")]
    public void ExportFileName_IsFileSystemSafe(string title, string expected)
    {
        Assert.Equal(expected, FormService.ExportFileName(new Form { Title = title }));
    }

    [Fact]
    public void Starter_ContainsCommonContactFields()
    {
        var labels = FormFieldPresets.Starter().Select(f => f.Label).ToList();

        Assert.Equal(["First Name", "Last Name", "Email", "Degree Major", "Interested in Open Source Project"], labels);
        Assert.Equal(FormFieldType.Email, FormFieldPresets.Starter()[2].FieldType);
    }

    internal static Form BuildForm(params FormField[] fields)
    {
        for (var i = 0; i < fields.Length; i++)
            fields[i].Order = i;
        return new Form { Title = "Signup", JoinCode = "ABC123", InstructorCode = "INST", Fields = [.. fields] };
    }

    internal static FormField Field(string label, FormFieldType type, bool required = false, List<string>? options = null) =>
        new() { Label = label, FieldType = type, Required = required, Options = options ?? [] };

    internal static string Key(Form form, string label) =>
        form.Fields.Single(f => f.Label == label).Id.ToString();

    internal static Dictionary<string, string> Values(Form form, params (string Label, string Value)[] entries) =>
        entries.ToDictionary(e => Key(form, e.Label), e => e.Value);
}
