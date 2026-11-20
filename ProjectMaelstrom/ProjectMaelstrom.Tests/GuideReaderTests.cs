using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class GuideReaderTests
{
    [Fact]
    public void RemovesScriptsAndDecodesText()
    {
        var text = GuideReader.ExtractText("<h1>Guide</h1><script>Press Delete</script><p>Press &amp; hold.</p>", true);
        Assert.Contains("Press & hold.", text);
        Assert.DoesNotContain("Press Delete", text);
    }
    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://user:password@example.com")]
    public async Task RejectsUnsafeUrls(string url) => await Assert.ThrowsAsync<InvalidDataException>(() => GuideReader.FetchAsync(url));
}
