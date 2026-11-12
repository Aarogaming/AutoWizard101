using System.Text;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class FirecrawlSearchTests
{
    [Fact]
    public void ParsesOnlySafeHttpsLinks()
    {
        var json = "{\"data\":{\"web\":[{\"title\":\"Guide\",\"url\":\"https://example.com/guide\"},{\"title\":\"Local\",\"url\":\"file:///tmp/file\"}]}}";
        var results = FirecrawlSearch.Parse(Encoding.UTF8.GetBytes(json));
        Assert.Single(results);
        Assert.Equal("Guide", results[0].Title);
        Assert.Throws<InvalidDataException>(() => FirecrawlSearch.Parse(Encoding.UTF8.GetBytes("{\"data\":[]}")));
    }
}
