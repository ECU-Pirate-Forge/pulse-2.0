using System.Text;
using Microsoft.AspNetCore.Http;
using Pulse.Application.Services;
using Pulse.Shared.Models;
using Pulse.WebApi;

namespace Pulse.Tests.Tests;

public class CsvPreviewEndpointTests
{
    private static HttpRequest MakeRequest(string? csvContent = null, string fileName = "test.csv")
    {
        var context = new DefaultHttpContext();
        if (csvContent is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(csvContent);
            var stream = new MemoryStream(bytes);
            var file = new FormFile(stream, 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/csv"
            };
            context.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), new FormFileCollection { file });
        }
        else
        {
            context.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());
        }
        return context.Request;
    }

    [Fact]
    public async Task PreviewImport_ValidCsv_Returns200WithParsedRows()
    {
        var csv = "text,type,options,matchingPairs\nWhat is 2+2?,MultipleChoice,3|4|5|6,\nThe sky is blue,TrueFalse,,";
        var result = await CsvPreviewEndpointHandlers.PreviewImport(MakeRequest(csv), new QuestionService());

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<CsvPreviewResponse>>(result);
        Assert.Equal(2, ok.Value!.Rows.Count);
        Assert.True(ok.Value.AllRowsValid);
        Assert.Equal("What is 2+2?", ok.Value.Rows[0].Text);
        Assert.Equal(4, ok.Value.Rows[0].Options.Count);
    }

    [Fact]
    public async Task PreviewImport_ExtendedSchema_ReturnsTaxonomyAndProvenance()
    {
        var csv = "Text,Type,Options,MatchingPairs,Course,Topic,Tags\n" +
                  "\"Choose one\",MultipleChoice,\"A|B\",,CSCI 3000,Networking,\"web|security\"";

        var result = await CsvPreviewEndpointHandlers.PreviewImport(MakeRequest(csv), new QuestionService());

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<CsvPreviewResponse>>(result);
        var row = Assert.Single(ok.Value!.Rows);
        Assert.Equal("CSCI 3000", row.Course);
        Assert.Equal("Networking", row.Topic);
        Assert.Equal(["web", "security"], row.Tags);
        Assert.Equal(QuestionSource.CsvImport, row.Source);
        Assert.Equal(QuestionModerationStatus.Approved, row.ModerationStatus);
    }

    [Fact]
    public async Task PreviewImport_MatchingPairs_ReturnsStructuredPairs()
    {
        var csv = "text,type,options,matchingPairs\nMatch terms,Matching,,HTTP=>Web protocol|HTML=>Markup language";
        var result = await CsvPreviewEndpointHandlers.PreviewImport(MakeRequest(csv), new QuestionService());

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<CsvPreviewResponse>>(result);
        Assert.True(ok.Value!.AllRowsValid);
        Assert.Equal(2, ok.Value.Rows[0].MatchingPairs.Count);
        Assert.Equal("HTTP", ok.Value.Rows[0].MatchingPairs[0].Prompt);
    }

    [Fact]
    public async Task PreviewImport_LegacyType_ReturnsValidationError()
    {
        var csv = "text,type,options\nLegacy prompt,1,";
        var result = await CsvPreviewEndpointHandlers.PreviewImport(MakeRequest(csv), new QuestionService());

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<CsvPreviewResponse>>(result);
        Assert.False(ok.Value!.AllRowsValid);
        Assert.Contains("not a supported question type", ok.Value.Rows[0].ValidationErrors[0]);
    }

    [Fact]
    public async Task PreviewImport_SomeInvalidRows_Returns200WithErrors()
    {
        var csv = "text,type,options\n,0,A|B\nValid question,0,A|B";
        var result = await CsvPreviewEndpointHandlers.PreviewImport(MakeRequest(csv), new QuestionService());

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<CsvPreviewResponse>>(result);
        Assert.False(ok.Value!.AllRowsValid);
        Assert.Equal(2, ok.Value.Rows.Count);
        var invalidRow = ok.Value.Rows.First(r => !r.IsValid);
        Assert.NotEmpty(invalidRow.ValidationErrors);
    }

    [Fact]
    public async Task PreviewImport_McWithOneOption_Returns200WithValidationError()
    {
        var csv = "text,type,options\nWhat is 2+2?,0,only one";
        var result = await CsvPreviewEndpointHandlers.PreviewImport(MakeRequest(csv), new QuestionService());

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<CsvPreviewResponse>>(result);
        Assert.False(ok.Value!.AllRowsValid);
        Assert.Contains(ok.Value.Rows, r => r.ValidationErrors.Any(e => e.Contains("2 options")));
    }

    [Fact]
    public async Task PreviewImport_NoFile_Returns400()
    {
        var result = await CsvPreviewEndpointHandlers.PreviewImport(MakeRequest(null), new QuestionService());
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
    }

    [Fact]
    public async Task PreviewImport_WrongFileType_Returns400()
    {
        var result = await CsvPreviewEndpointHandlers.PreviewImport(MakeRequest("some content", "test.txt"), new QuestionService());
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
    }

    [Fact]
    public async Task PreviewImport_EmptyFile_Returns400()
    {
        var result = await CsvPreviewEndpointHandlers.PreviewImport(MakeRequest(string.Empty), new QuestionService());
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
    }

    [Fact]
    public async Task PreviewImport_InvalidType_OnlyShowsTypeError_NotMcError()
    {
        var csv = "text,type,options\nSome question,invalid_type,only one";
        var result = await CsvPreviewEndpointHandlers.PreviewImport(MakeRequest(csv), new QuestionService());

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<CsvPreviewResponse>>(result);
        Assert.False(ok.Value!.AllRowsValid);
        var row = ok.Value.Rows[0];
        Assert.Single(row.ValidationErrors);
        Assert.Contains("not a supported question type", row.ValidationErrors[0]);
        Assert.DoesNotContain("2 options", row.ValidationErrors[0]);
    }
}
