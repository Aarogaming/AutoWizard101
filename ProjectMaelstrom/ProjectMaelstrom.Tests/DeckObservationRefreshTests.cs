using ProjectMaelstrom.Utilities;
using System.Text.Json;

namespace ProjectMaelstrom.Tests;

public sealed class DeckObservationRefreshTests
{
    private readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void LateResultsAfterCancellationOrDeadlineCannotPublish()
    {
        Assert.True(DeckObservationRefresh.CanPublish(1, 1, _now, _now.AddSeconds(1), false));
        Assert.False(DeckObservationRefresh.CanPublish(1, 2, _now, null, false));
        Assert.False(DeckObservationRefresh.CanPublish(1, 1, _now, null, true));
        Assert.False(DeckObservationRefresh.CanPublish(1, 1, _now, _now, false));
    }
    [Fact]
    public void StaleFactsRefreshWithCooldownAndFiniteBudget()
    {
        var gate = new DeckObservationRefresh();
        for (var i = 0; i < 30; i++)
        {
            Assert.True(gate.Admit("run", _now.AddSeconds(i * 5), _now.AddHours(1), null, true, true, false, false));
            Assert.False(gate.Admit("run", _now.AddSeconds(i * 5 + 1), _now.AddHours(1), null, true, true, false, false));
        }
        Assert.False(gate.Admit("run", _now.AddMinutes(3), _now.AddHours(1), null, true, true, false, false));
        Assert.Equal("refresh-budget-exhausted", gate.Reason);
        Assert.True(gate.Admit("new-run", _now.AddMinutes(3), _now.AddHours(1), null, true, true, false, false));
    }
    [Theory]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, false, true)]
    public void IneligibleStatesCannotConsumeBudget(bool waiting, bool enabled, bool busy, bool blocked)
    {
        var gate = new DeckObservationRefresh();
        Assert.False(gate.Admit("run", _now, _now.AddMinutes(1), null, waiting, enabled, busy, blocked));
        Assert.Equal(0, gate.Attempts);
    }
    [Fact]
    public void FreshFactsAndInsufficientDeadlineDoNotRefresh()
    {
        var gate = new DeckObservationRefresh();
        Assert.False(gate.Admit("run", _now, _now.AddMinutes(1), _now, true, true, false, false));
        Assert.False(gate.Admit("run", _now, _now.AddSeconds(4), null, true, true, false, false));
        Assert.False(gate.Admit("run", _now, null, null, true, true, false, false));
        Assert.Equal(0, gate.Attempts);
    }
    [Fact]
    public async Task SyntheticProviderWakesWaitingDeckAndStopPreventsFurtherRefresh()
    {
        var root = Path.Combine(Path.GetTempPath(), "deck-refresh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var scheduler = new OperationScheduler(root, Path.Combine(root, "locks"));
            JobFacts? facts = null;
            var actions = 0;
            var providerCalls = 0;
            var routine = new JobRoutine(new("synthetic", "Synthetic", "Test", "test", "*", "Offline only", true, false, [], "No inputs"),
                _ => { }, (_, _) => new(new(Guid.NewGuid().ToString("N"), "refresh.test", "1", 5, false, "synthetic", new Dictionary<string, string>()),
                (context, token) =>
                {
                    Interlocked.Increment(ref actions);
                    var report = Path.Combine(context.Directory, "result.json");
                    GameExperiments.WriteAtomic(report, new { synthetic = true });
                    return Task.FromResult(OperationOutcome.Completed("Synthetic completion", report));
                }));
            var card = new JobCardSpec(Guid.NewGuid().ToString("N"), "synthetic", "Wait", true,
                Conditions: [new("area", "eq", JsonSerializer.SerializeToElement("ready"))]);
            await using var deck = new JobDeckService(root, scheduler, [routine], new("Test", "offline-lab", [card]), () => facts);
            var run = Guid.NewGuid().ToString("N");
            deck.Start(run, true);
            Assert.Equal("Waiting", deck.State.Status);
            Assert.Equal(0, actions);
            var gate = new DeckObservationRefresh();
            void Refresh()
            {
                var state = deck.State;
                if (!gate.Admit(run, DateTime.UtcNow, state.StartedUtc?.AddSeconds(state.Deck.DurationSeconds), facts?.ObservedUtc,
                    state.Status == "Waiting", true, scheduler.Pending > 0, state.RecoveryRequired)) return;
                providerCalls++;
                facts = new("offline-lab", "synthetic", DateTime.UtcNow, new() { ["area"] = JsonSerializer.SerializeToElement("ready") }, "fake-provider");
            }
            Refresh();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (deck.State.Progress.GetValueOrDefault(card.Id)?.Completed != 1) await Task.Delay(20, timeout.Token);
            Assert.Equal(1, actions);
            Assert.Equal(1, providerCalls);
            deck.Stop(); facts = null;
            Refresh();
            Assert.Equal(1, providerCalls);
            Assert.Equal(1, actions);
        }
        finally { Directory.Delete(root, true); }
    }
}
