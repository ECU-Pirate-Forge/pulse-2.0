using LiteDB;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;

namespace Pulse.WebApi.Features.SurveyGeneration;

public sealed class SurveyGenerationRepository(LiteDatabase database)
{
    public SurveyGrounding GetGrounding(Guid sessionId) =>
        database.GetCollection<SurveyGrounding>("survey_grounding").FindById(sessionId)
        ?? new SurveyGrounding { Id = sessionId };

    public SurveyGrounding SaveGrounding(SurveyGrounding material)
    {
        if (!database.BeginTrans())
            throw new InvalidOperationException("Could not start grounding transaction.");
        try
        {
            var collection = database.GetCollection<SurveyGrounding>("survey_grounding");
            var existing = collection.FindById(material.Id);
            if (existing is not null && existing.Revision != material.Revision)
                throw new GroundingConflictException("Grounding was changed in another request. Reload before saving.");
            material.Revision = Guid.NewGuid();
            collection.Upsert(material);
            database.GetCollection<SurveyGenerationPreview>("survey_generation_previews")
                .DeleteMany(preview => preview.SessionId == material.Id);
            if (!database.Commit())
                throw new InvalidOperationException("Could not commit grounding.");
            return material;
        }
        catch
        {
            database.Rollback();
            throw;
        }
    }

    public SurveyGenerationPreview SavePreview(Guid sessionId, Guid revision, List<GeneratedSurveyQuestion> questions)
    {
        if (!database.BeginTrans())
            throw new InvalidOperationException("Could not start generation-preview transaction.");
        try
        {
            if (GetGrounding(sessionId).Revision != revision)
                throw new GroundingConflictException("Grounding changed during generation. Generate a new preview.");
            var preview = new SurveyGenerationPreview
            {
                SessionId = sessionId, Revision = revision, Questions = questions
            };
            var collection = database.GetCollection<SurveyGenerationPreview>("survey_generation_previews");
            collection.DeleteMany(item => item.SessionId == sessionId || item.CreatedAt < DateTime.Now.AddHours(-1));
            collection.Insert(preview);
            if (!database.Commit())
                throw new InvalidOperationException("Could not commit generation preview.");
            return preview;
        }
        catch
        {
            database.Rollback();
            throw;
        }
    }

    public IReadOnlyList<Question> Accept(Guid sessionId, Guid previewId)
    {
        if (!database.BeginTrans())
            throw new InvalidOperationException("Could not start generated-question transaction.");
        try
        {
            var preview = database.GetCollection<SurveyGenerationPreview>("survey_generation_previews").FindById(previewId);
            if (preview is null || preview.SessionId != sessionId)
                throw new GroundingValidationException("This preview was not found. Generate a new preview.");
            var material = GetGrounding(sessionId);
            if (preview.Revision != material.Revision || preview.CreatedAt.ToUniversalTime() < DateTime.UtcNow.AddHours(-1))
                throw new GroundingValidationException("This preview is out of date. Generate a new preview.");
            var session = database.GetCollection<Session>("sessions").FindOne(item => item.Id == sessionId);
            if (session?.Status != SessionStatus.Draft)
                throw new GroundingValidationException("Questions can only be saved while the survey is in Draft.");
            var collection = database.GetCollection<Question>("questions");
            var sortOrder = collection.Find(question => question.SessionId == sessionId)
                .Select(question => question.SortOrder).DefaultIfEmpty(-1).Max() + 1;
            var created = preview.Questions.Select((question, index) => new Question
            {
                SessionId = sessionId, Text = question.Text, Type = QuestionType.MultipleChoice,
                Options = question.Options, CorrectAnswers = [question.CorrectAnswer],
                Explanation = question.Explanation, SourceId = question.SourceId,
                SourceQuote = question.SourceQuote, SortOrder = sortOrder + index
            }).ToList();
            collection.InsertBulk(created);
            database.GetCollection<SurveyGenerationPreview>("survey_generation_previews").Delete(previewId);
            if (!database.Commit())
                throw new InvalidOperationException("Could not commit generated questions.");
            return created;
        }
        catch
        {
            database.Rollback();
            throw;
        }
    }
}

public sealed class SurveyGenerationPreview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid Revision { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<GeneratedSurveyQuestion> Questions { get; set; } = [];
}

public sealed class GroundingConflictException(string message) : Exception(message);
