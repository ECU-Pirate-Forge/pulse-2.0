using LiteDB;
using Pulse.Shared.Models;

namespace Pulse.Common.Services;

public sealed class FormRepository
{
    private readonly ILiteCollection<Form> forms;
    private readonly ILiteCollection<FormSubmission> submissions;

    public FormRepository(LiteDatabase database)
    {
        forms = database.GetCollection<Form>("forms");
        submissions = database.GetCollection<FormSubmission>("form_submissions");
        forms.EnsureIndex(form => form.JoinCode, unique: true);
        forms.EnsureIndex(form => form.InstructorCode);
        submissions.EnsureIndex(submission => submission.FormId);
    }

    public Form Insert(Form form)
    {
        forms.Insert(form);
        return form;
    }

    public bool Update(Form form)
    {
        form.UpdatedAt = DateTime.UtcNow;
        return forms.Update(form);
    }

    public Form? Get(Guid id) => forms.FindById(id);

    public Form? GetByJoinCode(string joinCode)
    {
        var normalized = joinCode.Trim().ToUpperInvariant();
        return forms.FindOne(form => form.JoinCode == normalized);
    }

    public IReadOnlyList<Form> GetByInstructorCode(string instructorCode) =>
        forms.Find(form => form.InstructorCode == instructorCode)
            .OrderByDescending(form => form.CreatedAt)
            .ToList();

    public bool Delete(Guid id)
    {
        submissions.DeleteMany(submission => submission.FormId == id);
        return forms.Delete(id);
    }

    public FormSubmission InsertSubmission(FormSubmission submission)
    {
        submissions.Insert(submission);
        return submission;
    }

    public IReadOnlyList<FormSubmission> GetSubmissions(Guid formId) =>
        submissions.Find(submission => submission.FormId == formId)
            .OrderBy(submission => submission.SubmittedAt)
            .ToList();

    public int CountSubmissions(Guid formId) =>
        submissions.Count(submission => submission.FormId == formId);
}
