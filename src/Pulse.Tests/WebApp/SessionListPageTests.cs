using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Bunit;
using Pulse.Shared.Models;
using Pulse.WebApp.Components.Pages;
using Xunit;

namespace Pulse.Tests.WebApp;

#pragma warning disable CS0618
public class SessionListPageTests : Bunit.TestContext
{
    private const string InstructorCode = "INST-123";

    [Fact]
    public void OnInitialized_LoadsSessions_AndRendersRows()
    {
        var sessionId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            new()
            {
                Id = sessionId,
                Title = "Physics Quiz",
                Status = "Draft",
                JoinCode = "ABC123",
                InstructorCode = InstructorCode
            }
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/sessions")
            {
                return JsonResponse(HttpStatusCode.OK, sessions);
            }

            return JsonResponse(HttpStatusCode.NotFound, new { error = "Not found" });
        });

        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<SessionListPage>(parameters =>
            parameters.Add(p => p.InstructorCode, InstructorCode));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Physics Quiz", cut.Markup);
            Assert.Contains("Draft", cut.Markup);
        });

        var getRequest = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Get);
        Assert.Equal("/api/sessions", getRequest.Path);
        Assert.Equal(InstructorCode, getRequest.InstructorCode);
    }

    [Theory]
    [InlineData("Draft", "open", "POST")]
    [InlineData("Open", "activate", "POST")]
    [InlineData("Active", "close", "POST")]
    [InlineData("Draft", "delete", "DELETE")]
    public void ActionButton_CallsEndpoint_AndRefreshesList(string status, string action, string expectedMethod)
    {
        var sessionId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            new()
            {
                Id = sessionId,
                Title = "Session A",
                Status = status,
                JoinCode = "JOINME",
                InstructorCode = InstructorCode
            }
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/sessions")
            {
                return JsonResponse(HttpStatusCode.OK, sessions);
            }

            var expectedPath = action == "delete"
                ? $"/api/sessions/{sessionId}"
                : $"/api/sessions/{sessionId}/{action}";

            if (request.Method.Method == expectedMethod && request.RequestUri?.AbsolutePath == expectedPath)
            {
                return JsonResponse(HttpStatusCode.OK, new { ok = true });
            }

            return JsonResponse(HttpStatusCode.NotFound, new { error = "Not found" });
        });

        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<SessionListPage>(parameters =>
            parameters.Add(p => p.InstructorCode, InstructorCode));

        cut.WaitForAssertion(() => Assert.Contains("Session A", cut.Markup));

        var selector = $"button[data-testid='{action}-{sessionId}']";
        cut.Find(selector).Click();

        cut.WaitForAssertion(() =>
        {
            var actionPath = action == "delete"
                ? $"/api/sessions/{sessionId}"
                : $"/api/sessions/{sessionId}/{action}";

            Assert.Contains(handler.Requests, r =>
                r.Method.Method == expectedMethod &&
                r.Path == actionPath &&
                r.InstructorCode == InstructorCode);

            var getCount = handler.Requests.Count(r => r.Method == HttpMethod.Get && r.Path == "/api/sessions");
            Assert.True(getCount >= 2, "Expected list refresh GET after action success.");
        });
    }

    [Fact]
    public void FailedAction_ShowsFriendlyErrorMessage()
    {
        var sessionId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            new()
            {
                Id = sessionId,
                Title = "Session B",
                Status = "Draft",
                JoinCode = "JOINB",
                InstructorCode = InstructorCode
            }
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/sessions")
            {
                return JsonResponse(HttpStatusCode.OK, sessions);
            }

            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == $"/api/sessions/{sessionId}/open")
            {
                return JsonResponse(HttpStatusCode.InternalServerError, new { error = "server error" });
            }

            return JsonResponse(HttpStatusCode.NotFound, new { error = "Not found" });
        });

        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<SessionListPage>(parameters =>
            parameters.Add(p => p.InstructorCode, InstructorCode));

        cut.WaitForAssertion(() => Assert.Contains("Session B", cut.Markup));

        cut.Find($"button[data-testid='open-{sessionId}']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("We could not open this session. Please try again.", cut.Markup));
    }

    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode code, T body)
    {
        return new HttpResponseMessage(code)
        {
            Content = JsonContent.Create(body)
        };
    }

    private sealed record CapturedRequest(HttpMethod Method, string Path, string? InstructorCode);

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryGetValues("InstructorCode", out var values);
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri?.AbsolutePath ?? string.Empty,
                values?.FirstOrDefault()));

            return Task.FromResult(responder(request));
        }
    }
}
