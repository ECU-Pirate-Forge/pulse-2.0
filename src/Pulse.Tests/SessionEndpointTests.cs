using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pulse.Common.Services;
using Pulse.Shared.Models;
using Pulse.WebApi;

namespace Pulse.Tests.Tests;

public class SessionEndpointTests
{
    // --- Fake in-memory repository ---

    private sealed class FakeSessionRepository : ISessionRepository
    {
        private readonly Dictionary<Guid, Session> _store = new();

        public Session Insert(Session session)
        {
            if (session.Id == Guid.Empty)
                session.Id = Guid.NewGuid();
            _store[session.Id] = session;
            return session;
        }

        public Session? GetById(Guid id) =>
            _store.TryGetValue(id, out var s) ? s : null;

        public Task<bool> JoinCodeExistsAsync(string joinCode, CancellationToken ct = default)
        {
            var exists = _store.Values.Any(s => string.Equals(s.JoinCode, joinCode, StringComparison.Ordinal));
            return Task.FromResult(exists);
        }
    }

    private HttpClient CreateClient(ISessionRepository? repo = null)
    {
        repo ??= new FakeSessionRepository();
        return new WebApplicationFactory<ApiAssemblyMarker>()
            .WithWebHostBuilder(b => b.ConfigureServices(services =>
            {
                services.AddSingleton(repo);
            }))
            .CreateClient();
    }

    // --- POST /api/sessions ---

    [Fact]
    public async Task Post_WithValidTitle_Returns201WithCodes()
    {
        var client = CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/sessions", new { Title = "Week 1 Review" }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateSessionResponse>(ct);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.False(string.IsNullOrWhiteSpace(body.JoinCode));
        Assert.False(string.IsNullOrWhiteSpace(body.InstructorCode));
    }

    [Fact]
    public async Task Post_WithEmptyTitle_Returns400()
    {
        var client = CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/sessions", new { Title = "" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithWhitespaceTitle_Returns400()
    {
        var client = CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync("/api/sessions", new { Title = "   " }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- GET /api/sessions/{id} ---

    [Fact]
    public async Task Get_WithValidInstructorCode_Returns200WithSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new FakeSessionRepository();
        var session = repo.Insert(new Session
        {
            Title = "Test Session",
            InstructorCode = "SECRETCODE",
            JoinCode = "JOIN01",
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        var client = CreateClient(repo);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/sessions/{session.Id}");
        request.Headers.Add("InstructorCode", "SECRETCODE");
        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Session>(ct);
        Assert.NotNull(body);
        Assert.Equal(session.Id, body!.Id);
        Assert.Equal("Test Session", body.Title);
    }

    [Fact]
    public async Task Get_WithMissingInstructorCodeHeader_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new FakeSessionRepository();
        var session = repo.Insert(new Session { Title = "Test", InstructorCode = "CODE" });
        var client = CreateClient(repo);

        var response = await client.GetAsync($"/api/sessions/{session.Id}", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithWrongInstructorCode_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new FakeSessionRepository();
        var session = repo.Insert(new Session { Title = "Test", InstructorCode = "RIGHTCODE" });
        var client = CreateClient(repo);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/sessions/{session.Id}");
        request.Headers.Add("InstructorCode", "WRONGCODE");
        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithNonExistentId_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/sessions/{Guid.NewGuid()}");
        request.Headers.Add("InstructorCode", "ANYCODE");
        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Helper record matching the API response shape
    private record CreateSessionResponse(Guid Id, string JoinCode, string InstructorCode);
}
