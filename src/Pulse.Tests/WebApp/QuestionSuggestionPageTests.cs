using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Pulse.WebApp.Components.Pages;

namespace Pulse.Tests.Tests.WebApp;

public class QuestionSuggestionPageTests : BunitContext
{
    [Fact]
    public void SubmittedSuggestion_ShowsPendingConfirmation()
    {
        var handler = new SuggestionHandler();
        Services.AddMudServices();
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<SuggestQuestion>();
        cut.Find("[data-testid='suggestion-text']").Change("The network is secure.");
        cut.Find("select").Change("TrueFalse");
        cut.Find("[data-testid='submit-suggestion']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Suggestion received", cut.Markup);
            Assert.Contains("pending instructor review", cut.Markup);
        });
        Assert.NotNull(handler.Payload);
        Assert.Equal("The network is secure.", handler.Payload!.Text);
    }

    private sealed class SuggestionHandler : HttpMessageHandler
    {
        public SuggestionPayload? Payload { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Payload = await request.Content!.ReadFromJsonAsync<SuggestionPayload>(
                cancellationToken: cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = JsonContent.Create(new { id = Guid.NewGuid() })
            };
        }
    }

    private sealed record SuggestionPayload(string Text);
}
