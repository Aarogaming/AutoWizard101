using System.Text;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class W101LegacyCatalogTests
{
    [Fact]
    public void EmbeddedCatalogueRetainsAllSourcesAndCompetingApproaches()
    {
        var catalog = W101LegacyCatalog.Load();
        Assert.Equal(25, catalog.Entries.Length);
        Assert.All(catalog.Entries, entry => Assert.False(entry.LiveQualified));
        var visuals = catalog.ForCapability("Visual recognition");
        Assert.Contains(visuals, entry => entry.Id == "wizsdk-visual");
        Assert.Contains(visuals, entry => entry.Id == "wizapi-visual");
        Assert.Contains(catalog.ForCapability("Navigation"), entry => entry.Id == "deimos-main");
        Assert.Contains(catalog.ForCapability("Navigation"), entry => entry.Id == "deimos-master");
        Assert.Empty(catalog.ForCapability("Unknown"));
    }

    [Fact]
    public void CatalogueCannotGrantExecutionOrDiscardSourceIdentity()
    {
        var catalog = W101LegacyCatalog.Load();
        var entry = catalog.Entries[0];
        foreach (var invalid in new[] {
            catalog with { Entries = [entry with { LiveQualified = true }] },
            catalog with { Entries = [entry with { Integration = "Run" }] },
            catalog with { Entries = [entry with { Sha256 = "unknown" }] },
            catalog with { Entries = [entry, entry] } })
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(invalid)));
            Assert.Throws<InvalidDataException>(() => W101LegacyCatalog.Read(stream));
        }
    }
}
