using Pulse.AppHost;

namespace Pulse.Tests.Tests;

public class PublicJoinUrlTests
{
    [Theory]
    [InlineData("https://sample.devtunnels.ms", "https://sample.devtunnels.ms")]
    [InlineData("https://sample.devtunnels.ms/", "https://sample.devtunnels.ms")]
    [InlineData("  https://sample.devtunnels.ms", "https://sample.devtunnels.ms")]
    [InlineData("https://sample.devtunnels.ms/  ", "https://sample.devtunnels.ms")]
    public void Normalize_AcceptsAbsoluteHttpsUrl(string value, string expected)
    {
        Assert.Equal(expected, PublicJoinUrl.Normalize(value));
    }

    [Theory]
    [InlineData("http://sample.devtunnels.ms")]
    [InlineData("https://user:secret@sample.devtunnels.ms")]
    [InlineData("https://sample.devtunnels.ms?token=secret")]
    [InlineData("https://sample.devtunnels.ms#fragment")]
    [InlineData("/relative")]
    public void Normalize_RejectsUnsafeUrl(string value)
    {
        Assert.Throws<ArgumentException>(() => PublicJoinUrl.Normalize(value));
    }
}
