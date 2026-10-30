using LiteDB;
using Pulse.Common.Services;
using Pulse.Shared.Models;

namespace Pulse.WebApi;

public static class QuestionBankManagementEndpoints
{
    public static void MapQuestionBankManagement(this WebApplication app)
    {
        app.MapGet("/api/questionbank/imports", GetImports);
        app.MapPost("/api/questionbank/delete/preview", PreviewDelete);
        app.MapPost("/api/questionbank/delete", Delete);
    }

    public static IResult GetImports(LiteDatabase database, IQuestionBankRepository bank)
    {
        var counts = bank.GetAll().Where(item => item.ImportBatchId.HasValue)
            .GroupBy(item => item.ImportBatchId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var imports = database.GetCollection<QuestionImportBatch>("question_imports").FindAll()
            .OrderByDescending(batch => batch.ImportedAt.ToUniversalTime())
            .Select(batch => new QuestionImportSummary(
                batch.Id, batch.FileName, batch.ImportedAt.ToUniversalTime(), batch.ImportedCount,
                counts.GetValueOrDefault(batch.Id))).ToList();
        return Results.Ok(imports);
    }

    public static IResult PreviewDelete(QuestionBankQuery filter, IQuestionBankRepository bank)
    {
        var error = ValidateDeleteFilter(filter);
        return error is not null
            ? Results.BadRequest(error)
            : Results.Ok(new QuestionBankDeletePreview(bank.Search(filter).Select(item => item.Id).ToList()));
    }

    public static IResult Delete(
        DeleteQuestionBankRequest request, LiteDatabase database, IQuestionBankRepository bank)
    {
        var error = ValidateDeleteFilter(request.Filter);
        if (error is not null)
            return Results.BadRequest(error);
        if (request.ExpectedIds is not { Count: > 0 } ||
            request.ExpectedIds.Contains(Guid.Empty) ||
            request.ExpectedIds.Distinct().Count() != request.ExpectedIds.Count)
            return Results.BadRequest("Confirm a non-empty preview of distinct question IDs before deleting.");

        if (!database.BeginTrans())
            throw new InvalidOperationException("Unable to start question deletion transaction.");
        try
        {
            var matches = bank.Search(request.Filter!).Select(item => item.Id).ToHashSet();
            if (!matches.SetEquals(request.ExpectedIds))
            {
                database.Rollback();
                return Results.Conflict("Matching questions changed. Preview the deletion again.");
            }
            foreach (var id in matches)
            {
                if (!bank.Delete(id))
                    throw new InvalidOperationException($"Unable to delete question {id}.");
            }
            if (!database.Commit())
                throw new InvalidOperationException("Unable to commit question deletion transaction.");
            return Results.Ok(new QuestionBankDeleteResponse(matches.Count));
        }
        catch
        {
            database.Rollback();
            throw;
        }
    }

    private static string? ValidateDeleteFilter(QuestionBankQuery? filter) =>
        filter is null ? "A question filter is required." :
        filter.Validate() ?? (!filter.HasFilters ? "Apply at least one filter before deleting questions." : null);
}
