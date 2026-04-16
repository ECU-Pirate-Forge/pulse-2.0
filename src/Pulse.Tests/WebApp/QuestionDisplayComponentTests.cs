using Xunit;
using Bunit;
using Pulse.WebApp.Components.QuestionDisplay;
using Pulse.Domain.Entities;

namespace Pulse.Tests.WebApp;

public class QuestionDisplayComponentTests : Bunit.TestContext
{
    [Fact]
    public void QuestionDisplay_Renders_With_MultipleChoice_Question()
    {
        // Arrange
        var question = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Text = "What is your favorite color?",
            Type = QuestionType.MultipleChoice,
            Options = new List<string> { "Red", "Blue", "Green" },
            SortOrder = 1
        };
        var sessionId = Guid.NewGuid();

        // Act
        var cut = Render<QuestionDisplay>(parameters => parameters
            .Add(p => p.Question, question)
            .Add(p => p.SessionId, sessionId));

        // Assert
        Assert.NotNull(cut);
        var markup = cut.Markup;
        Assert.Contains("What is your favorite color?", markup);
        Assert.Contains("multiple-choice-options", markup);
    }

    [Fact]
    public void QuestionDisplay_Renders_With_LikertScale_Question()
    {
        // Arrange
        var question = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Text = "I enjoy this session.",
            Type = QuestionType.LikertScale,
            Options = new List<string>(),
            SortOrder = 1
        };
        var sessionId = Guid.NewGuid();

        // Act
        var cut = Render<QuestionDisplay>(parameters => parameters
            .Add(p => p.Question, question)
            .Add(p => p.SessionId, sessionId));

        // Assert
        var markup = cut.Markup;
        Assert.Contains("I enjoy this session.", markup);
        Assert.Contains("likert-scale-options", markup);
        Assert.Contains("Strongly Disagree", markup);
        Assert.Contains("Strongly Agree", markup);
    }

    [Fact]
    public void QuestionDisplay_Renders_With_OpenEnded_Question()
    {
        // Arrange
        var question = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Text = "What did you learn?",
            Type = QuestionType.OpenEnded,
            Options = new List<string>(),
            SortOrder = 1
        };
        var sessionId = Guid.NewGuid();

        // Act
        var cut = Render<QuestionDisplay>(parameters => parameters
            .Add(p => p.Question, question)
            .Add(p => p.SessionId, sessionId));

        // Assert
        var markup = cut.Markup;
        Assert.Contains("What did you learn?", markup);
        Assert.Contains("open-ended-response", markup);
        Assert.Contains("<textarea", markup);
    }

    [Fact]
    public void QuestionDisplay_Renders_Loading_Message_When_Question_Null()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var cut = Render<QuestionDisplay>(parameters => parameters
            .Add(p => p.Question, (Question?)null)
            .Add(p => p.SessionId, sessionId));

        // Assert
        var markup = cut.Markup;
        Assert.Contains("Loading question", markup);
    }

    [Fact]
    public void QuestionDisplay_Has_Submit_Button()
    {
        // Arrange
        var question = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Text = "Test?",
            Type = QuestionType.MultipleChoice,
            Options = new List<string> { "Yes" },
            SortOrder = 1
        };
        var sessionId = Guid.NewGuid();

        // Act
        var cut = Render<QuestionDisplay>(parameters => parameters
            .Add(p => p.Question, question)
            .Add(p => p.SessionId, sessionId));

        // Assert
        var markup = cut.Markup;
        Assert.Contains("Submit Response", markup);
        Assert.Contains("btn-primary", markup);
    }

    [Fact]
    public void QuestionDisplay_MultipleChoice_Has_Radio_Buttons()
    {
        // Arrange
        var question = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Text = "Pick one",
            Type = QuestionType.MultipleChoice,
            Options = new List<string> { "Option A", "Option B", "Option C" },
            SortOrder = 1
        };
        var sessionId = Guid.NewGuid();

        // Act
        var cut = Render<QuestionDisplay>(parameters => parameters
            .Add(p => p.Question, question)
            .Add(p => p.SessionId, sessionId));

        // Assert
        var markup = cut.Markup;
        Assert.Contains("type=\"radio\"", markup);
        Assert.Contains("Option A", markup);
        Assert.Contains("Option B", markup);
        Assert.Contains("Option C", markup);
        Assert.Contains("form-check-input", markup);
    }

    [Fact]
    public void QuestionDisplay_Shows_All_Likert_Labels()
    {
        // Arrange
        var question = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Text = "Rate your experience",
            Type = QuestionType.LikertScale,
            Options = new List<string>(),
            SortOrder = 1
        };
        var sessionId = Guid.NewGuid();

        // Act
        var cut = Render<QuestionDisplay>(parameters => parameters
            .Add(p => p.Question, question)
            .Add(p => p.SessionId, sessionId));

        // Assert
        var markup = cut.Markup;
        Assert.Contains("Strongly Disagree", markup);
        Assert.Contains("Disagree", markup);
        Assert.Contains("Neutral", markup);
        Assert.Contains("Agree", markup);
        Assert.Contains("Strongly Agree", markup);
    }

    [Fact]
    public void QuestionDisplay_Renders_Question_Container()
    {
        // Arrange
        var question = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Text = "Test question",
            Type = QuestionType.MultipleChoice,
            Options = new List<string> { "A" },
            SortOrder = 1
        };
        var sessionId = Guid.NewGuid();

        // Act
        var cut = Render<QuestionDisplay>(parameters => parameters
            .Add(p => p.Question, question)
            .Add(p => p.SessionId, sessionId));

        // Assert
        var markup = cut.Markup;
        Assert.Contains("question-display", markup);
        Assert.Contains("question-container", markup);
        Assert.Contains("question-text", markup);
        Assert.Contains("response-options", markup);
    }

    [Fact]
    public void QuestionDisplay_Bootstrap_Classes_Present()
    {
        // Arrange
        var question = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Text = "Test",
            Type = QuestionType.MultipleChoice,
            Options = new List<string> { "Yes" },
            SortOrder = 1
        };
        var sessionId = Guid.NewGuid();

        // Act
        var cut = Render<QuestionDisplay>(parameters => parameters
            .Add(p => p.Question, question)
            .Add(p => p.SessionId, sessionId));

        // Assert
        var markup = cut.Markup;
        Assert.Contains("form-check", markup);
        Assert.Contains("btn", markup);
        Assert.Contains("question-display", markup);
    }

    [Fact]
    public void QuestionDisplay_Textarea_Has_Placeholder()
    {
        // Arrange
        var question = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Text = "Your response",
            Type = QuestionType.OpenEnded,
            Options = new List<string>(),
            SortOrder = 1
        };
        var sessionId = Guid.NewGuid();

        // Act
        var cut = Render<QuestionDisplay>(parameters => parameters
            .Add(p => p.Question, question)
            .Add(p => p.SessionId, sessionId));

        // Assert
        var markup = cut.Markup;
        Assert.Contains("placeholder", markup);
        Assert.Contains("Enter your response here", markup);
    }

    [Fact]
    public void QuestionDisplay_All_Question_Types_Render()
    {
        // Test that all three question types can render successfully
        var sessionId = Guid.NewGuid();

        // MultipleChoice
        var mcQuestion = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Text = "MC",
            Type = QuestionType.MultipleChoice,
            Options = new List<string> { "A", "B" },
            SortOrder = 1
        };
        var mcCut = Render<QuestionDisplay>(parameters => parameters
            .Add(p => p.Question, mcQuestion)
            .Add(p => p.SessionId, sessionId));
        Assert.Contains("multiple-choice-options", mcCut.Markup);

        // LikertScale
        var lsQuestion = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Text = "LS",
            Type = QuestionType.LikertScale,
            Options = new List<string>(),
            SortOrder = 2
        };
        var lsCut = Render<QuestionDisplay>(parameters => parameters
            .Add(p => p.Question, lsQuestion)
            .Add(p => p.SessionId, sessionId));
        Assert.Contains("likert-scale-options", lsCut.Markup);

        // OpenEnded
        var oeQuestion = new Question
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Text = "OE",
            Type = QuestionType.OpenEnded,
            Options = new List<string>(),
            SortOrder = 3
        };
        var oeCut = Render<QuestionDisplay>(parameters => parameters
            .Add(p => p.Question, oeQuestion)
            .Add(p => p.SessionId, sessionId));
        Assert.Contains("textarea", oeCut.Markup);
    }
}
