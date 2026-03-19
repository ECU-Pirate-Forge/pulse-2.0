using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Pulse.Shared.Models;
using Pulse.Shared.Repositories;
using Pulse.Shared.Services;

namespace Pulse.WebApi;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record CreateSessionRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
    public string Title { get; init; } = string.Empty;
}

public record CreateSessionResponse(
    Guid Id,
    string JoinCode,
    string InstructorCode
);

// ── Controller ────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly ISessionRepository _sessions;
    private readonly IJoinCodeGenerator _joinCodeGenerator;

    public SessionsController(
        ISessionRepository sessions,
        IJoinCodeGenerator joinCodeGenerator)
    {
        _sessions = sessions;
        _joinCodeGenerator = joinCodeGenerator;
    }

    /// <summary>
    /// POST /api/sessions — creates a new session in Draft status.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateSessionRequest request,
        CancellationToken ct)
    {
        // Generate a unique JoinCode (retry on collision — extremely rare)
        string joinCode;
        do
        {
            joinCode = _joinCodeGenerator.Generate();
        }
        while (await _sessions.JoinCodeExistsAsync(joinCode, ct));

        var instructorCode = Guid.NewGuid().ToString("N")[..12].ToUpper();

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            JoinCode = joinCode,
            InstructorCode = instructorCode,
            Status = SessionStatus.Draft,
            CreatedAt = DateTime.UtcNow,
        };

        await _sessions.InsertAsync(session, ct);

        var response = new CreateSessionResponse(
            session.Id,
            session.JoinCode,
            session.InstructorCode
        );

        return CreatedAtAction(nameof(CreateSession), new { id = session.Id }, response);
    }
}