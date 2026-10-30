using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Pulse.Admin.Components.Pages;
using Pulse.Shared.Contracts.V1;
using Pulse.Shared.Models;

namespace Pulse.Tests.Tests.WebApp;

public class FormMarkdownAdminTests : BunitContext
{
    private async Task<FormHandler> PrepareAsync()
    {
        Services.AddMudServices();
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = new ProtectedSessionStorage(JSInterop.JSRuntime, new EphemeralDataProtectionProvider());
        await storage.SetAsync("instructorCode", "INSTRUCTOR");
        var encrypted = Assert.IsType<string>(JSInterop.Invocations["sessionStorage.setItem"].Single().Arguments[1]);
        JSInterop.Setup<string>("sessionStorage.getItem", "instructorCode").SetResult(encrypted);
        Services.AddSingleton(storage);
        var handler = new FormHandler();
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        Render<MudBlazor.MudPopoverProvider>();
        return handler;
    }

    [Fact]
    public async Task Monitor_RendersDescriptionAndQuestionMarkdown_ButNotAnswers()
    {
        var handler = await PrepareAsync();

        var cut = Render<FormLive>(p => p.Add(x => x.Id, handler.Form.Id));

        Assert.Equal("Instructions", cut.WaitForElement(".pulse-markdown h2").TextContent);
        Assert.Equal("Question", cut.Find("th .pulse-markdown strong").TextContent);
        Assert.Contains("Detail", cut.Find("th .pulse-markdown li").TextContent);
        Assert.Contains("**Plain answer**", cut.Find("[data-testid='response-row']").TextContent);
        Assert.Empty(cut.FindAll("td strong"));
    }

    [Fact]
    public async Task Editor_OffersMultilineMarkdownInputs_AndSavesTheSource()
    {
        var handler = await PrepareAsync();

        var cut = Render<FormEditor>(p => p.Add(x => x.Id, handler.Form.Id));

        cut.WaitForElement("[data-testid='field-row']");
        var textareas = cut.FindAll("textarea");
        Assert.Equal(2, textareas.Count);
        Assert.Equal(handler.Form.Description, textareas[0].GetAttribute("value"));
        Assert.Equal(handler.Form.Fields[0].Label, textareas[1].GetAttribute("value"));
        Assert.Contains("Supports Markdown", cut.Markup);
        cut.Find("[data-testid='save-form']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(handler.Saved);
            Assert.Equal(handler.Form.Description, handler.Saved.Description);
            Assert.Equal(handler.Form.Fields[0].Label, Assert.Single(handler.Saved.Fields!).Label);
        });
    }

    private sealed class FormHandler : HttpMessageHandler
    {
        public Form Form { get; } = new()
        {
            Title = "Markdown form",
            Description = "## Instructions\n\nRead *carefully*.",
            JoinCode = "ABC123",
            Status = FormStatus.Open,
            Fields = [new FormField { Label = "**Question**\n\n- Detail", FieldType = FormFieldType.Text }]
        };
        public SaveFormRequest? Saved { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Put)
            {
                Saved = await request.Content!.ReadFromJsonAsync<SaveFormRequest>(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Form) };
            }

            var path = request.RequestUri!.AbsolutePath;
            if (path == $"/api/forms/{Form.Id}/submissions")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new[]
                    {
                        new FormSubmission
                        {
                            FormId = Form.Id,
                            Values = new() { [Form.Fields[0].Id.ToString()] = "**Plain answer**" }
                        }
                    })
                };
            }
            if (path == $"/api/forms/{Form.Id}/qr")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
            if (path == $"/api/forms/{Form.Id}")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Form) };
            throw new InvalidOperationException($"Unexpected request: {request.Method} {path}");
        }
    }
}
