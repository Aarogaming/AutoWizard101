using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class W101RoutineTests
{
    [Fact]
    public void CompilesSharedPrimitivesAndRequiresHomeForTranslation()
    {
        var options = new HomeMovementOptions(Mode: HomeIdleMode.Routine, RoutineActions: ["turn-left", "step-forward", "turn-right"]);
        options.Validate(); Assert.Equal(new[] { "A", "W", "D" }, options.Keys);
        Assert.True(options.RequiresHome); Assert.True(options.UsesRollingScene);
        Assert.False((options with { RoutineActions = ["turn-left"] }).RequiresHome);
    }
    [Fact]
    public void RejectsUnknownEmptyOversizedOrOverBudgetChains()
    {
        foreach (var actions in new string[][] { [], ["CHAT"], Enumerable.Repeat("turn-left", 9).ToArray() })
            Assert.Throws<InvalidDataException>(() => new HomeMovementOptions(Mode: HomeIdleMode.Routine, RoutineActions: actions).Validate());
        Assert.Throws<InvalidDataException>(() => new HomeMovementOptions(PulseMs: 150, Mode: HomeIdleMode.Routine, RoutineActions: Enumerable.Repeat("turn-left", 5).ToArray()).Validate());
        Assert.Throws<InvalidDataException>(() => new W101Routine("../bad", "Bad", "Pets", ["turn-left"]).Validate());
    }
    [Fact]
    public void SavesReusableDraftAndExportsRegistryWithoutInventingQualification()
    {
        var root = Path.Combine(Path.GetTempPath(), "w101-routines-" + Guid.NewGuid().ToString("N"));
        try
        {
            var routine = new W101Routine("example", "Turn sequence", "Potions", ["turn-left", "turn-left", "turn-right"]);
            var path = routine.Save(root); var loaded = W101Routine.Load(path);
            Assert.Equal(routine.Actions, loaded.Actions); Assert.Equal("Potions", loaded.Category);
            var pack = routine.ExportPack(root);
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(pack, "pack.json")));
            var content = json.RootElement;
            Assert.Equal(2, content.GetProperty("schema_version").GetInt32());
            Assert.Equal("wizard101", content.GetProperty("game_id").GetString());
            Assert.Equal("draft", content.GetProperty("status").GetString());
            Assert.Equal(2, content.GetProperty("actions").GetArrayLength());
            Assert.Equal(0, content.GetProperty("skills").GetArrayLength());
            Assert.Equal(3, content.GetProperty("routine_draft").GetProperty("sequence").GetArrayLength());
            Assert.NotEqual(pack, routine.ExportPack(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData(0, 4)]
    [InlineData(61, 4)]
    [InlineData(30, 0)]
    [InlineData(30, 6)]
    public void PassiveSamplerHasFiniteResourceBounds(int seconds, int fps) => Assert.Throws<ArgumentOutOfRangeException>(() => PassiveTeachingSampler.Validate(seconds, fps));
}
