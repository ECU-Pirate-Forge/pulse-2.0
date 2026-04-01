using Moq;
using Pulse.Common.Services;
using Pulse.Shared.Services;
using Xunit;

namespace Pulse.Tests.Sessions;

public class JoinCodeGeneratorTests
{
    private readonly JoinCodeGenerator _sut = new();

    [Fact]
    public void Generate_ReturnsExactly6Characters()
    {
        var code = _sut.Generate();
        Assert.Equal(6, code.Length);
    }

    [Fact]
    public void Generate_ReturnsOnlyUpperAlphanumeric()
    {
        for (int i = 0; i < 100; i++)
        {
            var code = _sut.Generate();
            Assert.Matches("^[A-Z0-9]{6}$", code);
        }
    }

    [Fact]
    public void Generate_ProducesDifferentCodesOverTime()
    {
        var codes = Enumerable.Range(0, 20).Select(_ => _sut.Generate()).ToHashSet();
        Assert.True(codes.Count > 1, "Generator should not produce the same code every time.");
    }

    [Fact]
    public async Task CollisionRetry_RegeneratesOnDuplicate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var repoMock = new Mock<ISessionRepository>();
        var generatorMock = new Mock<IJoinCodeGenerator>();

        var callCount = 0;
        generatorMock.Setup(g => g.Generate()).Returns(() => callCount++ == 0 ? "TAKEN1" : "FREE22");
        repoMock.Setup(r => r.JoinCodeExistsAsync("TAKEN1", default)).ReturnsAsync(true);
        repoMock.Setup(r => r.JoinCodeExistsAsync("FREE22", default)).ReturnsAsync(false);

        string joinCode;
        do
        {
            joinCode = generatorMock.Object.Generate();
        }
        while (await repoMock.Object.JoinCodeExistsAsync(joinCode, cancellationToken));

        Assert.Equal("FREE22", joinCode);
        generatorMock.Verify(g => g.Generate(), Times.Exactly(2));
    }
}