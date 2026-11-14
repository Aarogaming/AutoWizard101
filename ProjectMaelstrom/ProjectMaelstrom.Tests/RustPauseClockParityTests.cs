using System.Globalization;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

/// <summary>Scene-time accounting parity; this does not model UI pause/resume control.</summary>
public sealed class RustPauseClockParityTests
{
    private static double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);
    private static string ErrorName(Exception? error) => error switch
    {
        null => "-",
        ArgumentOutOfRangeException { ParamName: "pauseBudgetMs" } => "pause-budget",
        ArgumentOutOfRangeException { ParamName: "gameplayBudgetMs" } => "gameplay-budget",
        ArgumentOutOfRangeException { ParamName: "wallMs" } => "wall-time",
        ArgumentException => "scene",
        _ => throw new InvalidOperationException("Unexpected clock exception", error)
    };

    private static (bool Started, bool Loading, double Active, double Paused, string? Stop, bool Allowed) State(GameplayPauseClock clock) =>
        (clock.Started, clock.IsLoading, clock.ActiveMs, clock.PausedMs, clock.StopReason, clock.GameplayAllowed);

    [Fact]
    public void SharedPauseClockContractMatchesNative()
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", "RustParity", "pause-clock-v1.tsv"));
        Assert.Equal("# maelstrom-pause-clock-v1", lines[0]);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        GameplayPauseClock? clock = null;
        var id = "";
        var count = 0;
        var caseCount = 0;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var row = line.Split('\t');
            switch (row[0])
            {
                case "case":
                    Assert.Equal(5, row.Length);
                    Assert.True(clock is null || caseCount > 0, $"Empty valid case: {id}");
                    id = row[1];
                    Assert.True(ids.Add(id), $"Duplicate case: {id}");
                    clock = null;
                    var construction = Record.Exception(() => { clock = new GameplayPauseClock(Number(row[2]), Number(row[3])); });
                    Assert.Equal(row[4], ErrorName(construction));
                    caseCount = 0;
                    break;
                case "obs":
                    Assert.Equal(11, row.Length);
                    Assert.NotNull(clock);
                    var before = State(clock);
                    var error = Record.Exception(() => clock.Advance(Number(row[1]), bool.Parse(row[2]), bool.Parse(row[3])));
                    Assert.Equal(row[4], ErrorName(error));
                    var actual = State(clock);
                    if (row[4] != "-") Assert.Equal(before, actual);
                    var expected = (bool.Parse(row[5]), bool.Parse(row[6]), Number(row[7]), Number(row[8]),
                        row[9] == "-" ? null : row[9], bool.Parse(row[10]));
                    Assert.True(actual == expected, $"State mismatch: {id}, line {lineIndex + 1}; actual={actual}, expected={expected}");
                    count++;
                    caseCount++;
                    break;
                default:
                    throw new InvalidDataException($"Unknown fixture operation: {row[0]}");
            }
        }
        Assert.True(clock is null || caseCount > 0);
        Assert.Equal(25, ids.Count);
        Assert.Equal(51, count);
        Assert.Equal(State(new GameplayPauseClock()), State(new GameplayPauseClock(45000, 75000)));
    }
}
