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

    [Fact]
    public async Task Drop_Reorders_Questions_And_Emits_OnOrderChanged()
    {
        // Arrange
        var q1Id = Guid.NewGuid();
        var q2Id = Guid.NewGuid();
        var q3Id = Guid.NewGuid();
        var questions = new List<Question>
        {
            new() { Id = q1Id, Text = "First", Type = QuestionType.MultipleChoice, SortOrder = 0 },
            new() { Id = q2Id, Text = "Second", Type = QuestionType.LikertScale, SortOrder = 1 },
            new() { Id = q3Id, Text = "Third", Type = QuestionType.OpenEnded, SortOrder = 2 }
        };

        List<Question>? receivedOrder = null;

        var component = Render<DraggableQuestionList>(parameters => parameters
            .Add(p => p.Questions, questions)
            .Add(p => p.OnOrderChanged, EventCallback.Factory.Create<List<Question>>(
                this, order => receivedOrder = order)));

        var items = component.FindAll(".question-item");

        // Act: drag item at index 0 (First) and drop onto index 2 (Third)
        await items[0].TriggerEventAsync("ondragstart", new DragEventArgs());
        // Re-find items after dragstart triggers a re-render
        items = component.FindAll(".question-item");
        await items[2].TriggerEventAsync("ondrop", new DragEventArgs());

        // Assert: callback was invoked and new order has "First" moved after "Second"
        // OnDrop: drag from index 0 to index 2, with adjustment for removal → inserts at index 1
        // Original: [First(0), Second(1), Third(2)]
        // After RemoveAt(0): [Second(0), Third(1)]
        // targetIndex adjusts from 2→1 because removing item at lower index shifts remaining indices down
        // After Insert(1): [Second, First, Third]
        Assert.NotNull(receivedOrder);
        Assert.Equal(3, receivedOrder!.Count);
        Assert.Equal(q2Id, receivedOrder[0].Id);
        Assert.Equal(q1Id, receivedOrder[1].Id);
        Assert.Equal(q3Id, receivedOrder[2].Id);
        // SortOrder values are updated sequentially
        Assert.Equal(0, receivedOrder[0].SortOrder);
        Assert.Equal(1, receivedOrder[1].SortOrder);
        Assert.Equal(2, receivedOrder[2].SortOrder);
    }
}
