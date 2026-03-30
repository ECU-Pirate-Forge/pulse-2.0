using Pulse.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Pulse.Tests.Tests;

public class QuestionModelTests
{
    [Fact]
    public void QuestionCanBeInstantiated()
    {
        var question = new Question
        {
            Text = "How clear was today's topic?"
        };

        Assert.NotNull(question);
    }

    [Fact]
    public void QuestionDefaultsInitializeExpectedValues()
    {
        var before = DateTime.UtcNow;

        var question = new Question
        {
            Text = "Default checks"
        };

        var after = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, question.Id);
        Assert.NotNull(question.Options);
        Assert.Empty(question.Options);
        Assert.InRange(question.CreatedAt, before, after);
    }

    [Fact]
    public void QuestionPropertiesCanBeSetAndRetrieved()
    {
        var id = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddMinutes(-5);

        var question = new Question
        {
            Id = id,
            SessionId = sessionId,
            Text = "Which option is correct?",
            Type = QuestionType.MultipleChoice,
            Options = new List<string> { "A", "B", "C" },
            SortOrder = 3,
            CreatedAt = createdAt
        };

        Assert.Equal(id, question.Id);
        Assert.Equal(sessionId, question.SessionId);
        Assert.Equal("Which option is correct?", question.Text);
        Assert.Equal(QuestionType.MultipleChoice, question.Type);
        Assert.Equal(new[] { "A", "B", "C" }, question.Options);
        Assert.Equal(3, question.SortOrder);
        Assert.Equal(createdAt, question.CreatedAt);
    }

    [Fact]
    public void OptionsSupportsMultipleValuesAndPreservesOrder()
    {
        var question = new Question
        {
            Text = "Select your preferred language",
            Type = QuestionType.MultipleChoice,
            Options = new List<string> { "C#", "Java", "Python" }
        };

        Assert.Equal(3, question.Options.Count);
        Assert.Equal(new[] { "C#", "Java", "Python" }, question.Options);
    }

    [Fact]
    public void RequiredTextValidationFailsWhenTextIsMissing()
    {
        var question = new Question
        {
            Text = null!
        };

        var validationContext = new ValidationContext(question);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(question, validationContext, validationResults, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(Question.Text)));
    }
}
