using LiteDB;
using Pulse.Common.Services;
using Pulse.Domain.Entities;
using Pulse.Shared.Models;

namespace Pulse.Tests.Tests;

public class QuestionBankRepositoryTests
{
    private QuestionBankRepository CreateRepository()
    {
        // Isolate mapping metadata from other databases used by parallel tests.
        var db = new LiteDatabase("Filename=:memory:", new BsonMapper());
        return new QuestionBankRepository(db);
    }

    [Fact]
    public void Insert_AndGetById_ReturnsItem()
    {
        // Arrange
        var repo = CreateRepository();
        var item = new QuestionBankItem { Text = "What is 2+2?", Type = QuestionType.MultipleChoice, Options = ["4", "3"] };

        // Act
        var inserted = repo.Insert(item);
        var result = repo.GetById(inserted.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("What is 2+2?", result.Text);
        Assert.Equal(inserted.Id, result.Id);
    }

    [Fact]
    public void GetAll_ReturnsAllItems()
    {
        // Arrange
        var repo = CreateRepository();
        repo.Insert(new QuestionBankItem { Text = "Question A", Type = QuestionType.MultipleChoice });
        repo.Insert(new QuestionBankItem { Text = "Question B", Type = QuestionType.MultipleChoice });

        // Act
        var all = repo.GetAll().ToList();

        // Assert
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GetById_WhenNotFound_ReturnsNull()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var result = repo.GetById(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Update_ChangesText()
    {
        // Arrange
        var repo = CreateRepository();
        var item = repo.Insert(new QuestionBankItem { Text = "Original", Type = QuestionType.MultipleChoice });

        // Act
        item.Text = "Updated";
        var success = repo.Update(item);
        var result = repo.GetById(item.Id);

        // Assert
        Assert.True(success);
        Assert.Equal("Updated", result?.Text);
    }

    [Fact]
    public void Delete_RemovesItem()
    {
        // Arrange
        var repo = CreateRepository();
        var item = repo.Insert(new QuestionBankItem { Text = "To Delete", Type = QuestionType.MultipleChoice });

        // Act
        var deleted = repo.Delete(item.Id);
        var result = repo.GetById(item.Id);

        // Assert
        Assert.True(deleted);
        Assert.Null(result);
    }

    [Fact]
    public void Search_ByText_ReturnsMatchingItems()
    {
        // Arrange
        var repo = CreateRepository();
        repo.Insert(new QuestionBankItem { Text = "What is photosynthesis?", Type = QuestionType.MultipleChoice });
        repo.Insert(new QuestionBankItem { Text = "What is gravity?", Type = QuestionType.MultipleChoice });
        repo.Insert(new QuestionBankItem { Text = "Who wrote Hamlet?", Type = QuestionType.MultipleChoice });

        // Act
        var results = repo.Search(text: "What is").ToList();

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Search_AnswerPositionSupportsTrueFalseMultipleAnswerAndMissingPositions()
    {
        var repo = CreateRepository();
        var tf = repo.Insert(new QuestionBankItem { Text = "True/false", Type = QuestionType.TrueFalse, CorrectAnswers = ["False"] });
        var multiple = repo.Insert(new QuestionBankItem
        {
            Text = "Multiple answers", Type = QuestionType.MultipleAnswer, Options = ["A", "B", "C"], CorrectAnswers = ["A", "C"]
        });
        repo.Insert(new QuestionBankItem { Text = "No answers", Type = QuestionType.MultipleChoice, Options = ["A", "B"] });

        Assert.Equal(tf.Id, Assert.Single(repo.Search(new QuestionBankQuery(CorrectAnswerPosition: 2))).Id);
        Assert.Equal(multiple.Id, Assert.Single(repo.Search(new QuestionBankQuery(CorrectAnswerPosition: 3))).Id);
        Assert.Empty(repo.Search(new QuestionBankQuery(CorrectAnswerPosition: 4)));
        Assert.Equal(tf.Id, Assert.Single(repo.Search(new QuestionBankQuery(Option: "FALSE"))).Id);
    }

    [Fact]
    public void ImportMetadata_PersistsWhenDatabaseReopens()
    {
        using var stream = new MemoryStream();
        var batch = new QuestionImportBatch { FileName = "questions.json", ImportedCount = 1 };
        Guid itemId;
        byte[] persisted;
        using (var db = new LiteDatabase(stream, new BsonMapper()))
        {
            db.GetCollection<QuestionImportBatch>("question_imports").Insert(batch);
            itemId = new QuestionBankRepository(db).Insert(new QuestionBankItem { Text = "Imported", ImportBatchId = batch.Id }).Id;
            db.Checkpoint();
            persisted = stream.ToArray();
        }
        using var reopened = new LiteDatabase(new MemoryStream(persisted), new BsonMapper());
        Assert.Equal(batch.Id, new QuestionBankRepository(reopened).GetById(itemId)!.ImportBatchId);
        Assert.Equal("questions.json", reopened.GetCollection<QuestionImportBatch>("question_imports").FindById(batch.Id).FileName);
    }

    [Fact]
    public void Search_ByType_ReturnsMatchingItems()
    {
        // Arrange
        var repo = CreateRepository();
        repo.Insert(new QuestionBankItem { Text = "MC Question", Type = QuestionType.MultipleChoice });
        repo.Insert(new QuestionBankItem { Text = "TF Question", Type = QuestionType.LikertScale });

        // Act
        var results = repo.Search(type: (int)QuestionType.MultipleChoice).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal("MC Question", results[0].Text);
    }
}
