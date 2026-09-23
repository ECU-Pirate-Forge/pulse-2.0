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
}
