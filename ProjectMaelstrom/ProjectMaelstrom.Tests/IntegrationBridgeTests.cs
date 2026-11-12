using System.Text.Json;
using ProjectMaelstrom.Models;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class IntegrationBridgeTests
{
    [Fact]
    public void CompanionMergePreservesPrimaryAndKeepsDifferentLocations()
    {
        var original = new WikiMob { Name = "Boss", Zone = "Area", School = "Fire" };
        var merged = WizWikiDataService.MergeMobSources(
            new[] { original },
            new[]
            {
                new WikiMob { Name = " boss ", Zone = "AREA", School = "Ice" },
                new WikiMob { Name = "Boss", Zone = "Elsewhere" },
                new WikiMob { Name = " " }
            });

        Assert.Equal(2, merged.Count);
        Assert.Same(original, merged[0]);
        Assert.Equal("Elsewhere", merged[1].Zone);
    }

    [Fact]
    public void CompanionExportShapeDeserializesForNativeKnowledge()
    {
        const string json = """[{"Name":"Boss","Zone":"Location text","School":"Fire","Type":"Boss","Cheats":["Keep shields"],"Source":"wizard101-companion"}]""";
        var mobs = JsonSerializer.Deserialize<List<WikiMob>>(json)!;
        var merged = WizWikiDataService.MergeMobSources(Array.Empty<WikiMob>(), mobs);
        Assert.Equal("Boss", Assert.Single(merged).Name);
        Assert.Equal("Location text", merged[0].Zone);
    }

    [Fact]
    public void SnapshotReplacementProducesCompleteJsonAndCleansTemporaryFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maelstrom-snapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "snapshot.json");
        try
        {
            SnapshotBridge.WriteSnapshotFile(path, new GameStateSnapshot { Resolution = "old" });
            SnapshotBridge.WriteSnapshotFile(path, new GameStateSnapshot { Resolution = "1280x720", HasFocus = true });
            var result = JsonSerializer.Deserialize<GameStateSnapshot>(File.ReadAllText(path))!;
            Assert.Equal("1280x720", result.Resolution);
            Assert.True(result.HasFocus);
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }
}
