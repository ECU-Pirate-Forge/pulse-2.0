using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Pulse.Shared.Models;
using Pulse.WebApp.Components.Pages;

namespace Pulse.Tests.WebApp;

public class InstructorDashboardTests : BunitContext
{
    private const string InstructorCode = "INST-123";

    public InstructorDashboardTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new ProtectedSessionStorage(
            JSInterop.JSRuntime,
            new EphemeralDataProtectionProvider()));
    }

    [Fact]
    public void DeleteButton_DeletesPersistedSessionBeforeRemovingCard()
    {
        var session = CreateSession();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/sessions")
                return JsonResponse(HttpStatusCode.OK, new[] { session });

            if (request.Method == HttpMethod.Get &&
                request.RequestUri?.AbsolutePath == $"/api/sessions/{session.Id}/questions")
                return JsonResponse(HttpStatusCode.OK, Array.Empty<object>());

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri?.AbsolutePath == $"/api/sessions/{session.Id}")
                return new HttpResponseMessage(HttpStatusCode.NoContent);

            return JsonResponse(HttpStatusCode.NotFound, new { error = "Not found" });
        });
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<InstructorDashboard>();
        Authenticate(cut);
        cut.WaitForAssertion(() => Assert.Contains(session.Title, cut.Markup));

        FindButton(cut, "Delete").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(handler.Requests, request =>
                request.Method == HttpMethod.Delete &&
                request.Path == $"/api/sessions/{session.Id}" &&
                request.InstructorCode == InstructorCode);
            Assert.DoesNotContain(session.Title, cut.Markup);
        });
    }

    [Fact]
    public void DeleteButton_WhenRequestFails_KeepsCardAndShowsError()
    {
        var session = CreateSession();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/sessions")
                return JsonResponse(HttpStatusCode.OK, new[] { session });

            if (request.Method == HttpMethod.Get &&
                request.RequestUri?.AbsolutePath == $"/api/sessions/{session.Id}/questions")
                return JsonResponse(HttpStatusCode.OK, Array.Empty<object>());

            if (request.Method == HttpMethod.Delete &&
                request.RequestUri?.AbsolutePath == $"/api/sessions/{session.Id}")
                return JsonResponse(HttpStatusCode.InternalServerError, new { error = "Server error" });

            return JsonResponse(HttpStatusCode.NotFound, new { error = "Not found" });
        });
        Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var cut = Render<InstructorDashboard>();
        Authenticate(cut);
        cut.WaitForAssertion(() => Assert.Contains(session.Title, cut.Markup));

        FindButton(cut, "Delete").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(session.Title, cut.Markup);
            Assert.Contains("Failed to delete session. Please try again.", cut.Markup);
        });
    }

    private static Session CreateSession() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Persistent Survey",
        InstructorCode = InstructorCode,
        JoinCode = "ABC123"
    };

    private static void Authenticate(IRenderedComponent<InstructorDashboard> cut)
    {
        cut.Find("input[type='password']").Change(InstructorCode);
        FindButton(cut, "Access Dashboard").Click();
    }

    private static AngleSharp.Dom.IElement FindButton(
        IRenderedComponent<InstructorDashboard> cut,
        string text) =>
        cut.FindAll("button").Single(button => button.TextContent.Contains(text, StringComparison.Ordinal));

    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode code, T body) =>
        new(code) { Content = JsonContent.Create(body) };

    private sealed record CapturedRequest(HttpMethod Method, string Path, string? InstructorCode);

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
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
