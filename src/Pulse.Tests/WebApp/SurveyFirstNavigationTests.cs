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

    [Fact]
    public void UnknownCode_ChecksFormsThenShowsNotFound()
    {
        var handler = new JoinLookupHandler(HttpStatusCode.NotFound, HttpStatusCode.NotFound);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<JoinSessionPage>(parameters => parameters.Add(p => p.JoinCode, "ABC123"));

        cut.WaitForAssertion(() => Assert.Contains("That survey, trivia, or form code was not found", cut.Markup));
        Assert.Equal(["/api/sessions/join/ABC123", "/api/trivia/join/ABC123", "/api/forms/join/ABC123"], handler.Paths);
    }

    [Fact]
    public void TriviaServerError_ShowsAppropriateError()
    {
        var handler = new JoinLookupHandler(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<JoinSessionPage>(parameters => parameters.Add(p => p.JoinCode, "ABC123"));

        cut.WaitForAssertion(() => Assert.Contains("could not check", cut.Markup));
        Assert.Equal(["/api/sessions/join/ABC123", "/api/trivia/join/ABC123"], handler.Paths);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Conflict)]
    public void FormCode_ContinuesToFormPage(HttpStatusCode formStatus)
    {
        var handler = new JoinLookupHandler(HttpStatusCode.NotFound, HttpStatusCode.NotFound, formStatus);
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<JoinSessionPage>(parameters => parameters.Add(p => p.JoinCode, " frm123 "));

        cut.WaitForAssertion(() =>
            Assert.EndsWith("/form/FRM123", Services.GetRequiredService<NavigationManager>().Uri));
    }

    private sealed class JoinLookupHandler(
        HttpStatusCode surveyStatus,
        HttpStatusCode triviaStatus,
        HttpStatusCode formStatus = HttpStatusCode.NotFound)
        : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            var status = path.StartsWith("/api/sessions/", StringComparison.Ordinal) ? surveyStatus
                : path.StartsWith("/api/trivia/", StringComparison.Ordinal) ? triviaStatus
                : formStatus;
            return Task.FromResult(new HttpResponseMessage(status));
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
