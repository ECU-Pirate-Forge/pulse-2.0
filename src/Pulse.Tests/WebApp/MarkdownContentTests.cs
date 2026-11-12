using Bunit;
using Pulse.Shared.Components;

namespace Pulse.Tests.Tests.WebApp;

public class MarkdownContentTests : BunitContext
{
    [Fact]
    public void RendersStandardMarkdown()
    {
        var cut = Render<MarkdownContent>(p => p.Add(x => x.Text, """
            # Instructions

            Read **carefully**, *please*, and use `code`.

            - First
            - Second

            1. One
            2. Two

            > A quotation

            [Help](https://example.com/help)

            ![Diagram](https://example.com/image.png)

            ```text
            a < b
            ```
            """));

        Assert.Equal("Instructions", cut.Find("h1").TextContent);
        Assert.Equal("carefully", cut.Find("strong").TextContent);
        Assert.Equal("please", cut.Find("em").TextContent);
        Assert.Equal(2, cut.FindAll("ul li").Count);
        Assert.Equal(2, cut.FindAll("ol li").Count);
        Assert.Contains("A quotation", cut.Find("blockquote").TextContent);
        Assert.Equal("https://example.com/help", cut.Find("a").GetAttribute("href"));
        Assert.Equal("Diagram", cut.Find("img").GetAttribute("alt"));
        Assert.Equal("https://example.com/image.png", cut.Find("img").GetAttribute("src"));
        Assert.Contains("a < b", cut.Find("pre code").TextContent);
    }

    [Fact]
    public void RawHtmlIsText_NotExecutableMarkup()
    {
        const string text = "<script>alert(1)</script>\n\n<img src=x onerror=alert(1)>\n\n<iframe src='https://example.com'></iframe>";
        var cut = Render<MarkdownContent>(p => p.Add(x => x.Text, text));

        Assert.Empty(cut.FindAll("script, img, iframe, [onerror]"));
        Assert.Contains("<script>alert(1)</script>", cut.Find(".pulse-markdown").TextContent);
        Assert.Contains("<img src=x onerror=alert(1)>", cut.Find(".pulse-markdown").TextContent);
    }

    [Theory]
    [InlineData("javascript:alert%281%29")]
    [InlineData("JaVaScRiPt:alert%281%29")]
    [InlineData("jav&#x61;script:alert%281%29")]
    [InlineData("java&#x09;script:alert%281%29")]
    [InlineData("vbscript:msgbox%281%29")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("file:///C:/secret.txt")]
    public void UnsafeDestinationsAreNotRendered(string url)
    {
        var cut = Render<MarkdownContent>(p => p.Add(x => x.Text, $"[Link]({url}) ![Image]({url})"));

        Assert.Empty(cut.FindAll("a, img"));
        Assert.Contains("Link", cut.Find(".pulse-markdown").TextContent);
        Assert.Contains("Image", cut.Find(".pulse-markdown").TextContent);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com")]
    [InlineData("mailto:teacher@example.com")]
    [InlineData("/instructions")]
    [InlineData("instructions.md")]
    [InlineData("#instructions")]
    public void SafeLinksArePreserved(string url)
    {
        var cut = Render<MarkdownContent>(p => p.Add(x => x.Text, $"[Help]({url})"));

        Assert.Equal(url, cut.Find("a").GetAttribute("href"));
    }

    [Fact]
    public void ChangedParametersRefreshTheRenderedMarkdown()
    {
        var cut = Render<MarkdownContent>(p => p.Add(x => x.Text, "**First**"));

        cut.Render(p => p.Add(x => x.Text, "*Second*"));

        Assert.Empty(cut.FindAll("strong"));
        Assert.Equal("Second", cut.Find("em").TextContent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyContentRendersWithoutMarkup(string? text)
    {
        var cut = Render<MarkdownContent>(p => p.Add(x => x.Text, text));

        Assert.Empty(cut.Find(".pulse-markdown").Children);
    }

    [Fact]
    public void PlainTextIsPreserved()
    {
        var cut = Render<MarkdownContent>(p => p.Add(x => x.Text, "Tell us about you & your class"));

        Assert.Equal("Tell us about you & your class", cut.Find("p").TextContent);
    }
}
