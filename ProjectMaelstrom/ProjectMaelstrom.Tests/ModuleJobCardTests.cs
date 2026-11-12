using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class ModuleJobCardTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "module-card-" + Guid.NewGuid().ToString("N"));
    public ModuleJobCardTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    internal JobCardSpec Fixture(string kind, bool receipts = true)
    {
        var source = ModuleRunnerTests.LabBundle();
        var module = source.Modules.Single(m => m.Kind == kind);
        var bundle = source with { Modules = [module] };
        var binding = ModuleRunnerTests.Binding(bundle);
        var bundlePath = Path.Combine(_root, "bundle.json"); ModuleCatalog.Save(bundle, bundlePath);
        var sessionPath = Path.Combine(_root, "session.json");
        File.WriteAllText(sessionPath, JsonSerializer.Serialize(new
        {
            schema_version = 1,
            session_id = "card-test",
            clock_origin = "synthetic-test",
            events_file = "events.jsonl",
            routine_binding = binding
        }, OperationModules.Json));
        var scenes = kind switch
        {
            "navigation" => new[] { "Spawn", "Pad" },
            "sigil" => ["Pad", "Station"],
            "menu" => ["Station", "Ready"],
            _ => ["Ready", "PauseMenu", "Resting"]
        };
        var runner = new ModuleRunner(bundle, binding, "card-test");
        var events = new List<string>(); var sequence = 0;
        var json = new JsonSerializerOptions(OperationModules.Json) { WriteIndented = false };
        void Event(double time, string type, object payload) => events.Add(JsonSerializer.Serialize(new
        { event_id = "e" + sequence, sequence = sequence++, time_ms = time, kind = type, payload }, json));
        var frame = ModuleRunnerTests.Frame(binding, 0, scenes[0]);
        var decision = runner.Observe(frame); Event(0, "observation", new { module_observation = frame });
        for (var i = 1; i < scenes.Length; i++)
        {
            var receipt = new RoutineReceipt(decision.ProposalId!, decision.ActionId!, i * 100 - 1, true, true);
            runner.RecordReceipt(receipt);
            if (receipts) Event(receipt.TimeMs, "action_receipt", new { routine_receipt = receipt });
            frame = ModuleRunnerTests.Frame(binding, i * 100, scenes[i], decision.ProposalId);
            Event(i * 100, "outcome", new { module_observation = frame });
            decision = runner.Observe(frame);
        }
        Assert.True(decision.Completed);
        File.WriteAllLines(Path.Combine(_root, "events.jsonl"), events);
        var info = JobDeckRoutines.Create().Single(r => r.Info.Id == "module." + kind).Info;
        return JobDeckRoutines.NewCard(info) with
        {
            InDeck = true,
            Settings = new()
            { ["mode"] = "offline-replay", ["bundleFile"] = bundlePath, ["sessionFile"] = sessionPath, ["timeoutSeconds"] = "10" }
        };
    }

    [Theory]
    [InlineData("navigation")]
    [InlineData("sigil")]
    [InlineData("menu")]
    [InlineData("auto-idle")]
    public async Task EachModuleCardRunsThroughDeckAndOneSchedulerWithoutInput(string kind)
    {
        var card = Fixture(kind); var data = Path.Combine(_root, "data");
        await using var scheduler = new OperationScheduler(data, Path.Combine(data, "locks"));
        await using var deck = new JobDeckService(data, scheduler, JobDeckRoutines.Create(),
            new("Modules", "offline-lab", [card]), () => null);
        deck.Start(Guid.NewGuid().ToString("N"), false);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (deck.State.ActiveCard != null) await Task.Delay(10, deadline.Token);
        Assert.Equal(1, deck.State.Progress[card.Id].Completed);
        var receipt = Assert.Single(OperationScheduler.History(data));
        Assert.Equal(OperationState.Completed, receipt.State);
        Assert.False(receipt.Request.WritesGame); Assert.False(receipt.Request.DelegatedInput);
        Assert.Null(GameInputLease.Current);
    }

    [Fact]
    public async Task MissingRecordedReceiptsCannotCompleteCard()
    {
        var card = Fixture("sigil", false); var data = Path.Combine(_root, "data");
        await using var scheduler = new OperationScheduler(data, Path.Combine(data, "locks"));
        await using var deck = new JobDeckService(data, scheduler, JobDeckRoutines.Create(), new("Modules", "offline-lab", [card]), () => null);
        deck.Start(Guid.NewGuid().ToString("N"), false);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (deck.State.ActiveCard != null) await Task.Delay(10, deadline.Token);
        Assert.Equal(0, deck.State.Progress[card.Id].Completed);
    }

    [Theory]
    [InlineData("mode", "live")]
    [InlineData("timeoutSeconds", "0")]
    [InlineData("timeoutSeconds", "301")]
    [InlineData("bundleFile", "")]
    public void InvalidCardConfigurationIsRejected(string setting, string value)
    {
        var card = Fixture("sigil"); card.Settings![setting] = value;
        Assert.Throws<InvalidDataException>(() => JobDeckRules.Validate(new("Modules", "offline-lab", [card]),
            JobDeckRoutines.Create().ToDictionary(r => r.Info.Id)));
    }

    [Fact]
    public async Task ExistingDeckCanExplicitlyAddUndealtModuleWithoutDispatchOrReplacement()
    {
        var catalog = JobDeckRoutines.Create();
        var original = JobDeckRoutines.NewCard(catalog.Single(r => r.Info.Id == "review.routine").Info);
        var data = Path.Combine(_root, "data");
        await using var scheduler = new OperationScheduler(data, Path.Combine(data, "locks"));
        await using var deck = new JobDeckService(data, scheduler, catalog, new("Existing", "offline-lab", [original]), () => null);
        Assert.Single(deck.State.Deck.Cards);
        var addition = JobDeckRoutines.NewCard(catalog.Single(r => r.Info.Id == "module.navigation").Info);
        var state = deck.State;
        deck.Replace(state.Deck with { Cards = [.. state.Deck.Cards, addition] }, state.Revision);
        Assert.Equal(original.Id, deck.State.Deck.Cards[0].Id);
        Assert.False(deck.State.Deck.Cards[1].InDeck);
        Assert.Empty(OperationScheduler.History(data)); Assert.Empty(deck.State.Progress);
    }

    [Fact]
    public void OldAutoIdleCardRetainsItsIdentityAndLiveUnavailableStatus()
    {
        var routines = JobDeckRoutines.Create();
        var old = Assert.Single(routines, r => r.Info.Id == "w101.auto-idle");
        Assert.False(old.Info.Available); Assert.True(old.Info.WritesGame);
        Assert.Equal(4, routines.Count(r => r.Info.Id.StartsWith("module.", StringComparison.Ordinal)));
        Assert.All(routines.Where(r => r.Info.Id.StartsWith("module.", StringComparison.Ordinal)),
            r => { Assert.True(r.Info.Available); Assert.False(r.Info.WritesGame); Assert.Equal("*", r.Info.GameId); });
    }
}
