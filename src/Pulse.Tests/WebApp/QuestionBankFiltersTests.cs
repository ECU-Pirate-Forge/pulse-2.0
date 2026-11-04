using Bunit;
using Pulse.Admin.Components;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;

namespace Pulse.Tests.Tests.WebApp;

public sealed class QuestionBankFiltersTests : BunitContext
{
    [Fact]
    public void Apply_EmitsCombinedAnswerImportAndTaxonomyFilters()
    {
        QuestionBankQuery? applied = null;
        var batch = new QuestionImportSummary(Guid.NewGuid(), "quiz.json", DateTime.UtcNow, 10, 8);
        var cut = Render<QuestionBankFilters>(parameters => parameters
            .Add(component => component.Imports, [batch])
            .Add(component => component.ValueChanged, value => applied = value));
        cut.FindAll("input")[0].Change("protocol");
        cut.FindAll("input")[1].Change("CSCI");
        cut.FindAll("input")[2].Change("Web");
        cut.FindAll("input")[3].Change("network");
        cut.FindAll("input")[4].Change("HTTP");
        cut.FindAll("input")[5].Change("SMTP");
        cut.FindAll("input")[6].Change("2");
        cut.FindAll("select")[0].Change(((int)QuestionType.MultipleChoice).ToString());
        cut.FindAll("select")[1].Change("JsonImport");
        cut.FindAll("select")[2].Change(batch.Id.ToString());
        cut.FindAll("select")[3].Change("Draft");
        cut.FindAll("select")[4].Change("false");
        cut.FindAll("button")[0].Click();

        Assert.NotNull(applied);
        Assert.Equal("protocol", applied.Text);
        Assert.Equal("CSCI", applied.Course);
        Assert.Equal("Web", applied.Topic);
        Assert.Equal("network", applied.Tag);
        Assert.Equal("HTTP", applied.CorrectAnswer);
        Assert.Equal("SMTP", applied.Option);
        Assert.Equal(2, applied.CorrectAnswerPosition);
        Assert.Equal((int)QuestionType.MultipleChoice, applied.Type);
        Assert.Equal(QuestionSource.JsonImport, applied.Source);
        Assert.Equal(batch.Id, applied.ImportBatchId);
        Assert.True(applied.IsDraft);
        Assert.Null(applied.ModerationStatus);
        Assert.False(applied.TriviaEligible);
    }

    [Fact]
    public void TriviaFilters_KeepEligibilityAndApprovalWhenAppliedOrCleared()
    {
        var applied = new List<QuestionBankQuery>();
        var cut = Render<QuestionBankFilters>(parameters => parameters
            .Add(component => component.TriviaOnly, true)
            .Add(component => component.ValueChanged, value => applied.Add(value)));
        Assert.Empty(cut.FindAll("[data-testid='bank-status-filter']"));
        cut.Find("input").Change("network");
        cut.FindAll("button")[0].Click();
        cut.FindAll("button")[1].Click();

        Assert.Equal(2, applied.Count);
        Assert.All(applied, query =>
        {
            Assert.Equal(QuestionModerationStatus.Approved, query.ModerationStatus);
            Assert.True(query.TriviaEligible);
            Assert.False(query.IsDraft);
        });
        Assert.Equal("network", applied[0].Text);
        Assert.Null(applied[1].Text);
    }
}
