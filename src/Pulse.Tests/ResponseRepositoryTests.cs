using LiteDB;
using Pulse.Shared.Models;
using Pulse.Shared.Services;

namespace Pulse.Tests.Tests;

public class ResponseRepositoryTests
{
    private static ResponseRepository CreateRepository() =>
        new ResponseRepository(new LiteDatabase("Filename=:memory:"));

    [Fact]
    public void UpsertByQuestionAndDevice_NewResponse_InsertsAndReturnsResponse()
    {
        var repo = CreateRepository();
        var deviceId = Guid.NewGuid().ToString();

        var result = repo.UpsertByQuestionAndDevice("q1", deviceId, "A");

        Assert.NotNull(result);
        Assert.Equal("q1", result.QuestionId);
        Assert.Equal(deviceId, result.DeviceId);
        Assert.Equal("A", result.Value);
    }

    [Fact]
    public void UpsertByQuestionAndDevice_SameDeviceAndQuestion_ReplacesValue()
    {
        var repo = CreateRepository();
        var deviceId = Guid.NewGuid().ToString();

        repo.UpsertByQuestionAndDevice("q1", deviceId, "A");
        var updated = repo.UpsertByQuestionAndDevice("q1", deviceId, "B");

        Assert.Equal("B", updated.Value);
    }

    [Fact]
    public void UpsertByQuestionAndDevice_DifferentDevice_CreatesNewResponse()
    {
        var repo = CreateRepository();
        var device1 = Guid.NewGuid().ToString();
        var device2 = Guid.NewGuid().ToString();

        repo.UpsertByQuestionAndDevice("q1", device1, "A");
        var result = repo.UpsertByQuestionAndDevice("q1", device2, "B");

        Assert.Equal(device2, result.DeviceId);
        Assert.Equal("B", result.Value);
    }

    [Fact]
    public void UpsertByQuestionAndDevice_InvalidDeviceId_ThrowsArgumentException()
    {
        var repo = CreateRepository();

        Assert.Throws<ArgumentException>(() =>
            repo.UpsertByQuestionAndDevice("q1", "not-a-guid", "A"));
    }
}
