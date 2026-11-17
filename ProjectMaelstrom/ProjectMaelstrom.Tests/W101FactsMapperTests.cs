using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101FactsMapperTests
{
    private const long T0 = 639000000000000000;

    private static W101UiTreeResult Observation(bool cleaned = true, bool idle = true, string? zone = null,
        string? objective = null) => new(
        SchemaVersion: 1, RunId: new string('a', 32), Operation: "w101.ui-tree-observe",
        SourceSha256: new string('b', 64),
        Target: new WizwalkerTarget(42, 123, 456, "WizardGraphicalClient", "image", new string('c', 64)),
        Status: cleaned ? "observed" : "failed", Code: "ok", Loading: false,
        States: new Dictionary<string, bool?> { ["nativeIdle"] = idle, ["menu"] = false },
        ObservedUtcTicks: T0, StatesObservedUtcTicks: T0, DurationMs: 100,
        CleanupConfirmed: cleaned, HookRestored: cleaned, LoadingSamples: 4, TreeSamples: 20, TreeFailures: 0,
        StableStates: new Dictionary<string, bool?> { ["nativeIdle"] = idle, ["menu"] = false },
        StableQuestContext: objective is null ? null : new W101QuestContext(objective, null, false),
        PoseCandidate: zone is null ? null : new W101PoseCandidate("archived-wizwalker-client-zone", "candidate", false, false,
            null, null, T0, 4, 64,
            JsonDocument.Parse($"{{\"zoneName\":\"{zone}\"}}").RootElement.Clone()));

    [Fact]
    public void OnlyCleanObservationsPublishFacts()
    {
        var store = new W101FactsStore(W101FactsMapper.ProposedFacts);
        Assert.Equal(3, W101FactsMapper.Map(Observation(zone: "Housing/CardPromo/GS_Fantasy_Castle"), store));
        Assert.Equal("true", store.Get("nativeIdle", T0)!.Value);
        Assert.Equal(W101FactQualification.Candidate, store.Get("zone.candidate", T0)!.Qualification);
        Assert.Equal(0, W101FactsMapper.Map(Observation(cleaned: false), store));
    }

    [Fact]
    public void QuestObjectiveMapsFromStableContextOnly()
    {
        var store = new W101FactsStore(W101FactsMapper.ProposedFacts);
        W101FactsMapper.Map(Observation(objective: "Talk to the NPC"), store);
        Assert.Equal("Talk to the NPC", store.Get("quest.objective", T0)!.Value);
    }

    [Fact]
    public async Task SessionInvalidatesOnIdentityChange()
    {
        var store = new W101FactsStore(W101FactsMapper.ProposedFacts);
        var calls = 0;
        var session = new W101FactsSession(
            _ => Task.FromResult<W101UiTreeResult?>(Observation(idle: true)),
            () => ++calls == 1 ? (true, "") : (false, "process-restarted"),
            delay: (_, _) => Task.CompletedTask, maximumCycles: 4);
        Assert.Equal("target-changed", await session.RunAsync(store, CancellationToken.None));
        Assert.Equal("process-restarted", store.Get("nativeIdle", T0)!.Reason);
    }

    [Fact]
    public async Task SessionMapsBoundedCyclesAndStopsOnRecovery()
    {
        var store = new W101FactsStore(W101FactsMapper.ProposedFacts);
        var recovery = false;
        var session = new W101FactsSession(
            _ => Task.FromResult<W101UiTreeResult?>(Observation(idle: true)),
            () => (true, ""), () => recovery,
            delay: (_, _) => { recovery = true; return Task.CompletedTask; }, maximumCycles: 4);
        Assert.Equal("recovery-hold", await session.RunAsync(store, CancellationToken.None));
        Assert.Equal(W101FactQualification.Unknown, store.Get("nativeIdle", T0)!.Qualification);
    }
}
