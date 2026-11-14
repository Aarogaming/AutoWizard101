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

    private sealed class Runtime : IPetDanceVisitRuntime
    {
        public bool UsesGameInput => false;
        public IReadOnlyDictionary<string, string> Pins => new Dictionary<string, string>();
        public bool Countdown = true, Accept = true, SpecialistComplete = true, Released = true;
        public int Dispatches, SpecialistCalls, Cleanups;
        public List<string> Calls = [];
        private int _index;
        private double _time;
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
        { Dispatches++; Calls.Add("dispatch"); return Task.FromResult(new PetDanceVisitDispatch(plan.ProposalId!, plan.Action, _time + 1, Accept, true)); }
        public Task<PetDanceSpecialistReceipt> PlayAsync(OperationContext context, PetDanceVisitPlan plan, CancellationToken token)
        {
            SpecialistCalls++; return Task.FromResult(new PetDanceSpecialistReceipt(plan.ProposalId!, _time + 1,
            "target", new(1282, 760), "visit", PetDanceArena.WizardCity, true, SpecialistComplete, true));
        }
        public Task<PetDanceVisitCleanup> FinishAsync(OperationContext context, bool idleAlreadyVerified, CancellationToken token)
        { Cleanups++; return Task.FromResult(new PetDanceVisitCleanup(idleAlreadyVerified, Released, "fixture cleanup")); }
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
}
