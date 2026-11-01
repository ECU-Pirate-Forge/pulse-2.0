using Bunit;
using Pulse.Admin.Components.Pages;
using QRCoder;

namespace Pulse.Tests.Tests.WebApp;

public class QrCodeGeneratorTests : BunitContext
{
    [Theory]
    [InlineData("https://example.com/path?name=Pulse&next=%2Fjoin#section")]
    [InlineData("http://localhost:5243/join/ABC123")]
    [InlineData("https://example.com/search?q=caf\u00e9")]
    public void GenerateProducesPngForExactProvidedUrl(string url)
    {
        var cut = Render<QrCodeGenerator>();
        cut.Find("#qr-url").Input($"  {url}  ");
        cut.Find("form").Submit();

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        var expected = $"data:image/png;base64,{Convert.ToBase64String(png.GetGraphic(10))}";

        Assert.Equal(expected, cut.Find("img").GetAttribute("src"));
        Assert.Equal($"QR code for {url}", cut.Find("img").GetAttribute("alt"));
        var download = cut.Find("a[download]");
        Assert.Equal(expected, download.GetAttribute("href"));
        Assert.Equal("qr-code.png", download.GetAttribute("download"));
        Assert.Empty(cut.FindAll("[role='alert']"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a URL")]
    [InlineData("/join/ABC123")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/file")]
    [InlineData("https://")]
    public void InvalidUrlShowsAccessibleErrorWithoutGeneratingImage(string url)
    {
        var cut = Render<QrCodeGenerator>();
        cut.Find("#qr-url").Input(url);
        cut.Find("form").Submit();

        Assert.Contains("HTTP or HTTPS", cut.Find("[role='alert']").TextContent);
        Assert.Equal("true", cut.Find("#qr-url").GetAttribute("aria-invalid"));
        Assert.Empty(cut.FindAll("img"));
        Assert.Empty(cut.FindAll("a[download]"));
    }

    [Fact]
    public void EditingUrlClearsOldResultAndCanGenerateNewCode()
    {
        var cut = Render<QrCodeGenerator>();
        cut.Find("#qr-url").Input("https://example.com/first");
        cut.Find("form").Submit();
        var firstImage = cut.Find("img").GetAttribute("src");

        cut.Find("#qr-url").Input("https://example.com/second");
        Assert.Empty(cut.FindAll("img"));
        Assert.Empty(cut.FindAll("a[download]"));

        cut.Find("form").Submit();
        Assert.NotEqual(firstImage, cut.Find("img").GetAttribute("src"));
        Assert.Contains("https://example.com/second", cut.Find("img").GetAttribute("alt"));
    }

    [Fact]
    public void OverCapacityUrlShowsErrorAndCanRecover()
    {
        var cut = Render<QrCodeGenerator>();
        cut.Find("#qr-url").Input("https://example.com/" + new string('a', 5000));
        cut.Find("form").Submit();

        Assert.Contains("too long", cut.Find("[role='alert']").TextContent);
        Assert.Empty(cut.FindAll("img"));

        cut.Find("#qr-url").Input("https://example.com");
        Assert.Empty(cut.FindAll("[role='alert']"));
        cut.Find("form").Submit();
        Assert.NotNull(cut.Find("img"));
    }
}
