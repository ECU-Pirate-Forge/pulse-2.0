using System.Text.Json;
using Pulse.Application.Services;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;

namespace Pulse.WebApi;

public static class SurveySubmissionEndpointHandlers
{
    public static async Task<IResult> SubmitSurvey(
        Guid sessionId,
        SubmitSurveyRequest request,
        ISessionRepository sessionRepo,
        QuestionRepository questionRepo,
        IResponseRepository responseRepo,
        DeviceIdValidationService deviceIdService,
        ILoggerFactory loggerFactory,
        HttpContext context)
    {
        var deviceValidation = deviceIdService.ValidateDeviceId(request.DeviceId);
        if (!deviceValidation.IsValid)
            return Results.BadRequest(deviceValidation.ErrorMessage);

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
            return Results.NotFound("Session not found.");

        if (session.Status != SessionStatus.Active)
            return Results.Conflict("Session is not active.");

        var questions = questionRepo.GetBySessionId(sessionId)
            .OrderBy(question => question.SortOrder)
            .ToList();
        var validationError = ValidateAnswers(questions, request.Answers);
        if (validationError is not null)
            return Results.BadRequest(validationError);

        var submittedAt = DateTime.UtcNow;
        var canonicalDeviceId = Guid.Parse(request.DeviceId).ToString();
        var answersByQuestion = request.Answers.ToDictionary(answer => answer.QuestionId);
        var responses = questions.Select(question =>
        {
            var values = NormalizeValues(question, answersByQuestion[question.Id].Values);
            return new Response
            {
                SessionId = sessionId,
                QuestionId = question.Id,
                DeviceId = canonicalDeviceId,
                Value = JsonSerializer.Serialize(values),
                SubmittedAt = submittedAt
            };
        }).ToList();

        responseRepo.SubmitSurvey(
            responses,
            new SurveySubmission
            {
                SessionId = sessionId,
                DeviceId = canonicalDeviceId,
                SubmittedAt = submittedAt
            });

        loggerFactory.CreateLogger("SurveySubmissionEndpointHandlers")
            .LogInformation(
                "Survey submitted SessionId={SessionId} AnswerCount={AnswerCount} CorrelationId={CorrelationId}",
                sessionId,
                responses.Count,
                context.TraceIdentifier);

        return Results.Ok(new SubmitSurveyResponse(submittedAt));
    }

    private static string? ValidateAnswers(
        IReadOnlyCollection<Question> questions,
        IReadOnlyCollection<SurveyAnswerRequest>? answers)
    {
        if (questions.Count == 0)
            return "The survey does not contain any questions.";

        if (answers is null || answers.Count != questions.Count)
            return "Every survey question must be answered exactly once.";

        if (answers.Select(answer => answer.QuestionId).Distinct().Count() != answers.Count)
            return "Each survey question may only be answered once.";

        var questionsById = questions.ToDictionary(question => question.Id);
        foreach (var answer in answers)
        {
            if (!questionsById.TryGetValue(answer.QuestionId, out var question))
                return "An answer does not belong to this survey.";

            var error = ValidateAnswer(question, answer.Values);
            if (error is not null)
                return error;
        }

        return null;
    }

    private static string? ValidateAnswer(Question question, IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0 || values.Any(string.IsNullOrWhiteSpace))
            return $"Question {question.Id} requires an answer.";

        if (values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count)
            return $"Question {question.Id} contains duplicate selections.";

        switch (question.Type)
        {
            case QuestionType.MultipleChoice:
                return values.Count == 1 && ContainsAll(question.Options, values)
                    ? null
                    : $"Question {question.Id} requires one valid option.";
            case QuestionType.MultipleAnswer:
                return ContainsAll(question.Options, values)
                    ? null
                    : $"Question {question.Id} contains an invalid option.";
            case QuestionType.TrueFalse:
                return values.Count == 1 &&
                    (values[0].Equals("True", StringComparison.OrdinalIgnoreCase) ||
                     values[0].Equals("False", StringComparison.OrdinalIgnoreCase))
                    ? null
                    : $"Question {question.Id} requires True or False.";
            case QuestionType.Matching:
                var choices = question.MatchingPairs.Select(pair => pair.Match).ToList();
                return values.Count == question.MatchingPairs.Count &&
                    ContainsAll(choices, values) &&
                    values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == choices.Count
                    ? null
                    : $"Question {question.Id} requires one valid match for every prompt.";
            default:
                return $"Question {question.Id} uses an unsupported type.";
        }
    }

    private static bool ContainsAll(
        IReadOnlyCollection<string> allowedValues,
        IReadOnlyCollection<string> submittedValues) =>
        submittedValues.All(value =>
            allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase));

    private static List<string> NormalizeValues(
        Question question,
        IReadOnlyList<string> values) =>
        question.Type switch
        {
            QuestionType.TrueFalse =>
                [values[0].Equals("True", StringComparison.OrdinalIgnoreCase) ? "True" : "False"],
            QuestionType.MultipleChoice or QuestionType.MultipleAnswer =>
                question.Options.Where(option =>
                    values.Contains(option, StringComparer.OrdinalIgnoreCase)).ToList(),
            QuestionType.Matching => values.Select(value =>
                question.MatchingPairs.Select(pair => pair.Match)
                    .Single(choice => choice.Equals(value, StringComparison.OrdinalIgnoreCase))).ToList(),
            _ => values.ToList()
        };
}
