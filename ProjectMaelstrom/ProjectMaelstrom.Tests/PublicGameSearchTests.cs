using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PublicGameSearchTests
{
    [Fact]
    public void ParsesSourceTitlesIntoPublicLinks()
    {
        var results = PublicGameSearch.Parse("{\"query\":{\"search\":[{\"title\":\"Wizard101\"},{\"title\":\"Game & Watch\"}]}}");
        Assert.Equal(2, results.Count);
        Assert.Equal("https://en.wikipedia.org/wiki/Wizard101", results[0].Url);
        Assert.Contains("%26", results[1].Url);
    }
}
