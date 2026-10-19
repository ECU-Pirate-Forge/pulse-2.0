namespace Pulse.Domain.Entities;

public enum QuestionType
{
    MultipleChoice = 0,
    LegacyLikertScale = 1,
    LegacyOpenEnded = 2,
    LikertScale = LegacyLikertScale,
    OpenEnded = LegacyOpenEnded,
    MultipleAnswer = 3,
    TrueFalse = 4,
    Matching = 5
}
