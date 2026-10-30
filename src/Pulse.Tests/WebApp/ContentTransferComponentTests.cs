using System.Net.Http.Json;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Pulse.Admin.Components;
using Pulse.Shared.Contracts.V1;

namespace Pulse.Tests.Tests.WebApp;

public sealed class ContentTransferComponentTests : BunitContext
{
    private TransferHandler Prepare(HttpStatusCode status = HttpStatusCode.OK)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var handler = new TransferHandler(status);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        return handler;
    }

    [Fact]
    public void Import_UploadsJsonWithInstructorHeaderAndRefreshesParent()
    {
        var handler = Prepare();
        var refreshed = false;
        var cut = Render<ContentTransfer>(p => p
            .Add(x => x.InstructorCode, "INST")
            .Add(x => x.ImportUrl, "/api/content/import/questions")
            .Add(x => x.Imported, () => refreshed = true));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("""{"format":"pulse"}""", "questions.json"));

        cut.WaitForAssertion(() =>
        {
            Assert.True(refreshed);
            Assert.Equal("INST", handler.Code);
            Assert.Equal("""{"format":"pulse"}""", handler.Body);
            Assert.Equal("/api/content/import/questions", handler.Path);
            Assert.Equal("questions.json", handler.FileName);
            Assert.Contains("Imported 2 item(s)", cut.Find("[role='status']").TextContent);
        });
    }

    [Fact]
    public void Import_ShowsServerErrorWithoutReportingSuccess()
    {
        Prepare(HttpStatusCode.BadRequest);
        var cut = Render<ContentTransfer>(p => p
            .Add(x => x.InstructorCode, "INST")
            .Add(x => x.ImportUrl, "/api/content/import/questions"));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("invalid", "bad.json"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Invalid file", cut.Find("[role='alert']").TextContent);
            Assert.Empty(cut.FindAll("[role='status']"));
        });
    }

    [Fact]
    public void Export_DownloadsJsonUsingServerFilenameAndInstructorHeader()
    {
        var handler = Prepare();
        var cut = Render<ContentTransfer>(p => p
            .Add(x => x.InstructorCode, "INST")
            .Add(x => x.ExportUrl, "/api/content/questions"));

        cut.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            var call = Assert.Single(JSInterop.Invocations["pulseDownload.saveBase64"]);
            Assert.Equal("questions.json", call.Arguments[0]);
            Assert.Equal("application/json", call.Arguments[1]);
            Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("{}")), call.Arguments[2]);
            Assert.Equal("INST", handler.Code);
        });
    }

    [Fact]
    public void Import_OversizedFileIsRejectedBeforeSending()
    {
        var handler = Prepare();
        var cut = Render<ContentTransfer>(p => p
            .Add(x => x.InstructorCode, "INST")
            .Add(x => x.ImportUrl, "/api/content/import/questions"));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(
            new string('x', ContentTransferJson.MaxFileBytes + 1), "large.json"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("2 MiB", cut.Find("[role='alert']").TextContent);
            Assert.Null(handler.Path);
        });
    }

    private sealed class TransferHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public string? Code { get; private set; }
        public string? Path { get; private set; }
        public string? Body { get; private set; }
        public string? FileName { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Code = request.Headers.GetValues("InstructorCode").Single();
            Path = request.RequestUri!.AbsolutePath;
            FileName = request.Headers.TryGetValues("X-Import-Filename", out var names) ? names.Single() : null;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var response = new HttpResponseMessage(status);
            if (status != HttpStatusCode.OK)
                response.Content = new StringContent("Invalid file");
            else if (request.Method == HttpMethod.Post)
                response.Content = JsonContent.Create(new ContentImportResponse("questions", null, 2));
            else
            {
                response.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                response.Content.Headers.ContentDisposition = new("attachment") { FileNameStar = "questions.json" };
            }
            return response;
        }
    }
}
