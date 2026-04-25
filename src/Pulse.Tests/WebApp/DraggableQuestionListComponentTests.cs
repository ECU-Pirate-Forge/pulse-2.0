using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Pulse.Domain.Entities;
using Pulse.WebApp.Components;
using Xunit;

namespace Pulse.Tests.WebApp;

public class DraggableQuestionListComponentTests : BunitContext
{
    [Fact]
    public void Renders_Empty_State_When_No_Questions()
    {
        // Arrange
        var component = Render<DraggableQuestionList>(parameters => parameters
            .Add(p => p.Questions, new List<Question>()));

        // Act & Assert
        var emptyState = component.FindAll(".empty-state");
        Assert.NotEmpty(emptyState);
        Assert.Contains("No questions to display", component.Markup);
    }

    [Fact]
    public void Renders_Question_List_When_Questions_Provided()
    {
        // Arrange
        var questions = new List<Question>
        {
            new() { Id = Guid.NewGuid(), Text = "Question 1", Type = QuestionType.MultipleChoice, SortOrder = 0 },
            new() { Id = Guid.NewGuid(), Text = "Question 2", Type = QuestionType.LikertScale, SortOrder = 1 },
            new() { Id = Guid.NewGuid(), Text = "Question 3", Type = QuestionType.OpenEnded, SortOrder = 2 }
        };

        var component = Render<DraggableQuestionList>(parameters => parameters
            .Add(p => p.Questions, questions));

        // Act & Assert
        var questionItems = component.FindAll(".question-item");
        Assert.Equal(3, questionItems.Count);
    }

    [Fact]
    public void Displays_Question_Text()
    {
        // Arrange
        var testText = "How satisfied are you with this feature?";
        var questions = new List<Question>
        {
            new() { Id = Guid.NewGuid(), Text = testText, Type = QuestionType.MultipleChoice, SortOrder = 0 }
        };

        var component = Render<DraggableQuestionList>(parameters => parameters
            .Add(p => p.Questions, questions));

        // Act & Assert
        Assert.Contains(testText, component.Markup);
    }

    [Fact]
    public void Displays_Question_Type_Correctly()
    {
        // Arrange
        var questions = new List<Question>
        {
            new() { Id = Guid.NewGuid(), Text = "Q1", Type = QuestionType.MultipleChoice, SortOrder = 0 },
            new() { Id = Guid.NewGuid(), Text = "Q2", Type = QuestionType.LikertScale, SortOrder = 1 },
            new() { Id = Guid.NewGuid(), Text = "Q3", Type = QuestionType.OpenEnded, SortOrder = 2 }
        };

        var component = Render<DraggableQuestionList>(parameters => parameters
            .Add(p => p.Questions, questions));

        // Act & Assert
        var markup = component.Markup;
        Assert.Contains("Multiple Choice", markup);
        Assert.Contains("Likert Scale", markup);
        Assert.Contains("Open Ended", markup);
    }

    [Fact]
    public void Displays_Options_For_MultipleChoice_Questions()
    {
        // Arrange
        var options = new List<string> { "Option A", "Option B", "Option C" };
        var questions = new List<Question>
        {
            new() 
            { 
                Id = Guid.NewGuid(), 
                Text = "Choose one", 
                Type = QuestionType.MultipleChoice, 
                Options = options,
                SortOrder = 0 
            }
        };

        var component = Render<DraggableQuestionList>(parameters => parameters
            .Add(p => p.Questions, questions));

        // Act & Assert
        var markup = component.Markup;
        Assert.Contains("Option A", markup);
        Assert.Contains("Option B", markup);
        Assert.Contains("Option C", markup);
    }

    [Fact]
    public void Does_Not_Display_Options_For_OpenEnded_Questions()
    {
        // Arrange
        var questions = new List<Question>
        {
            new() 
            { 
                Id = Guid.NewGuid(), 
                Text = "What do you think?", 
                Type = QuestionType.OpenEnded,
                Options = new List<string>(),
                SortOrder = 0 
            }
        };

        var component = Render<DraggableQuestionList>(parameters => parameters
            .Add(p => p.Questions, questions));

        // Act & Assert
        var questionOptions = component.FindAll(".question-options");
        Assert.Empty(questionOptions);
    }

    [Fact]
    public void Renders_Drag_Handles()
    {
        // Arrange
        var questions = new List<Question>
        {
            new() { Id = Guid.NewGuid(), Text = "Question 1", Type = QuestionType.MultipleChoice, SortOrder = 0 }
        };

        var component = Render<DraggableQuestionList>(parameters => parameters
            .Add(p => p.Questions, questions));

        // Act & Assert
        var dragHandles = component.FindAll(".drag-handle");
        Assert.NotEmpty(dragHandles);
        Assert.Contains("⋮⋮", component.Markup);
    }

    [Fact]
    public void Questions_Are_Draggable()
    {
        // Arrange
        var questions = new List<Question>
        {
            new() { Id = Guid.NewGuid(), Text = "Question 1", Type = QuestionType.MultipleChoice, SortOrder = 0 },
            new() { Id = Guid.NewGuid(), Text = "Question 2", Type = QuestionType.MultipleChoice, SortOrder = 1 }
        };

        var component = Render<DraggableQuestionList>(parameters => parameters
            .Add(p => p.Questions, questions));

        // Act & Assert
        var questionItems = component.FindAll(".question-item");
        foreach (var item in questionItems)
        {
            Assert.True(item.HasAttribute("draggable"));
            Assert.Equal("true", item.GetAttribute("draggable"));
        }
    }

    [Fact]
    public void Question_Numbers_Display_Sequentially()
    {
        // Arrange
        var questions = new List<Question>
        {
            new() { Id = Guid.NewGuid(), Text = "Question 1", Type = QuestionType.MultipleChoice, SortOrder = 0 },
            new() { Id = Guid.NewGuid(), Text = "Question 2", Type = QuestionType.MultipleChoice, SortOrder = 1 },
            new() { Id = Guid.NewGuid(), Text = "Question 3", Type = QuestionType.MultipleChoice, SortOrder = 2 }
        };

        var component = Render<DraggableQuestionList>(parameters => parameters
            .Add(p => p.Questions, questions));

        // Act & Assert
        var markup = component.Markup;
        // Check for question numbers
        Assert.Contains(">1<", markup); // Question 1 number
        Assert.Contains(">2<", markup); // Question 2 number
        Assert.Contains(">3<", markup); // Question 3 number
    }
}
