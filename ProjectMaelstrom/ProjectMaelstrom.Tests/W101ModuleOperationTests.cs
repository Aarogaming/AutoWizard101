using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101ModuleOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "w101-modules-" + Guid.NewGuid().ToString("N"));
    private readonly WizwalkerTarget _target = W101QuestConversationTests.Target;
    public W101ModuleOperationTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData("sigil", "success", 1, true)]
    [InlineData("menu", "success", 1, true)]
    [InlineData("native-idle", "success", 2, true)]
    [InlineData("sigil", "loading", 1, true)]
    [InlineData("sigil", "dialogue", 0, false)]
    [InlineData("sigil", "no-outcome", 1, false)]
    [InlineData("menu", "takeover", 0, false)]
    [InlineData("menu", "reader-error", 0, false)]
    [InlineData("menu", "unreleased", 1, false)]
    [InlineData("sigil", "prompt-mismatch", 0, false)]
    [InlineData("menu", "card", 1, true)]
    public async Task SharedOwnerRequiresIndependentOutcomes(string mode, string scenario, int expectedActions, bool completed)
    {
        double clock = 10000; int actions = 0, reads = 0; bool taken = false;
        var bundle = W101ModuleBindings.Bundle(mode);
        var binding = bundle.Bind("pid-" + _target.ProcessId + "-start-" + _target.StartedUtcTicks, "qualified-test-layout", "test");
        var runtime = new W101ModuleRuntime(() => !taken,
            (context, pending, token) =>
            {
                Assert.Same(context.Input, GameInputLease.Current);
                Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competitor", Path.Combine(_root, "locks")));
                if (scenario == "reader-error") throw new IOException("Synthetic reader failure");
                clock += 500; reads++;
                if (scenario == "takeover") taken = true;
                var idle = mode == "native-idle" && actions == 2;
                var menu = actions > 0 && !idle;
                var states = new Dictionary<string, bool?>
                {
                    ["interactionPrompt"] = mode == "sigil" && actions == 0,
                    ["dialogue"] = scenario == "dialogue",
                    ["menu"] = menu,
                    ["nativeIdle"] = idle
                };
                var reading = W101QuestConversationTests.Read(clock, null) with
                {
                    Target = _target,
                    States = states,
                    StableStates = new(states),
                    Loading = scenario == "loading" && reads == 1
                };
                return Task.FromResult(new W101ModuleSample(reading,
                    new(OutsideReady: !menu && !idle, InteractionVerified: mode == "sigil",
                        GameMenuVerified: mode != "sigil" && menu, IdleControlVerified: mode == "native-idle" && menu,
                        InteractionMenuVerified: mode == "sigil" && menu),
                    scenario == "no-outcome" ? null : pending));
            }, (context, proposal, ready, token) =>
            {
                Assert.True(ready()); Assert.Same(context.Input, GameInputLease.Current); actions++; clock++;
                return Task.FromResult(new RoutineReceipt(proposal.ProposalId!, proposal.ActionId!, clock, true, scenario != "unreleased"));
            }, () => clock, () => W101QuestConversationTests.Stamp(clock));
        await using var scheduler = new OperationScheduler(_root, Path.Combine(_root, "locks"));
        OperationReceipt receipt;
        if (scenario == "card")
        {
            var adapter = W101ModuleOperation.ForCards(_target, "qualified-test-layout", runtime, _ => true);
            var routines = JobDeckRoutines.Create(w101Modules: adapter);
            var card = JobDeckRoutines.NewCard(routines.Single(r => r.Info.Id == "w101.menu").Info) with { InDeck = true };
            await using var deck = new JobDeckService(_root, scheduler, routines, new("Test", "wizard101", [card]),
                () => new("wizard101", W101QuestStep.TargetBinding(_target), new DateTime(W101QuestConversationTests.Stamp(clock), DateTimeKind.Utc), new(), "synthetic"),
                () => new DateTime(W101QuestConversationTests.Stamp(clock), DateTimeKind.Utc));
            deck.Start(Guid.NewGuid().ToString("N"), false);
            using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (deck.State.ActiveCard != null) await Task.Delay(10, wait.Token);
            Assert.Equal(1, deck.State.Progress[card.Id].Completed);
            receipt = Assert.Single(OperationScheduler.History(_root));
        }
        else
        {
            var job = W101ModuleOperation.Prepare(mode, _target, binding, true, runtime, 8,
                expectedPrompt: scenario == "prompt-mismatch" ? "Selected prompt" : null);
            receipt = await scheduler.Submit(job.Request, job.Execute).Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(expectedActions, actions);
        Assert.Equal(completed, receipt.State == OperationState.Completed);
        Assert.Single(OperationScheduler.History(_root));
        Assert.All(receipt.Artifacts, path => Assert.True(File.Exists(path)));
        if (scenario is "reader-error" or "unreleased")
            Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", Path.Combine(_root, "locks")));
        else { using var next = GameInputLease.Acquire("next", Path.Combine(_root, "locks")); Assert.True(next.CanDispatch); }
    }

    [Fact]
    public void DisabledBindingDoesNotPrepareWork()
    {
        var bundle = W101ModuleBindings.Bundle("menu");
        var binding = bundle.Bind("pid-42-start-123", "test", "test");
        Assert.Throws<InvalidOperationException>(() => W101ModuleOperation.Prepare("menu", _target, binding, false, null!));
        Assert.Null(GameInputLease.Current);
    }

    [Fact]
    public void CardFactoryRequiresQualifiedFreshTargetAndPreservesTimeout()
    {
        var now = DateTime.UtcNow;
        var runtime = new W101ModuleRuntime(() => true, (_, _, _) => throw new InvalidOperationException("No reads during prepare"),
            (_, _, _, _) => throw new InvalidOperationException("No input during prepare"), () => 1000, () => now.Ticks);
        var card = new JobCardSpec(Guid.NewGuid().ToString("N"), "w101.menu", "Menu", Settings: new() { ["timeoutSeconds"] = "2", ["action"] = "open-menu" });
        var facts = new JobFacts("wizard101", W101QuestStep.TargetBinding(_target), now, new(), "synthetic");
        var adapter = W101ModuleOperation.ForCards(_target, "qualified-test", runtime, _ => true);
        Assert.NotNull(adapter.Readiness("menu", card, null));
        Assert.NotNull(adapter.Readiness("menu", card, facts with { TargetId = "other" }));
        Assert.Null(adapter.Readiness("menu", card, facts));
        var job = adapter.Prepare("menu", card, facts);
        Assert.Equal(2, job.Request.TimeoutSeconds); Assert.True(job.Request.WritesGame);
        Assert.NotNull(W101ModuleOperation.ForCards(_target, "qualified-test", runtime, _ => false).Readiness("menu", card, facts));
        Assert.Null(GameInputLease.Current);
    }
}
