using System.Text.Json.Serialization;
using Pulse.Shared.Models;

namespace Pulse.Shared.Contracts.V1;

public sealed record SurveyAnswerRequest(
    [property: JsonPropertyName("questionId")] Guid QuestionId,
    [property: JsonPropertyName("values")] IReadOnlyList<string> Values);

public sealed record SubmitSurveyRequest(
    [property: JsonPropertyName("deviceId")] string DeviceId,
    [property: JsonPropertyName("answers")] IReadOnlyList<SurveyAnswerRequest> Answers);

public sealed record SubmitSurveyResponse(
    [property: JsonPropertyName("submittedAt")] DateTime SubmittedAt);

public sealed record SessionSummaryResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("mode")] SessionMode Mode,
    [property: JsonPropertyName("status")] SessionStatus Status,
    [property: JsonPropertyName("resultsVisibility")] ResultsVisibility ResultsVisibility);

public sealed record StudentResultsStatusResponse(
    [property: JsonPropertyName("resultsVisibility")] ResultsVisibility ResultsVisibility);

public sealed record OptionTallyResponse(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("count")] int Count);

public sealed record SurveyQuestionResultsResponse(
    [property: JsonPropertyName("questionId")] Guid QuestionId,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("tallies")] IReadOnlyList<OptionTallyResponse> Tallies,
    [property: JsonPropertyName("responseCount")] int ResponseCount);

public sealed record InstructorSurveyResultsResponse(
    [property: JsonPropertyName("sessionId")] Guid SessionId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("participantCount")] int ParticipantCount,
    [property: JsonPropertyName("completedCount")] int CompletedCount,
    [property: JsonPropertyName("questions")] IReadOnlyList<SurveyQuestionResultsResponse> Questions);

public sealed record StudentSurveyResultsResponse(
    [property: JsonPropertyName("sessionId")] Guid SessionId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("questions")] IReadOnlyList<SurveyQuestionResultsResponse> Questions);
