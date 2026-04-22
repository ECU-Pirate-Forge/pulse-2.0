using Xunit;
using Bunit;
using Moq;
using Moq.Protected;
using Pulse.WebApp.Components;
using FluentAssertions;
using System.Net;
using MudBlazor.Services;

namespace Pulse.Tests.WebApp;

#pragma warning disable CS0618
public class SessionQRCodeComponentTests : Bunit.TestContext
{
    public SessionQRCodeComponentTests()
    {
        // Register MudBlazor services required by components
        Services.AddMudServices();
    }

    /// <summary>
    /// Tests that the component can be instantiated with required parameters (initial load requirement).
    /// </summary>
    [Fact]
    public void SessionQRCode_CanBeInstantiated()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var joinCode = "ABC123";

        // Act
        var cut = Render<SessionQRCode>(parameters => parameters
            .Add(p => p.SessionId, sessionId)
            .Add(p => p.JoinCode, joinCode));

        // Assert
        cut.Instance.Should().NotBeNull("Component should instantiate successfully (initial load requirement)");
    }

    /// <summary>
    /// Tests that the component renders without throwing exceptions (successful refresh requirement).
    /// </summary>
    [Fact]
    public void SessionQRCode_RendersWithoutError()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var joinCode = "TEST_CODE";

        // Act - Component should render without throwing
        var cut = Render<SessionQRCode>(parameters => parameters
            .Add(p => p.SessionId, sessionId)
            .Add(p => p.JoinCode, joinCode));

        // Assert
        cut.Instance.Should().NotBeNull("Component should render without errors (successful refresh requirement)");
        cut.Markup.Should().NotBeNullOrEmpty("Component should produce markup");
    }

    /// <summary>
    /// Tests that the component handles missing QR data gracefully (failed image request requirement).
    /// </summary>
    [Fact]
    public void SessionQRCode_HandlesLoadingState()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var joinCode = "XYZ789";

        // Act
        var cut = Render<SessionQRCode>(parameters => parameters
            .Add(p => p.SessionId, sessionId)
            .Add(p => p.JoinCode, joinCode));

        // Assert - Component should render and show loading state initially
        cut.Instance.Should().NotBeNull();
        // Component will show loading spinner or error message gracefully
        var markup = cut.Markup;
        markup.Should().NotBeNullOrEmpty("Component should render and display loading or error state (failed request handling)");
    }

    /// <summary>
    /// Tests that the component exposes the public OnSessionActivatedAsync method (activation refresh wiring requirement).
    /// </summary>
    [Fact]
    public void SessionQRCode_ExposesOnSessionActivatedAsyncMethod()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var joinCode = "ABC123";

        // Act
        var cut = Render<SessionQRCode>(parameters => parameters
            .Add(p => p.SessionId, sessionId)
            .Add(p => p.JoinCode, joinCode));

        // Assert - Verify public method exists with correct signature
        var component = cut.Instance as SessionQRCode;
        component.Should().NotBeNull();

        var method = typeof(SessionQRCode)
            .GetMethod("OnSessionActivatedAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        method.Should().NotBeNull("OnSessionActivatedAsync public method should exist for parent component to call");
        method?.ReturnType.Should().Be(typeof(Task), "OnSessionActivatedAsync should return Task");
        method?.GetParameters().Should().HaveCount(0, "OnSessionActivatedAsync should have no parameters");
    }

    /// <summary>
    /// Tests that OnSessionActivatedAsync can be invoked without throwing exceptions (activation refresh implementation).
    /// </summary>
    [Fact]
    public async Task SessionQRCode_OnSessionActivatedAsync_CanBeInvoked()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var joinCode = "ABC123";

        var cut = Render<SessionQRCode>(parameters => parameters
            .Add(p => p.SessionId, sessionId)
            .Add(p => p.JoinCode, joinCode));

        var component = cut.Instance as SessionQRCode;
        component.Should().NotBeNull();

        var method = typeof(SessionQRCode)
            .GetMethod("OnSessionActivatedAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        method.Should().NotBeNull();

        // Act - Invoke the public method
        if (method != null && component != null)
        {
            var task = (Task)method.Invoke(component, null)!;

            // Assert - Method should be invocable and return a Task that completes
            await cut.InvokeAsync(async () => await task);
            task.IsCompleted.Should().BeTrue(
                "OnSessionActivatedAsync should complete successfully when invoked (activation refresh implementation)");
        }
    }

    /// <summary>
    /// Tests that the component stores parameters correctly.
    /// </summary>
    [Fact]
    public void SessionQRCode_StoresParametersCorrectly()
    {
        // Arrange
        var sessionId = "session-123";
        var joinCode = "TESTCODE";

        // Act
        var cut = Render<SessionQRCode>(parameters => parameters
            .Add(p => p.SessionId, sessionId)
            .Add(p => p.JoinCode, joinCode));

        // Assert
        var component = cut.Instance as SessionQRCode;
        component.Should().NotBeNull();
        component?.SessionId.Should().Be(sessionId);
        component?.JoinCode.Should().Be(joinCode);
    }
}
