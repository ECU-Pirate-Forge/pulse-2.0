using System.Text;
using Pulse.Common.Services;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi.Middleware;
using QRCoder;

namespace Pulse.WebApi;

public static class FormEndpointHandlers
{
    public static IResult GetForms(HttpContext context, FormRepository repository)
    {
        var instructorCode = InstructorCode(context);
        if (instructorCode is null)
            return Results.Unauthorized();

        var summaries = repository.GetByInstructorCode(instructorCode)
            .Select(form => new FormSummary(
                form.Id,
                form.Title,
                form.JoinCode,
                form.Status,
                form.Fields.Count,
                repository.CountSubmissions(form.Id),
                form.CreatedAt))
            .ToList();
        return Results.Ok(summaries);
    }

    public static IResult CreateForm(
        SaveFormRequest request,
        HttpContext context,
        FormRepository repository,
        IJoinCodeGenerator joinCodeGenerator)
    {
        var instructorCode = InstructorCode(context);
        if (instructorCode is null)
            return Results.Unauthorized();

        var error = FormService.ValidateDefinition(request);
        if (error is not null)
            return Results.BadRequest(error);

        string joinCode;
        do
        {
            joinCode = joinCodeGenerator.Generate();
        } while (repository.GetByJoinCode(joinCode) is not null);

        var form = new Form { InstructorCode = instructorCode, JoinCode = joinCode };
        FormService.ApplyDefinition(form, request);
        repository.Insert(form);
        return Results.Created($"/api/forms/{form.Id}", form);
    }

    public static IResult GetForm(Guid id, HttpContext context, FormRepository repository)
    {
        if (InstructorCode(context) is null)
            return Results.Unauthorized();
        var form = OwnedForm(id, context, repository);
        return form is null ? Results.NotFound() : Results.Ok(form);
    }

    public static IResult UpdateForm(
        Guid id,
        SaveFormRequest request,
        HttpContext context,
        FormRepository repository)
    {
        if (InstructorCode(context) is null)
            return Results.Unauthorized();
        var form = OwnedForm(id, context, repository);
        if (form is null)
            return Results.NotFound();

        var error = FormService.ValidateDefinition(request);
        if (error is not null)
            return Results.BadRequest(error);

        FormService.ApplyDefinition(form, request);
        repository.Update(form);
        return Results.Ok(form);
    }

    public static IResult DeleteForm(Guid id, HttpContext context, FormRepository repository)
    {
        if (InstructorCode(context) is null)
            return Results.Unauthorized();
        var form = OwnedForm(id, context, repository);
        if (form is null)
            return Results.NotFound();

        repository.Delete(id);
        return Results.NoContent();
    }

    public static IResult OpenForm(Guid id, HttpContext context, FormRepository repository) =>
        SetStatus(id, FormStatus.Open, context, repository);

    public static IResult CloseForm(Guid id, HttpContext context, FormRepository repository) =>
        SetStatus(id, FormStatus.Closed, context, repository);

    public static IResult GetSubmissions(Guid id, HttpContext context, FormRepository repository)
    {
        if (InstructorCode(context) is null)
            return Results.Unauthorized();
        var form = OwnedForm(id, context, repository);
        return form is null ? Results.NotFound() : Results.Ok(repository.GetSubmissions(id));
    }

    public static IResult ExportSubmissions(Guid id, HttpContext context, FormRepository repository)
    {
        if (InstructorCode(context) is null)
            return Results.Unauthorized();
        var form = OwnedForm(id, context, repository);
        if (form is null)
            return Results.NotFound();

        var csv = FormService.ExportCsv(form, repository.GetSubmissions(id));
        // UTF-8 BOM so Excel detects the encoding correctly.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return Results.File(bytes, "text/csv", FormService.ExportFileName(form));
    }

    public static IResult GetQrCode(
        Guid id,
        HttpContext context,
        HttpRequest request,
        FormRepository repository,
        IConfiguration configuration)
    {
        if (InstructorCode(context) is null)
            return Results.Unauthorized();
        var form = OwnedForm(id, context, repository);
        if (form is null)
            return Results.NotFound();

        var url = BuildFormUrl(configuration["App:JoinBaseUrl"], request, form.JoinCode);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(20);
        return Results.File(png, "image/png");
    }

    public static IResult GetPublicForm(string joinCode, FormRepository repository)
    {
        var form = repository.GetByJoinCode(joinCode);
        if (form is null)
            return Results.NotFound();
        if (form.Status != FormStatus.Open)
            return Results.Conflict(form.Status == FormStatus.Closed
                ? "This form is closed and no longer accepting responses."
                : "This form is not open yet.");
        return Results.Ok(FormService.ToPublic(form));
    }

    public static IResult SubmitForm(string joinCode, SubmitFormRequest request, FormRepository repository)
    {
        var form = repository.GetByJoinCode(joinCode);
        if (form is null)
            return Results.NotFound();
        if (form.Status != FormStatus.Open)
            return Results.Conflict("This form is not accepting responses.");

        var errors = FormService.ValidateSubmission(form, request?.Values, out var normalized);
        if (errors.Count > 0)
            return Results.BadRequest(new FormValidationErrorResponse("Please correct the highlighted fields.", errors));

        var submission = repository.InsertSubmission(new FormSubmission
        {
            FormId = form.Id,
            Values = normalized
        });
        return Results.Ok(new SubmitFormResponse(submission.Id));
    }

    public static string BuildFormUrl(string? configuredBaseUrl, HttpRequest request, string joinCode)
    {
        var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? $"{request.Scheme}://{request.Host}"
            : configuredBaseUrl.TrimEnd('/');
        return $"{baseUrl}/form/{Uri.EscapeDataString(joinCode)}";
    }

    private static IResult SetStatus(Guid id, FormStatus status, HttpContext context, FormRepository repository)
    {
        if (InstructorCode(context) is null)
            return Results.Unauthorized();
        var form = OwnedForm(id, context, repository);
        if (form is null)
            return Results.NotFound();
        if (status == FormStatus.Open && form.Fields.Count == 0)
            return Results.BadRequest("Add at least one field before opening the form.");

        form.Status = status;
        repository.Update(form);
        return Results.Ok(form);
    }

    private static Form? OwnedForm(Guid id, HttpContext context, FormRepository repository)
    {
        var code = InstructorCode(context);
        var form = repository.Get(id);
        return form is not null && string.Equals(form.InstructorCode, code, StringComparison.Ordinal)
            ? form
            : null;
    }

    private static string? InstructorCode(HttpContext context) =>
        context.Items[InstructorCodeMiddleware.HeaderName]?.ToString();
}
