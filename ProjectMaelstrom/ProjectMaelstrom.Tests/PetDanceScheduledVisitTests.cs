using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceScheduledVisitTests
{
    [Theory]
    [InlineData(true, true, "five-results-observed", true)]
    [InlineData(false, true, "five-results-observed", false)]
    [InlineData(true, false, "five-results-observed", false)]
    [InlineData(true, true, "capture-timeout", false)]
    public void SpecialistHandoffRequiresEndedGameAndCleanup(bool cleanup, bool release, string reason, bool complete)
    {
        var options = new PetDanceVisitOptions("target", new(1282, 760), "visit", PetDanceArena.WizardCity, true);
        var plan = new PetDanceVisitPlan(PetDanceLifecycleAction.TeacherHandoff, true, "handoff", "proposal", Arena: options.Arena);
        var result = new PetDanceSpecialistResult("report", cleanup, release, 0, true, reason, 5, 0);
        var receipt = PetDanceScheduledVisit.SpecialistReceipt(options, plan, result, 500);
        Assert.Equal(complete, receipt.Completed);
        Assert.Equal(release, receipt.InputReleased);
        Assert.Equal("visit", receipt.ContextId);
        Assert.False(result.GameVictoryVerified);
        Assert.Throws<InvalidOperationException>(() => PetDanceScheduledVisit.SpecialistReceipt(options,
            plan with { Arena = PetDanceArena.Krokotopia }, result, 500));
    }

    private class Runtime : IPetDanceVisitRuntime
    {
        public bool UsesGameInput => false;
        public IReadOnlyDictionary<string, string> Pins => new Dictionary<string, string>();
        public bool Countdown = true, Accept = true, SpecialistComplete = true, Released = true;
        public int Dispatches, SpecialistCalls, Cleanups;
        public List<string> Calls = [];
        public List<PetDanceLifecycleAction> DispatchedActions = [];
        private int _index;
        protected double _time;
        private static readonly PetDanceScene[] Scenes = [PetDanceScene.DanceSigil,
            PetDanceScene.InstructionsUnselected, PetDanceScene.InstructionsReady, PetDanceScene.Watch,
            PetDanceScene.GameResult, PetDanceScene.SnackOffer, PetDanceScene.OutsideAfterGame,
            PetDanceScene.MenuObserved, PetDanceScene.NativeIdle];
        public Task<bool> CountdownAsync(OperationContext context, CancellationToken token)
        { Calls.Add("countdown"); return Task.FromResult(Countdown); }
        public Task<PetDanceVisitObservation> ObserveAsync(OperationContext context, CancellationToken token)
        {
            Calls.Add("observe"); _time += 100;
            var scene = Scenes[Math.Min(_index++, Scenes.Length - 1)];
            var box = new RectangleF(400, 400, 60, 40);
            var entry = scene is PetDanceScene.DanceSigil or PetDanceScene.InstructionsUnselected or PetDanceScene.InstructionsReady
                ? new PetDanceEntryScene(scene, new('a', 64), _time, new(1282, 760), "synthetic", box,
                    scene == PetDanceScene.InstructionsReady ? PetDanceArena.WizardCity : null,
                    [new(PetDanceArena.WizardCity, box, true)],
                    SelectionVerified: scene == PetDanceScene.InstructionsReady, PlayEnabledVerified: scene == PetDanceScene.InstructionsReady) : null;
            return Task.FromResult(new PetDanceVisitObservation(_time, new('a', 64), scene, "target", new(1282, 760), "visit",
                entry, box, Fresh: true, TargetReady: true, ContextVerified: true, InputOwned: true,
                EntryCostApproved: true, WatchStartVerified: true, SpecialistQualified: true, WholeGameResultVerified: true));
        }
        public Task<PetDanceVisitDispatch> DispatchAsync(OperationContext context, PetDanceVisitPlan plan, CancellationToken token)
        { Dispatches++; DispatchedActions.Add(plan.Action); Calls.Add("dispatch"); return Task.FromResult(new PetDanceVisitDispatch(plan.ProposalId!, plan.Action, _time + 1, Accept, true)); }
        public Task<PetDanceSpecialistReceipt> PlayAsync(OperationContext context, PetDanceVisitPlan plan, CancellationToken token)
        {
            SpecialistCalls++; return Task.FromResult(new PetDanceSpecialistReceipt(plan.ProposalId!, _time + 1,
            "target", new(1282, 760), "visit", PetDanceArena.WizardCity, true, SpecialistComplete, true));
        }
        public Task<PetDanceVisitCleanup> FinishAsync(OperationContext context, bool idleAlreadyVerified, CancellationToken token)
        { Cleanups++; return Task.FromResult(new PetDanceVisitCleanup(idleAlreadyVerified, Released, "fixture cleanup")); }
    }

    private sealed class PrimedRuntime(string mode) : Runtime, IPrimedPetDanceVisitRuntime
    {
        public int CompositeCalls, CallbackCalls;
        public async Task<PetDanceSpecialistReceipt> PlayPrimedAsync(OperationContext context, PetDanceVisitPlan playPlan,
            Func<PetDanceVisitDispatch, PetDanceVisitObservation, PetDanceVisitPlan> bindHandoff, CancellationToken token)
        {
            CompositeCalls++;
            Calls.Add("prime-capture");
            var dispatch = new PetDanceVisitDispatch(playPlan.ProposalId!, PetDanceLifecycleAction.Play,
                _time + 1, true, mode != "unreleased-play");
            Calls.Add("play-released");
            var watch = await ObserveAsync(context, token);
            watch = mode switch
            {
                "wrong-target" => watch with { TargetId = "other" },
                "stale-watch" => watch with { Fresh = false },
                "wrong-context" => watch with { ContextId = "other-visit" },
                "wrong-geometry" => watch with { Geometry = new(1280, 720) },
                "focus-paused" => watch with { FocusPaused = true },
                "user-takeover" => watch with { UserActive = true },
                "unowned-input" => watch with { InputOwned = false },
                "invalid-image" => watch with { ImageSha256 = "not-evidence" },
                "early-watch" => watch with { TimeMs = dispatch.ReleasedMs },
                "wrong-scene" => watch with { Scene = PetDanceScene.Go },
                "unverified-onset" => watch with { WatchStartVerified = false },
                _ => watch
            };
            if (mode == "missing-callback")
                return new("invented", _time + 1, "target", new(1282, 760), "visit", PetDanceArena.WizardCity, true, true, true);
            CallbackCalls++;
            var handoff = bindHandoff(dispatch, watch);
            Calls.Add("handoff-bound");
            if (mode == "duplicate")
            {
                CallbackCalls++;
                // Even swallowing the rejected duplicate cannot promote success.
                Assert.Throws<InvalidOperationException>(() => bindHandoff(dispatch, watch));
            }
            return new(mode == "wrong-proposal" ? "invented" : handoff.ProposalId!, _time + 1,
                "target", new(1282, 760), "visit", PetDanceArena.WizardCity, true, mode != "incomplete", true);
        }
    }

    [Theory]
    [InlineData("complete", "Completed")]
    [InlineData("wrong-target", "Failed")]
    [InlineData("stale-watch", "Failed")]
    [InlineData("wrong-context", "Failed")]
    [InlineData("wrong-geometry", "Failed")]
    [InlineData("focus-paused", "Failed")]
    [InlineData("user-takeover", "Failed")]
    [InlineData("unowned-input", "Failed")]
    [InlineData("invalid-image", "Failed")]
    [InlineData("early-watch", "Failed")]
    [InlineData("wrong-scene", "Failed")]
    [InlineData("unverified-onset", "Failed")]
    [InlineData("unreleased-play", "Failed")]
    [InlineData("duplicate", "Failed")]
    [InlineData("missing-callback", "Failed")]
    [InlineData("wrong-proposal", "Failed")]
    [InlineData("incomplete", "Failed")]
    [InlineData("cleanup", "CleanupFailed")]
    public async Task PrimedPlayBindsRealEntryBeforeOneSpecialistAndPreservesCleanup(string mode, string expected)
    {
        var folder = Path.Combine(Path.GetTempPath(), "dance-primed-scheduler-" + Guid.NewGuid().ToString("N"));
        try
        {
            var runtime = new PrimedRuntime(mode) { Released = mode != "cleanup" };
            var job = PetDanceScheduledVisit.Prepare(new("target", new(1282, 760), "visit", PetDanceArena.WizardCity, true), runtime);
            await using (var scheduler = new OperationScheduler(folder))
            {
                var receipt = await scheduler.Submit(job.Request, job.Execute).Completion;
                Assert.Equal(expected, receipt.State.ToString());
            }
            Assert.Equal(1, runtime.CompositeCalls);
            Assert.Equal(0, runtime.SpecialistCalls);
            Assert.DoesNotContain(PetDanceLifecycleAction.Play, runtime.DispatchedActions);
            Assert.Equal(1, runtime.Cleanups);
            Assert.True(runtime.Calls.IndexOf("prime-capture") < runtime.Calls.IndexOf("play-released"));
            if (mode == "complete")
            {
                Assert.Equal(1, runtime.CallbackCalls);
                Assert.Equal(6, runtime.Dispatches);
                Assert.True(runtime.Calls.IndexOf("play-released") < runtime.Calls.IndexOf("handoff-bound"));
            }
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public void NoRuntimeOrUnreviewedSnackPolicyCannotExecute()
    {
        var routine = JobDeckRoutines.Create().Single(r => r.Info.Id == "w101.pet.dance");
        Assert.False(routine.Info.Available);
        var card = new JobCardSpec("dance", "w101.pet.dance", "Dance");
        Assert.Throws<InvalidOperationException>(() => routine.Prepare(card, null));
        Assert.Throws<InvalidDataException>(() => routine.Validate(card with { Settings = new() { ["snacks"] = "feed" } }));
        Assert.Throws<InvalidDataException>(() => routine.Validate(card with { Settings = new() { ["level"] = "0" } }));
    }
    [Theory]
    [InlineData("complete", "Completed")]
    [InlineData("countdown", "Failed")]
    [InlineData("rejected", "Failed")]
    [InlineData("specialist", "Failed")]
    [InlineData("cleanup", "CleanupFailed")]
    public async Task SharedSchedulerRequiresWholeVisitAndCleanup(string mode, string expected)
    {
        var folder = Path.Combine(Path.GetTempPath(), "dance-scheduler-" + Guid.NewGuid().ToString("N"));
        try
        {
            var runtime = new Runtime
            {
                Countdown = mode != "countdown",
                Accept = mode != "rejected",
                SpecialistComplete = mode != "specialist",
                Released = mode != "cleanup"
            };
            var job = PetDanceScheduledVisit.Prepare(new("target", new(1282, 760), "visit", PetDanceArena.WizardCity, true), runtime);
            var routine = JobDeckRoutines.Create(dance: (_, _) => job).Single(r => r.Info.Id == "w101.pet.dance");
            Assert.True(routine.Info.Available);
            job = routine.Prepare(new("dance", "w101.pet.dance", "Dance"), null);
            await using (var scheduler = new OperationScheduler(folder))
            {
                var receipt = await scheduler.Submit(job.Request, job.Execute).Completion;
                Assert.Equal(expected, receipt.State.ToString());
                Assert.Single(receipt.Artifacts);
                Assert.Contains("\"live_qualified\": false", File.ReadAllText(receipt.Artifacts[0]));
            }
            Assert.Equal("countdown", runtime.Calls[0]);
            Assert.Equal(1, runtime.Cleanups);
            if (mode == "countdown") Assert.Equal(0, runtime.Dispatches);
            if (mode == "complete") { Assert.Equal(7, runtime.Dispatches); Assert.Equal(1, runtime.SpecialistCalls); }
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public void PrepareProductionBindsSeamAndPreservesInvariants()
    {
        var target = new GameWindow(5678, 1234, DateTime.UtcNow.Ticks, "WizardGraphicalClient", "Wizard101");
        var options = new PetDanceVisitOptions("target-id", new(1282, 760), "context-id", PetDanceArena.WizardCity, Enabled: true);
        var job = PetDanceScheduledVisit.PrepareProduction(options, target, "profile.json", "output-folder");
        Assert.NotNull(job);
        Assert.Equal("w101.pet-dance-visit", job.Request.Operation);
        Assert.Equal("target-id", job.Request.Target);
        Assert.True(job.Request.WritesGame);
        Assert.Equal("1282x760", job.Request.Pins["geometry"]);
        Assert.Equal("unwired", job.Request.Pins["entry_cost_policy"]);
    }
}
