using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

[Collection("Dodge input")]
public class DodgeLiveTests
{
    private const string Pack = """
        {"schema_version":2,"game_id":"godot-dodge-the-creeps","pack_id":"dodge-baseline","version":"0.1.0","status":"draft",
         "actions":[{"id":"move-left","title":"Move left","key":"LEFT","duration_ms":100}],
         "skills":[{"id":"avoid-left","when":{"state":"dodge-advice-left"},"propose":{"type":"action","parameters":{"action_id":"move-left"}}}]}
        """;
    private sealed class FakeSink : IDodgeKeySink
    {
        public readonly List<string> Events = new();
        public bool Ready(GameWindow target, Size size) => true;
        public bool StopRequested() => false;
        public bool MovementKeyHeld() => false;
        public void KeyDown(string key) => Events.Add("down:" + key);
        public void KeyUp(string key) => Events.Add("up:" + key);
    }

    private static Bitmap Scene(int playerX)
    {
        var bitmap = new Bitmap(480, 750);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(55, 95, 94));
        using var player = new SolidBrush(Color.FromArgb(150, 130, 130));
        using var enemy = new SolidBrush(Color.FromArgb(210, 50, 70));
        graphics.FillEllipse(player, playerX, 380, 54, 54);
        graphics.FillEllipse(Brushes.White, playerX + 14, 394, 26, 26);
        graphics.FillEllipse(enemy, 340, 385, 30, 30);
        return bitmap;
    }

    [Fact]
    public async Task OfflineFramesPackFakeInputAndOutcomeFormABoundedLoop()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Pack);
            var pack = DodgeLivePack.Load(path);
            using var before = Scene(210);
            using var after = Scene(180);
            var state = DodgeSpecialist.Analyze(before);
            var action = pack.Resolve(state.Advice);
            Assert.NotNull(action);
            var sink = new FakeSink();
            var result = await new DodgeInputPulse(sink, () => true).RunAsync(new((nint)1, 1, 1, "test", "test"), before.Size, action.Key, action.DurationMs, FrameTimeline.TimestampMs(), CancellationToken.None);
            Assert.Equal("dispatched", result.Status);
            Assert.Equal(new[] { "down:LEFT", "up:LEFT" }, sink.Events);
            var next = DodgeSpecialist.Analyze(after);
            Assert.Equal("movement-observed", DodgeLiveRun.ClassifyMovement(state.Player, next.Player, action.Id, before.Size));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void LivePackResolvesOnlyPermittedBoundedActions()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Pack);
            var pack = DodgeLivePack.Load(path);
            Assert.Equal("LEFT", pack.Resolve("Left")!.Key);
            Assert.Null(pack.Resolve("Unknown"));
            foreach (var invalid in new[] { Pack.Replace("LEFT", "ENTER"), Pack.Replace("100}", "201}"), Pack.Replace("\"schema_version\":2", "\"schema_version\":2,\"schema_version\":2"), Pack.Replace("move-left\"}}}", "move-right\"}}}"), Pack.Replace("\"state\":\"dodge-advice-left\"", "\"state\":\"dodge-advice-left\",\"health\":\"low\"") })
            {
                File.WriteAllText(path, invalid);
                Assert.ThrowsAny<Exception>(() => DodgeLivePack.Load(path));
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void DiagonalInputsAreRejectedOutsideTheContextAwareIsolatedPath()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Pack.Replace("move-left", "move-up-left").Replace("dodge-advice-left", "dodge-advice-up-left").Replace("LEFT", "UP_LEFT"));
            Assert.Throws<InvalidDataException>(() => DodgeLivePack.Load(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ContextAwareEndurancePackResolvesDiagonalMovement()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContextV12", "pack.json");
        var pack = DodgeLivePack.Load(path);
        Assert.Equal("center-first-v12", pack.Context!.AvoidancePolicy);
        Assert.Equal("UP_LEFT", pack.Resolve("Up-Left")!.Key);
        Assert.Equal("DOWN_RIGHT", pack.Resolve("Down-Right")!.Key);
    }

    [Fact]
    public void ContextRejectsActionsAboveItsDeclaredLimit()
    {
        var sample = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContext", "pack.json");
        var text = File.ReadAllText(sample).Replace("\"max_action_duration_ms\": 200", "\"max_action_duration_ms\": 100");
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, text); Assert.Throws<InvalidDataException>(() => DodgeLivePack.Load(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ContextRejectsKeysThatContradictTheDeclaredDirection()
    {
        var sample = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContext", "pack.json");
        var text = File.ReadAllText(sample).Replace("\"key\": \"LEFT\"", "\"key\": \"RIGHT\"");
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, text); Assert.Throws<InvalidDataException>(() => DodgeLivePack.Load(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ContextRejectsAdviceMappedToAnOppositeAction()
    {
        var sample = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContext", "pack.json");
        var text = File.ReadAllText(sample).Replace("\"action_id\": \"move-left\"", "\"action_id\": \"move-right\"");
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, text); Assert.Throws<InvalidDataException>(() => DodgeLivePack.Load(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ForegroundLoopRejectsContextBeforeOpeningOrFocusingTarget()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "DodgeContext", "pack.json");
        var pack = DodgeLivePack.Load(path);
        await Assert.ThrowsAsync<InvalidOperationException>(() => DodgeLiveRun.RunAsync(null!, pack, ".", () => true, CancellationToken.None));
    }

    [Fact]
    public void ObservedMovementIsNotTreatedAsSurvival()
    {
        var size = new Size(480, 750);
        Assert.Equal("movement-observed", DodgeLiveRun.ClassifyMovement(new(.5f, .5f), new(.46f, .5f), "move-left", size));
        Assert.Equal("movement-not-observed", DodgeLiveRun.ClassifyMovement(new(.5f, .5f), new(.54f, .5f), "move-left", size));
        Assert.Equal("unknown", DodgeLiveRun.ClassifyMovement(new(.5f, .5f), null, "move-left", size));
        Assert.Equal("movement-observed", DodgeLiveRun.ClassifyMovement(new(.5f, .5f), new(.46f, .46f), "move-up-left", size));
        Assert.Equal("movement-not-observed", DodgeLiveRun.ClassifyMovement(new(.5f, .5f), new(.46f, .54f), "move-up-left", size));
    }

    [Fact]
    public void ActionBudgetClosesAtTheTimeOrActionLimit()
    {
        Assert.True(DodgeLiveRun.WithinBudget(TimeSpan.FromMilliseconds(2999), 3, 0, 1));
        Assert.False(DodgeLiveRun.WithinBudget(TimeSpan.FromSeconds(3), 3, 0, 1));
        Assert.False(DodgeLiveRun.WithinBudget(TimeSpan.FromSeconds(1), 3, 1, 1));
    }

    [Fact]
    public void CaptureFreshnessRejectsOldFutureAndInvalidTimes()
    {
        Assert.True(DodgeLiveRun.IsFreshCapture(900, 1000, 150));
        Assert.False(DodgeLiveRun.IsFreshCapture(800, 1000, 150));
        Assert.False(DodgeLiveRun.IsFreshCapture(1001, 1000, 150));
        Assert.False(DodgeLiveRun.IsFreshCapture(double.NaN, 1000, 150));
    }
}
