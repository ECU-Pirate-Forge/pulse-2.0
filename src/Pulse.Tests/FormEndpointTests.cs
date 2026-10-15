using LiteDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Pulse.Common.Services;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi;
using Pulse.WebApi.Middleware;

namespace Pulse.Tests.Tests;

public sealed class FormEndpointTests : IDisposable
{
    private readonly LiteDatabase database = new("Filename=:memory:");
    private readonly FormRepository repository;
    private readonly Mock<IJoinCodeGenerator> joinCodes = new();

    public FormEndpointTests()
    {
        repository = new FormRepository(database);
        joinCodes.SetupSequence(g => g.Generate())
            .Returns("AAA111")
            .Returns("BBB222")
            .Returns("CCC333");
    }

    public void Dispose() => database.Dispose();

    private static DefaultHttpContext Context(string? instructorCode = "INST")
    {
        var context = new DefaultHttpContext();
        if (instructorCode is not null)
            context.Items[InstructorCodeMiddleware.HeaderName] = instructorCode;
        return context;
    }

    private static SaveFormRequest StarterRequest(string title = "Club Signup") =>
        new(title, "Join us", FormFieldPresets.Starter());

    private Form CreateForm(string title = "Club Signup", string instructor = "INST")
    {
        var result = FormEndpointHandlers.CreateForm(StarterRequest(title), Context(instructor), repository, joinCodes.Object);
        return Assert.IsType<Created<Form>>(result).Value!;
    }

    private Form CreateOpenForm()
    {
        var form = CreateForm();
        Assert.IsType<Ok<Form>>(FormEndpointHandlers.OpenForm(form.Id, Context(), repository));
        return form;
    }

    [Fact]
    public void AdminEndpoints_WithoutInstructorCode_ReturnUnauthorized()
    {
        var id = Guid.NewGuid();
        var anonymous = Context(null);

        Assert.IsType<UnauthorizedHttpResult>(FormEndpointHandlers.GetForms(anonymous, repository));
        Assert.IsType<UnauthorizedHttpResult>(FormEndpointHandlers.CreateForm(StarterRequest(), anonymous, repository, joinCodes.Object));
        Assert.IsType<UnauthorizedHttpResult>(FormEndpointHandlers.GetForm(id, anonymous, repository));
        Assert.IsType<UnauthorizedHttpResult>(FormEndpointHandlers.UpdateForm(id, StarterRequest(), anonymous, repository));
        Assert.IsType<UnauthorizedHttpResult>(FormEndpointHandlers.DeleteForm(id, anonymous, repository));
        Assert.IsType<UnauthorizedHttpResult>(FormEndpointHandlers.OpenForm(id, anonymous, repository));
        Assert.IsType<UnauthorizedHttpResult>(FormEndpointHandlers.GetSubmissions(id, anonymous, repository));
        Assert.IsType<UnauthorizedHttpResult>(FormEndpointHandlers.ExportSubmissions(id, anonymous, repository));
    }

    [Fact]
    public void CreateForm_PersistsDraftWithUniqueJoinCode()
    {
        repository.Insert(new Form { Title = "Existing", JoinCode = "AAA111", InstructorCode = "OTHER" });

        var form = CreateForm();

        Assert.Equal("BBB222", form.JoinCode);
        Assert.Equal(FormStatus.Draft, form.Status);
        Assert.Equal("INST", form.InstructorCode);
        Assert.Equal(5, form.Fields.Count);
        Assert.NotNull(repository.Get(form.Id));
    }

    [Fact]
    public void CreateForm_InvalidDefinition_ReturnsBadRequest()
    {
        var result = FormEndpointHandlers.CreateForm(new SaveFormRequest(""), Context(), repository, joinCodes.Object);

        Assert.Equal("Form title is required.", Assert.IsType<BadRequest<string>>(result).Value);
    }

    [Fact]
    public void GetForms_ReturnsOnlyCallersFormsWithCounts()
    {
        var mine = CreateOpenForm();
        CreateForm("Someone else's", "OTHER");
        repository.InsertSubmission(new FormSubmission { FormId = mine.Id });

        var result = Assert.IsType<Ok<List<FormSummary>>>(FormEndpointHandlers.GetForms(Context(), repository));

        var summary = Assert.Single(result.Value!);
        Assert.Equal(mine.Id, summary.Id);
        Assert.Equal(5, summary.FieldCount);
        Assert.Equal(1, summary.SubmissionCount);
        Assert.Equal(FormStatus.Open, summary.Status);
    }

    [Fact]
    public void OtherInstructorsForm_IsNotFound()
    {
        var form = CreateForm(instructor: "OTHER");

        Assert.IsType<NotFound>(FormEndpointHandlers.GetForm(form.Id, Context(), repository));
        Assert.IsType<NotFound>(FormEndpointHandlers.UpdateForm(form.Id, StarterRequest(), Context(), repository));
        Assert.IsType<NotFound>(FormEndpointHandlers.DeleteForm(form.Id, Context(), repository));
        Assert.IsType<NotFound>(FormEndpointHandlers.CloseForm(form.Id, Context(), repository));
        Assert.IsType<NotFound>(FormEndpointHandlers.ExportSubmissions(form.Id, Context(), repository));
        Assert.NotNull(repository.Get(form.Id));
    }

    [Fact]
    public void UpdateForm_ReplacesFieldsAndKeepsIds()
    {
        var form = CreateForm();
        var keep = form.Fields[0];
        var request = new SaveFormRequest("Renamed", null,
        [
            new FormFieldRequest(keep.Id, "Given Name", FormFieldType.Text, true),
            new FormFieldRequest(null, "Year", FormFieldType.Dropdown, Options: ["Freshman", "Senior"])
        ]);

        var result = Assert.IsType<Ok<Form>>(FormEndpointHandlers.UpdateForm(form.Id, request, Context(), repository));

        Assert.Equal("Renamed", result.Value!.Title);
        var stored = repository.Get(form.Id)!;
        Assert.Equal(2, stored.Fields.Count);
        Assert.Equal(keep.Id, stored.Fields[0].Id);
        Assert.Equal("Given Name", stored.Fields[0].Label);
    }

    [Fact]
    public void OpenForm_WithoutFields_ReturnsBadRequest()
    {
        var created = FormEndpointHandlers.CreateForm(new SaveFormRequest("Empty"), Context(), repository, joinCodes.Object);
        var form = Assert.IsType<Created<Form>>(created).Value!;

        Assert.IsType<BadRequest<string>>(FormEndpointHandlers.OpenForm(form.Id, Context(), repository));
        Assert.Equal(FormStatus.Draft, repository.Get(form.Id)!.Status);
    }

    [Fact]
    public void OpenAndCloseForm_UpdateStatus()
    {
        var form = CreateOpenForm();
        Assert.Equal(FormStatus.Open, repository.Get(form.Id)!.Status);

        Assert.IsType<Ok<Form>>(FormEndpointHandlers.CloseForm(form.Id, Context(), repository));
        Assert.Equal(FormStatus.Closed, repository.Get(form.Id)!.Status);
    }

    [Fact]
    public void GetPublicForm_HandlesMissingDraftClosedAndOpen()
    {
        Assert.IsType<NotFound>(FormEndpointHandlers.GetPublicForm("NOPE00", repository));

        var form = CreateForm();
        Assert.Contains("not open yet",
            Assert.IsType<Conflict<string>>(FormEndpointHandlers.GetPublicForm(form.JoinCode, repository)).Value);

        FormEndpointHandlers.OpenForm(form.Id, Context(), repository);
        var open = Assert.IsType<Ok<PublicFormResponse>>(FormEndpointHandlers.GetPublicForm(form.JoinCode.ToLowerInvariant(), repository));
        Assert.Equal("Club Signup", open.Value!.Title);
        Assert.Equal(5, open.Value.Fields.Count);

        FormEndpointHandlers.CloseForm(form.Id, Context(), repository);
        Assert.Contains("closed",
            Assert.IsType<Conflict<string>>(FormEndpointHandlers.GetPublicForm(form.JoinCode, repository)).Value);
    }

    [Fact]
    public void SubmitForm_InvalidValues_ReturnsFieldErrors()
    {
        var form = CreateOpenForm();
        var email = form.Fields.Single(f => f.Label == "Email").Id.ToString();
        var request = new SubmitFormRequest(new Dictionary<string, string> { [email] = "bad" });

        var result = Assert.IsType<BadRequest<FormValidationErrorResponse>>(
            FormEndpointHandlers.SubmitForm(form.JoinCode, request, repository));

        Assert.Equal(3, result.Value!.FieldErrors.Count);
        Assert.Contains(email, result.Value.FieldErrors.Keys);
        Assert.Equal(0, repository.CountSubmissions(form.Id));
    }

    [Fact]
    public void SubmitForm_ValidValues_StoresSubmission()
    {
        var form = CreateOpenForm();
        var request = new SubmitFormRequest(Values(form,
            ("First Name", "Ada"), ("Last Name", "Lovelace"), ("Email", "ada@example.edu"),
            ("Degree Major", "Computer Science"), ("Interested in Open Source Project", "yes")));

        var result = Assert.IsType<Ok<SubmitFormResponse>>(FormEndpointHandlers.SubmitForm(form.JoinCode, request, repository));

        var stored = Assert.Single(repository.GetSubmissions(form.Id));
        Assert.Equal(result.Value!.SubmissionId, stored.Id);
        Assert.Equal("Yes", stored.Values[Key(form, "Interested in Open Source Project")]);
    }

    [Fact]
    public void SubmitForm_ClosedOrMissingForm_IsRejected()
    {
        var form = CreateForm();
        var request = new SubmitFormRequest([]);

        Assert.IsType<NotFound>(FormEndpointHandlers.SubmitForm("NOPE00", request, repository));
        Assert.IsType<Conflict<string>>(FormEndpointHandlers.SubmitForm(form.JoinCode, request, repository));
    }

    [Fact]
    public void GetSubmissions_ReturnsStoredSubmissions()
    {
        var form = CreateOpenForm();
        repository.InsertSubmission(new FormSubmission { FormId = form.Id });

        var result = Assert.IsType<Ok<IReadOnlyList<FormSubmission>>>(
            FormEndpointHandlers.GetSubmissions(form.Id, Context(), repository));

        Assert.Single(result.Value!);
    }

    [Fact]
    public void ExportSubmissions_ReturnsCsvFileWithBom()
    {
        var form = CreateOpenForm();
        FormEndpointHandlers.SubmitForm(form.JoinCode, new SubmitFormRequest(Values(form,
            ("First Name", "Ada"), ("Last Name", "Lovelace"), ("Email", "ada@example.edu"))), repository);

        var file = Assert.IsType<FileContentHttpResult>(FormEndpointHandlers.ExportSubmissions(form.Id, Context(), repository));

        Assert.Equal("text/csv", file.ContentType);
        Assert.Equal("Club-Signup-submissions.csv", file.FileDownloadName);
        var bytes = file.FileContents.ToArray();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var lines = System.Text.Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Submitted At (UTC),First Name,Last Name,Email,Degree Major,Interested in Open Source Project", lines[0]);
        Assert.EndsWith(",Ada,Lovelace,ada@example.edu,,", lines[1]);
    }

    [Fact]
    public void DeleteForm_RemovesFormAndSubmissions()
    {
        var form = CreateOpenForm();
        repository.InsertSubmission(new FormSubmission { FormId = form.Id });

        Assert.IsType<NoContent>(FormEndpointHandlers.DeleteForm(form.Id, Context(), repository));

        Assert.Null(repository.Get(form.Id));
        Assert.Equal(0, repository.CountSubmissions(form.Id));
    }

    [Fact]
    public void BuildFormUrl_PrefersConfiguredBase()
    {
        var request = Context().Request;
        request.Scheme = "https";
        request.Host = new HostString("api.example.com");

        Assert.Equal("https://pulse.example.com/form/ABC123",
            FormEndpointHandlers.BuildFormUrl("https://pulse.example.com/", request, "ABC123"));
        Assert.Equal("https://api.example.com/form/ABC123",
            FormEndpointHandlers.BuildFormUrl(null, request, "ABC123"));
    }

    [Fact]
    public void Repository_GetByJoinCode_IsCaseInsensitiveAndTrimmed()
    {
        var form = CreateForm();

        Assert.Equal(form.Id, repository.GetByJoinCode($" {form.JoinCode.ToLowerInvariant()} ")!.Id);
    }

    [Theory]
    [InlineData("GET", "/api/forms", true)]
    [InlineData("POST", "/api/forms", true)]
    [InlineData("GET", "/api/forms/00000000-0000-0000-0000-000000000001/export", true)]
    [InlineData("GET", "/api/forms/join/ABC123", false)]
    [InlineData("POST", "/api/forms/join/ABC123/submit", false)]
    public void Middleware_ProtectsFormAdminRoutesOnly(string method, string path, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        Assert.Equal(expected, InstructorOnlyEndpointMatcher.IsInstructorOnly(context.Request));
    }

    private static string Key(Form form, string label) =>
        form.Fields.Single(f => f.Label == label).Id.ToString();

    private static Dictionary<string, string> Values(Form form, params (string Label, string Value)[] entries) =>
        entries.ToDictionary(e => Key(form, e.Label), e => e.Value);
}
