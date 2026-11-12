using System.IO.Compression;
using System.Text;
using LiteDB;
using Pulse.Common.Services;
using Pulse.Shared.Models;
using Pulse.Shared.Services;
using Pulse.WebApi.Features.SurveyGeneration;
using UglyToad.PdfPig.Writer;

namespace Pulse.Tests;

public class SurveyGroundingTests
{
    [Theory]
    [InlineData("notes.txt")]
    [InlineData("notes.md")]
    public void Extract_ReadsUtf8Documents(string name)
    {
        Assert.Equal("Grounding text", new DocumentTextExtractor().Extract(name, Encoding.UTF8.GetBytes("Grounding text")));
    }

    [Fact]
    public void Extract_ReadsDocxParagraphs()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        using (var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
            writer.Write("""
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                <w:body><w:p><w:r><w:t>First paragraph.</w:t></w:r></w:p>
                <w:p><w:r><w:t>Second paragraph.</w:t></w:r></w:p></w:body></w:document>
                """);
        Assert.Equal("First paragraph.\nSecond paragraph.", new DocumentTextExtractor().Extract("notes.docx", buffer.ToArray()));
    }

    [Fact]
    public void Extract_ReadsTextPdf()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4)
            .AddText("Photosynthesis uses light.", 12, new UglyToad.PdfPig.Core.PdfPoint(20, 700), font);
        var text = new DocumentTextExtractor().Extract("notes.pdf", builder.Build());
        Assert.Contains("Photosynthesis uses light.", text);
    }

    [Theory]
    [InlineData("notes.exe", "text")]
    [InlineData("notes.txt", " ")]
    [InlineData("notes.pdf", "not a PDF")]
    [InlineData("notes.docx", "not a ZIP")]
    public void Extract_RejectsUnsupportedEmptyOrCorruptDocuments(string name, string text)
    {
        Assert.Throws<GroundingValidationException>(() => new DocumentTextExtractor().Extract(name, Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void Extract_RejectsOversizedFileAndExtractedText()
    {
        var extractor = new DocumentTextExtractor();
        Assert.Throws<GroundingValidationException>(() => extractor.Extract("notes.txt", new byte[DocumentTextExtractor.MaximumFileBytes + 1]));
        Assert.Throws<GroundingValidationException>(() => extractor.Extract("notes.md",
            Encoding.UTF8.GetBytes(new string('x', DocumentTextExtractor.MaximumDocumentCharacters + 1))));
    }

    [Fact]
    public void Grounding_PersistsAndReloadsWithOriginalDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pulse-grounding-{Guid.NewGuid():N}.db");
        var sessionId = Guid.NewGuid();
        try
        {
            using (var database = new LiteDatabase(path))
            {
                new SurveyGenerationRepository(database).SaveGrounding(new SurveyGrounding
                {
                    Id = sessionId, AdditionalContext = "Instructor focus",
                    Documents = [new SurveyGroundingDocument { FileName = "notes.txt", Text = "Text", Content = [1, 2, 3] }]
                });
            }
            using var reopened = new LiteDatabase(path);
            var reloaded = new SurveyGenerationRepository(reopened).GetGrounding(sessionId);
            Assert.Equal("Instructor focus", reloaded.AdditionalContext);
            Assert.Equal([1, 2, 3], reloaded.Documents.Single().Content);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Grounding_ConcurrentStaleSaveDoesNotOverwriteNewerChanges()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var repository = new SurveyGenerationRepository(database);
        var material = repository.SaveGrounding(OllamaSurveyGeneratorTests.Material());
        var stale = repository.GetGrounding(material.Id);
        material.AdditionalContext = "Newer focus";
        repository.SaveGrounding(material);
        stale.AdditionalContext = "Older request";
        Assert.Throws<GroundingConflictException>(() => repository.SaveGrounding(stale));
        Assert.Equal("Newer focus", repository.GetGrounding(material.Id).AdditionalContext);
    }

    [Fact]
    public void SavePreview_StaleGenerationDoesNotReplaceCurrentPreview()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var repository = new SurveyGenerationRepository(database);
        var material = repository.SaveGrounding(OllamaSurveyGeneratorTests.Material());
        var originalRevision = material.Revision;
        material.AdditionalContext = "Updated focus";
        repository.SaveGrounding(material);
        var current = repository.SavePreview(material.Id, material.Revision, []);

        Assert.Throws<GroundingConflictException>(() =>
            repository.SavePreview(material.Id, originalRevision, []));
        Assert.NotNull(database.GetCollection<SurveyGenerationPreview>("survey_generation_previews").FindById(current.Id));
    }

    [Fact]
    public void Accept_RejectsPreviewAfterGroundingChanges()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var material = OllamaSurveyGeneratorTests.Material();
        var repository = new SurveyGenerationRepository(database);
        repository.SaveGrounding(material);
        var preview = repository.SavePreview(material.Id, material.Revision, []);
        material.AdditionalContext = "Changed focus";
        repository.SaveGrounding(material);
        Assert.Throws<GroundingValidationException>(() => repository.Accept(material.Id, preview.Id));
    }

    [Fact]
    public async Task DeleteSurvey_RemovesAttachedMaterialAndPreviews()
    {
        using var database = new LiteDatabase("Filename=:memory:");
        var sessions = new SessionRepository(database, new JoinCodeGenerator());
        var session = sessions.Insert(new Session { Title = "Delete grounding" });
        var repository = new SurveyGenerationRepository(database);
        var material = OllamaSurveyGeneratorTests.Material();
        material.Id = session.Id;
        repository.SaveGrounding(material);
        repository.SavePreview(session.Id, material.Revision, []);
        Assert.True(await sessions.DeleteAsync(session.Id));
        Assert.Equal(0, database.GetCollection("survey_grounding").Count());
        Assert.Equal(0, database.GetCollection("survey_generation_previews").Count());
    }
}
