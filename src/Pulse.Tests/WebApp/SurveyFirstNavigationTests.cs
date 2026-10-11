using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using System.Net.Http.Json;
using AdminHome = Pulse.Admin.Components.Pages.Home;
using JoinSurveyForm = Pulse.WebApp.Components.JoinSurveyForm;
using JoinSessionPage = Pulse.WebApp.Components.Pages.JoinSessionPage;

namespace Pulse.Tests.Tests.WebApp;

public class SurveyFirstNavigationTests : BunitContext
{
    public SurveyFirstNavigationTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void JoinForm_UsesAccessibleSurveyLanguage()
    {
        var cut = Render<JoinSurveyForm>();

        Assert.NotNull(cut.Find("label[for='join-code']"));
        Assert.Contains("anonymous survey", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("quiz", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JoinForm_NormalizesCodeAndUsesCanonicalJoinRoute()
    {
        var cut = Render<JoinSurveyForm>();

        cut.Find("#join-code").Input("abc123");
        cut.Find("form").Submit();

        Assert.EndsWith("/join/ABC123", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void AdminHome_PresentsSurveyFirstWorkspaces()
    {
        var cut = Render<AdminHome>();
        var text = cut.Markup;

        Assert.Contains("Surveys", text);
        Assert.Contains("Trivia", text);
        Assert.Contains("Question Bank", text);
        Assert.True(text.IndexOf("Surveys", StringComparison.Ordinal) <
                    text.IndexOf("Trivia", StringComparison.Ordinal));
        Assert.DoesNotContain("Hello MudBlazor", text);
    }

    [Fact]
    public void ValidJoinCode_ContinuesDirectlyToSurvey()
    {
        Services.AddSingleton(new HttpClient(new SuccessfulJoinHandler())
        {
            BaseAddress = new Uri("http://localhost")
        });

        var cut = Render<JoinSessionPage>(parameters => parameters
            .Add(p => p.JoinCode, "ABC123"));

        cut.WaitForAssertion(() =>
            Assert.EndsWith("/quiz/ABC123", Services.GetRequiredService<NavigationManager>().Uri));
    }

    [Fact]
    public void InactiveJoinCode_ShowsNotAcceptingResponsesMessage()
    {
        Services.AddSingleton(new HttpClient(new InactiveJoinHandler())
        {
            BaseAddress = new Uri("http://localhost")
        });

        var cut = Render<JoinSessionPage>(parameters => parameters
            .Add(p => p.JoinCode, "ABC123"));

        cut.WaitForAssertion(() =>
            Assert.Contains("not currently accepting responses", cut.Markup, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TriviaCode_ContinuesToTriviaWithNormalizedCode()
    {
        var handler = new JoinLookupHandler(HttpStatusCode.NotFound, HttpStatusCode.OK);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<JoinSessionPage>(parameters => parameters.Add(p => p.JoinCode, " abc123 "));

        cut.WaitForAssertion(() =>
            Assert.EndsWith("/trivia/ABC123", Services.GetRequiredService<NavigationManager>().Uri));
        Assert.Equal(["/api/sessions/join/ABC123", "/api/trivia/join/ABC123"], handler.Paths);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public void ExistingSurveyOrServerError_DoesNotFallBackToTrivia(HttpStatusCode surveyStatus)
    {
        var handler = new JoinLookupHandler(surveyStatus, HttpStatusCode.OK);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<JoinSessionPage>(parameters => parameters.Add(p => p.JoinCode, "ABC123"));

        cut.WaitForAssertion(() => Assert.DoesNotContain("Confirming", cut.Markup));
        Assert.Equal(["/api/sessions/join/ABC123"], handler.Paths);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "That survey or trivia code was not found")]
    [InlineData(HttpStatusCode.InternalServerError, "could not check")]
    public void UnknownCodeOrTriviaServerError_ShowsAppropriateError(
        HttpStatusCode triviaStatus, string expectedMessage)
    {
        var handler = new JoinLookupHandler(HttpStatusCode.NotFound, triviaStatus);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<JoinSessionPage>(parameters => parameters.Add(p => p.JoinCode, "ABC123"));

        cut.WaitForAssertion(() => Assert.Contains(expectedMessage, cut.Markup));
        Assert.Equal(["/api/sessions/join/ABC123", "/api/trivia/join/ABC123"], handler.Paths);
    }

    private sealed class JoinLookupHandler(HttpStatusCode surveyStatus, HttpStatusCode triviaStatus)
        : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            return Task.FromResult(new HttpResponseMessage(
                path.StartsWith("/api/sessions/", StringComparison.Ordinal) ? surveyStatus : triviaStatus));
        }
    }

    private sealed class SuccessfulJoinHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id = Guid.NewGuid(),
                    title = "Week 1 feedback",
                    joinCode = "ABC123"
                })
            });
    }

    private sealed class InactiveJoinHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent.Create(new
                {
                    error = "This survey is not accepting responses."
                })
            });
    }
}
