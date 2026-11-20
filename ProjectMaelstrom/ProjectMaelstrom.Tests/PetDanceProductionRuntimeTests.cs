using System.Drawing;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceProductionRuntimeTests
{
    private static GameWindow CreateFakeWindow(int pid = 1234, nint handle = 5678) =>
        new(handle, pid, DateTime.UtcNow.Ticks, "WizardGraphicalClient", "Wizard101");

    private static PetDanceVisitOptions CreateValidOptions() =>
        new("target-pid", new(1282, 760), "test-visit-context", PetDanceArena.WizardCity, Enabled: true);

    private static OperationContext CreateFakeContext(string targetId = "target-pid", bool canDispatch = true)
    {
        var request = new OperationRequest("req-1", "w101.pet.dance", "v1", 60, true, targetId, new Dictionary<string, string>());
        return new(request, "F:\\dummy-dir", null);
    }

    [Fact]
    public void ContractConformanceAndPins()
    {
        var target = CreateFakeWindow();
        var options = CreateValidOptions();
        using var runtime = new PetDanceProductionRuntime(
            target, options, "profile.json", "output-dir",
            inputTag: 9999,
            levelVision: null, playVision: null, menuInput: null,
            monitor: null, controlPermission: null, audioComparisonPath: null,
            readFrame: null, readOcr: null, runSpecialist: null,
            isTargetCurrent: _ => true,
            getClientSize: _ => new(1282, 760),
            clock: () => 1000);

        Assert.True(runtime.UsesGameInput);
        Assert.IsAssignableFrom<IPetDanceVisitRuntime>(runtime);
        Assert.IsAssignableFrom<IPrimedPetDanceVisitRuntime>(runtime);
        Assert.Equal("entry-cost-policy-unwired", runtime.EntryCostPolicyReason);

        Assert.Equal("target-pid", runtime.Pins["target"]);
        Assert.Equal("1282x760", runtime.Pins["geometry"]);
        Assert.Equal("WizardCity", runtime.Pins["arena"]);
        Assert.Equal("unwired", runtime.Pins["entry_cost_policy"]);
    }

    [Fact]
    public async Task FailClosedGeometryRejectionOnOptions()
    {
        var target = CreateFakeWindow();
        var invalidOptions = new PetDanceVisitOptions("target-pid", new(1280, 720), "context", PetDanceArena.WizardCity, true);
        using var runtime = new PetDanceProductionRuntime(
            target, invalidOptions, "profile.json", "output-dir",
            inputTag: 9999,
            levelVision: null, playVision: null, menuInput: null,
            monitor: null, controlPermission: null, audioComparisonPath: null,
            readFrame: null, readOcr: null, runSpecialist: null,
            isTargetCurrent: _ => true,
            getClientSize: _ => new(1280, 720),
            clock: () => 1000);

        var context = CreateFakeContext();

        Assert.False(await runtime.CountdownAsync(context, default));

        var obs = await runtime.ObserveAsync(context, default);
        Assert.False(obs.Fresh);
        Assert.False(obs.TargetReady);
        Assert.NotNull(obs.Entry);
        Assert.Equal("geometry-unqualified", obs.Entry!.Reason);

        var plan = new PetDanceVisitPlan(PetDanceLifecycleAction.ChooseLevel, true, "choose", "p-1", new RectangleF(452, 485, 59, 55));
        var dispatch = await runtime.DispatchAsync(context, plan, default);
        Assert.False(dispatch.Accepted);
        Assert.True(dispatch.InputReleased);

        var playPlan = new PetDanceVisitPlan(PetDanceLifecycleAction.Play, true, "play", "p-2", new RectangleF(500, 500, 100, 40));
        var exPrimed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.PlayPrimedAsync(context, playPlan, (d, w) => playPlan, default));
        Assert.Equal("geometry-unqualified", exPrimed.Message);

        var exPlay = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.PlayAsync(context, playPlan, default));
        Assert.Equal("geometry-unqualified", exPlay.Message);
    }

    [Fact]
    public async Task FailClosedGeometryRejectionOnObservedWindowOrFrame()
    {
        var target = CreateFakeWindow();
        var options = CreateValidOptions();
        using var bitmap = new Bitmap(1280, 720);
        using var frame = new CapturedWindowFrame(bitmap, TimeSpan.FromMilliseconds(1000));

        using var runtime = new PetDanceProductionRuntime(
            target, options, "profile.json", "output-dir",
            inputTag: 9999,
            levelVision: null, playVision: null, menuInput: null,
            monitor: null, controlPermission: null, audioComparisonPath: null,
            readFrame: () => Task.FromResult<CapturedWindowFrame?>(new CapturedWindowFrame(new Bitmap(1280, 720), TimeSpan.FromMilliseconds(1000))),
            readOcr: null, runSpecialist: null,
            isTargetCurrent: _ => true,
            getClientSize: _ => new(1282, 760),
            clock: () => 1050);

        var context = CreateFakeContext();

        var obs = await runtime.ObserveAsync(context, default);
        Assert.False(obs.Fresh);
        Assert.False(obs.TargetReady);
        Assert.NotNull(obs.Entry);
        Assert.Equal("geometry-unqualified", obs.Entry!.Reason);
    }

    [Fact]
    public async Task FailClosedArenaRejection()
    {
        var target = CreateFakeWindow();
        var invalidArenaOptions = new PetDanceVisitOptions("target-pid", new(1282, 760), "context", PetDanceArena.Krokotopia, true);
        using var runtime = new PetDanceProductionRuntime(
            target, invalidArenaOptions, "profile.json", "output-dir",
            inputTag: 9999,
            levelVision: null, playVision: null, menuInput: null,
            monitor: null, controlPermission: null, audioComparisonPath: null,
            readFrame: null, readOcr: null, runSpecialist: null,
            isTargetCurrent: _ => true,
            getClientSize: _ => new(1282, 760),
            clock: () => 1000);

        var context = CreateFakeContext();

        Assert.False(await runtime.CountdownAsync(context, default));

        var obs = await runtime.ObserveAsync(context, default);
        Assert.False(obs.Fresh);
        Assert.False(obs.TargetReady);
        Assert.NotNull(obs.Entry);
        Assert.Equal("arena-unqualified", obs.Entry!.Reason);

        var playPlan = new PetDanceVisitPlan(PetDanceLifecycleAction.Play, true, "play", "p-2", new RectangleF(500, 500, 100, 40));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.PlayPrimedAsync(context, playPlan, (d, w) => playPlan, default));
        Assert.Equal("arena-unqualified", ex.Message);
    }

    [Fact]
    public async Task EnergyApprovalIsWithheldAndSurfacedInVisitMachine()
    {
        var target = CreateFakeWindow();
        var options = CreateValidOptions();
        using var bmp = new Bitmap(1282, 760);

        var text = new PetDanceMenuText(new(1282, 760),
        [
            new("DANCE", new(300, 100, 100, 30)),
            new("GAME", new(410, 100, 100, 30)),
            new("OBJECTIVE", new(300, 200, 150, 30)),
            new("SELECT", new(300, 350, 100, 30)),
            new("LEVEL", new(410, 350, 100, 30)),
            new("PLAY", new(400, 550, 120, 35))
        ]);

        using var runtime = new PetDanceProductionRuntime(
            target, options, "profile.json", "output-dir",
            inputTag: 9999,
            levelVision: null, playVision: null, menuInput: null,
            monitor: null, controlPermission: null, audioComparisonPath: null,
            readFrame: () => Task.FromResult<CapturedWindowFrame?>(new CapturedWindowFrame(new Bitmap(1282, 760), TimeSpan.FromMilliseconds(1000))),
            readOcr: (_, _) => Task.FromResult(text),
            runSpecialist: null,
            isTargetCurrent: _ => true,
            getClientSize: _ => new(1282, 760),
            clock: () => 1050);

        var context = CreateFakeContext();
        var obs = await runtime.ObserveAsync(context, default);

        Assert.True(obs.Fresh);
        Assert.True(obs.TargetReady);
        Assert.False(obs.EntryCostApproved);

        var visit = new PetDanceVisit(options);
        var plan = visit.Observe(obs);

        Assert.Equal("entry-cost-policy-unwired", runtime.EntryCostPolicyReason);
    }

    [Fact]
    public async Task PrimedPlayDelegatesToSpecialistOnceAndBindsTeacherHandoff()
    {
        var target = CreateFakeWindow();
        var options = CreateValidOptions();
        var playBounds = new RectangleF(400, 550, 100, 40);
        var playPlan = new PetDanceVisitPlan(PetDanceLifecycleAction.Play, true, "ready-to-play", "play-prop-1", playBounds, PetDanceArena.WizardCity);

        var specialistCalls = 0;
        var handoffCalls = 0;
        PetDanceVisitDispatch? capturedDispatch = null;
        PetDanceVisitObservation? capturedWatch = null;

        Task<PetDanceSpecialistResult> MockSpecialist(
            OperationContext ctx, GameWindow tgt, Size sz, string prof, string outp,
            PetDanceSpecialistControl ctrl, CancellationToken ct, string? audio, Func<bool>? perm,
            Func<CancellationToken, Task<PetDanceStartReceipt>>? startAfterCapture,
            Action<Bitmap, double>? firstWatch)
        {
            specialistCalls++;
            Assert.NotNull(startAfterCapture);
            Assert.NotNull(firstWatch);

            var startTask = startAfterCapture!(ct);
            Assert.True(startTask.IsCompletedSuccessfully);

            using var watchBmp = new Bitmap(1282, 760);
            firstWatch!(watchBmp, 2500);

            return Task.FromResult(new PetDanceSpecialistResult("report.json", true, true, 10, true, "five-results-observed", 5, 0));
        }

        using var runtime = new PetDanceProductionRuntime(
            target, options, "profile.json", "output-dir",
            inputTag: 9999,
            levelVision: null, playVision: null,
            menuInput: null,
            monitor: null, controlPermission: null, audioComparisonPath: null,
            readFrame: null, readOcr: null,
            runSpecialist: MockSpecialist,
            isTargetCurrent: _ => true,
            getClientSize: _ => new(1282, 760),
            clock: () => 2000);

        var context = CreateFakeContext();

        PetDanceVisitPlan BindHandoff(PetDanceVisitDispatch dispatch, PetDanceVisitObservation watch)
        {
            handoffCalls++;
            capturedDispatch = dispatch;
            capturedWatch = watch;
            return new PetDanceVisitPlan(PetDanceLifecycleAction.TeacherHandoff, true, "handoff", "teacher-prop-1", Arena: PetDanceArena.WizardCity);
        }

        var receipt = await runtime.PlayPrimedAsync(context, playPlan, BindHandoff, default);

        Assert.Equal(1, specialistCalls);
        Assert.Equal(1, handoffCalls);
        Assert.NotNull(capturedDispatch);
        Assert.NotNull(capturedWatch);

        Assert.Equal("play-prop-1", capturedDispatch!.ProposalId);
        Assert.Equal(PetDanceLifecycleAction.Play, capturedDispatch.Action);
        Assert.Equal(PetDanceScene.Watch, capturedWatch!.Scene);
        Assert.True(capturedWatch.WatchStartVerified);
        Assert.True(capturedWatch.SpecialistQualified);

        Assert.Equal("teacher-prop-1", receipt.ProposalId);
        Assert.True(receipt.Started);
        Assert.True(receipt.Completed);
        Assert.True(receipt.InputReleased);
    }

    [Fact]
    public async Task FinishAsyncVerifiesIdleStatusAndInputRelease()
    {
        var target = CreateFakeWindow();
        var options = CreateValidOptions();
        using var runtime = new PetDanceProductionRuntime(
            target, options, "profile.json", "output-dir",
            inputTag: 9999,
            levelVision: null, playVision: null, menuInput: null,
            monitor: null, controlPermission: null, audioComparisonPath: null,
            readFrame: null, readOcr: null, runSpecialist: null,
            isTargetCurrent: _ => true,
            getClientSize: _ => new(1282, 760),
            clock: () => 1000);

        var context = CreateFakeContext();

        var cleanupIdle = await runtime.FinishAsync(context, idleAlreadyVerified: true, default);
        Assert.True(cleanupIdle.IdleVerified);
        Assert.True(cleanupIdle.InputReleased);

        var cleanupNoIdle = await runtime.FinishAsync(context, idleAlreadyVerified: false, default);
        Assert.False(cleanupNoIdle.IdleVerified);
        Assert.True(cleanupNoIdle.InputReleased);
    }

    [Fact]
    public void ComputeSha256ProducesConsistentHexDigest()
    {
        using var bmp = new Bitmap(10, 10);
        var hash1 = PetDanceProductionRuntime.ComputeSha256(bmp);
        var hash2 = PetDanceProductionRuntime.ComputeSha256(bmp);
        Assert.Equal(hash1, hash2);
        Assert.True(PetDanceEntryVision.IsImageHash(hash1));
    }

    [Fact]
    public async Task DispatchInteractXAndMouseActionsAllowed()
    {
        var target = CreateFakeWindow();
        var options = CreateValidOptions();

        using var runtime = new PetDanceProductionRuntime(
            target, options, "profile.json", "output-dir",
            inputTag: 9999,
            levelVision: null, playVision: null,
            menuInput: null,
            monitor: null, controlPermission: null, audioComparisonPath: null,
            readFrame: null, readOcr: null, runSpecialist: null,
            isTargetCurrent: _ => true,
            getClientSize: _ => new(1282, 760),
            clock: () => 1000);

        var context = CreateFakeContext();

        var invalidPlan = new PetDanceVisitPlan(PetDanceLifecycleAction.ApproachRoute, true, "approach");
        var invalidReceipt = await runtime.DispatchAsync(context, invalidPlan, default);
        Assert.False(invalidReceipt.Accepted);
        Assert.True(invalidReceipt.InputReleased);

        var nonProp = new PetDanceVisitPlan(PetDanceLifecycleAction.InteractX, false, "wait");
        var nonPropReceipt = await runtime.DispatchAsync(context, nonProp, default);
        Assert.False(nonPropReceipt.Accepted);
        Assert.True(nonPropReceipt.InputReleased);
    }

    [Fact]
    public async Task PrimedPlayRejectsDuplicateWatchCallback()
    {
        var target = CreateFakeWindow();
        var options = CreateValidOptions();
        var playBounds = new RectangleF(400, 550, 100, 40);
        var playPlan = new PetDanceVisitPlan(PetDanceLifecycleAction.Play, true, "ready-to-play", "play-prop-1", playBounds, PetDanceArena.WizardCity);

        Task<PetDanceSpecialistResult> DuplicateWatchSpecialist(
            OperationContext ctx, GameWindow tgt, Size sz, string prof, string outp,
            PetDanceSpecialistControl ctrl, CancellationToken ct, string? audio, Func<bool>? perm,
            Func<CancellationToken, Task<PetDanceStartReceipt>>? startAfterCapture,
            Action<Bitmap, double>? firstWatch)
        {
            var startTask = startAfterCapture!(ct);
            using var watchBmp = new Bitmap(1282, 760);
            firstWatch!(watchBmp, 2500);

            Assert.Throws<InvalidOperationException>(() => firstWatch(watchBmp, 2600));

            return Task.FromResult(new PetDanceSpecialistResult("report.json", true, true, 10, true, "five-results-observed", 5, 0));
        }

        using var runtime = new PetDanceProductionRuntime(
            target, options, "profile.json", "output-dir",
            inputTag: 9999,
            levelVision: null, playVision: null, menuInput: null,
            monitor: null, controlPermission: null, audioComparisonPath: null,
            readFrame: null, readOcr: null,
            runSpecialist: DuplicateWatchSpecialist,
            isTargetCurrent: _ => true,
            getClientSize: _ => new(1282, 760),
            clock: () => 2000);

        var context = CreateFakeContext();
        var receipt = await runtime.PlayPrimedAsync(context, playPlan, (d, w) =>
            new PetDanceVisitPlan(PetDanceLifecycleAction.TeacherHandoff, true, "handoff", "teacher-prop-1", Arena: PetDanceArena.WizardCity), default);

        Assert.NotNull(receipt);
        Assert.True(receipt.Started);
    }

    [Fact]
    public async Task PrimedPlayFailsIfSpecialistCompletesWithoutWatchOnset()
    {
        var target = CreateFakeWindow();
        var options = CreateValidOptions();
        var playBounds = new RectangleF(400, 550, 100, 40);
        var playPlan = new PetDanceVisitPlan(PetDanceLifecycleAction.Play, true, "ready-to-play", "play-prop-1", playBounds, PetDanceArena.WizardCity);

        Task<PetDanceSpecialistResult> MissingWatchSpecialist(
            OperationContext ctx, GameWindow tgt, Size sz, string prof, string outp,
            PetDanceSpecialistControl ctrl, CancellationToken ct, string? audio, Func<bool>? perm,
            Func<CancellationToken, Task<PetDanceStartReceipt>>? startAfterCapture,
            Action<Bitmap, double>? firstWatch)
        {
            return Task.FromResult(new PetDanceSpecialistResult("report.json", true, true, 10, false, "capture-timeout", 0, 0));
        }

        using var runtime = new PetDanceProductionRuntime(
            target, options, "profile.json", "output-dir",
            inputTag: 9999,
            levelVision: null, playVision: null, menuInput: null,
            monitor: null, controlPermission: null, audioComparisonPath: null,
            readFrame: null, readOcr: null,
            runSpecialist: MissingWatchSpecialist,
            isTargetCurrent: _ => true,
            getClientSize: _ => new(1282, 760),
            clock: () => 2000);

        var context = CreateFakeContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.PlayPrimedAsync(context, playPlan, (d, w) =>
                new PetDanceVisitPlan(PetDanceLifecycleAction.TeacherHandoff, true, "handoff", "teacher-prop-1", Arena: PetDanceArena.WizardCity), default));

        Assert.Equal("Specialist completed without exactly one bound Watch handoff.", ex.Message);
    }

    [Fact]
    public void PrepareProductionCreatesValidPreparedDeckJob()
    {
        var target = CreateFakeWindow();
        var options = CreateValidOptions();
        var job = PetDanceScheduledVisit.PrepareProduction(options, target, "profile.json", "output-dir");
        Assert.NotNull(job);
        Assert.Equal("w101.pet-dance-visit", job.Request.Operation);
        Assert.Equal("target-pid", job.Request.Target);
        Assert.True(job.Request.WritesGame);
        Assert.Equal("1282x760", job.Request.Pins["geometry"]);
    }
}
