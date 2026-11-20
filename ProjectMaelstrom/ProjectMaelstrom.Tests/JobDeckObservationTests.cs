using Maelstrom.DeckApi;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class JobDeckObservationTests
{
    private static readonly WizwalkerTarget Target = new(42, 123, 100, "WizardGraphicalClient", "fixture", new string('a', 64));
    private static W101UiTreeResult Reading(long now, bool idle, bool menu)
    {
        Dictionary<string, bool?> states = new() { ["interactionPrompt"] = false, ["dialogue"] = false, ["menu"] = menu, ["nativeIdle"] = idle };
        return new(1, "fixture", W101UiTreeOperation.Version, WizwalkerOperation.SourceSha256, Target, "observed", "fixture",
            false, states, now, now, 1000, true, true, 5, 2, 0, new(states));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ObservationDoesNotRequireQuestActionReadiness(bool idle, bool menu)
    {
        var now = DateTime.UtcNow.Ticks;
        var read = Reading(now, idle, menu);
        Assert.True(JobDeckHost.ObservationReady(Target, read, now));
        Assert.False(W101QuestStep.EvidenceReady(Target, read, now));
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("cleanup")]
    [InlineData("hook")]
    [InlineData("target")]
    [InlineData("loading")]
    [InlineData("source")]
    [InlineData("inconsistent")]
    public void UnsafeObservationRemainsUnavailable(string fault)
    {
        var now = DateTime.UtcNow.Ticks;
        var read = Reading(now, true, false);
        read = fault switch
        {
            "stale" => read with { StatesObservedUtcTicks = now - TimeSpan.FromSeconds(3).Ticks },
            "future" => read with { ObservedUtcTicks = now + 1 },
            "cleanup" => read with { CleanupConfirmed = false },
            "hook" => read with { HookRestored = false },
            "target" => read with { Target = Target with { ProcessId = 999 } },
            "loading" => read with { Loading = true },
            "source" => read with { SourceSha256 = "wrong" },
            _ => read with { StableStates = new(read.StableStates) { ["nativeIdle"] = false } }
        };
        Assert.False(JobDeckHost.ObservationReady(Target, read, now));
    }
}
