using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class ModuleOwnedInputTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "module-input-tests-" + Guid.NewGuid().ToString("N"));
    private static OperationModuleBundle Bundle() => W101ModulePrograms.NativeIdle();
    private static RoutineBinding Binding() => ModuleScreenSessionTests.Binding(Bundle());
    private static ModuleScreenDecision Proposal(string id = "one")
    {
        var image = ModuleScreenSessionTests.Evidence("graphics-menu", 100, Binding());
        // Deliberate test double for input guards. No local OCR or OS transport runs.
        image = image with { Frame = image.Frame with { TextOrigin = "local-windows-ocr" } };
        var action = W101ModulePrograms.Policy().Actions[^1];
        return new(new("idle", "menu", "action-proposed", id, action.ActionId, action), image, true);
    }

    [Fact]
    public async Task ExistingSchedulerLeaseGuardsThePreparedInputAdapterAndDuplicateReceipt()
    {
        await using var scheduler = new OperationScheduler(_root, _root); var calls = 0;
        var ticket = ModuleOperation.SubmitOwned(scheduler, Bundle(), Binding(), async (context, token) =>
        {
            var input = new ModuleOwnedInput(context, Binding(), ModuleScreenSessionTests.Geometry, () => true, () => 200,
                (_, guard, _) => { Assert.True(guard()); calls++; return Task.FromResult(new PetDanceMenuReceipt(true, true, "test-only")); });
            var first = await input.DispatchAsync(Proposal(), token); Assert.True(first.Accepted); Assert.True(first.InputReleased);
            Assert.False((await input.DispatchAsync(Proposal(), token)).Accepted);
            return OperationOutcome.Completed("No OS inputs; fake transport only.");
        });
        Assert.Equal(OperationState.Completed, (await ticket.Completion).State); Assert.Equal(1, calls);
    }

    [Fact]
    public async Task AnotherTargetCannotBorrowTheAdmittedLease()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var ticket = ModuleOperation.SubmitOwned(scheduler, Bundle(), Binding(), (context, _) =>
        {
            Assert.Throws<InvalidDataException>(() => new ModuleOwnedInput(context, Binding() with { TargetId = "another" },
                ModuleScreenSessionTests.Geometry, () => true, () => 200, (_, _, _) => throw new Exception("must not dispatch")));
            return Task.FromResult(OperationOutcome.Completed("target mismatch rejected"));
        });
        Assert.Equal(OperationState.Completed, (await ticket.Completion).State);
    }

    [Fact]
    public async Task SavedPixelsFlowThroughRecognitionModulesSchedulerAndOwnedFakeTransport()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var bundle = W101ModulePrograms.Postgame(); var binding = ModuleScreenSessionTests.Binding(bundle); var calls = 0;
        var ticket = ModuleOperation.SubmitOwned(scheduler, bundle, binding, async (context, token) =>
        {
            var session = new ModuleScreenSession(bundle, binding, ModuleScreenSessionTests.Geometry, W101ModulePrograms.Policy(), "combined");
            double now = 0;
            var input = new ModuleOwnedInput(context, binding, ModuleScreenSessionTests.Geometry, () => true, () => now,
                (_, guard, _) => { Assert.True(guard()); calls++; now += 10; return Task.FromResult(new PetDanceMenuReceipt(true, true, "fake-transport-only")); });
            string[] names = ["reward", "snack", "outside", "graphics-menu", "native-idle"];
            for (var i = 0; i < names.Length; i++)
                for (var sample = 0; sample < 2; sample++)
                {
                    var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Modules", "W101Menus", names[i] + ".png");
                    var bytes = File.ReadAllBytes(path); var captured = i * 1000 + sample * 500;
                    // Original pixels, freshly computed OCR, synthetic source clock
                    // and eligibility; this sink never calls a Windows input API.
                    var recognition = await W101MenuRecognition.ReadPngAsync(bytes,
                        new(RoutineReplay.Digest(bytes), captured, ModuleScreenSessionTests.Geometry, binding, "local-windows-ocr"), token);
                    now = captured + 20;
                    var proposal = session.Observe(recognition, now, ModuleScreenSessionTests.Authority);
                    if (proposal.Decision.IsProposal) session.RecordReceipt(await input.DispatchAsync(proposal, token));
                }
            Assert.True(session.Status.Completed); Assert.Equal(4, calls);
            return OperationOutcome.Completed("Saved-image contract complete; four fake receipts, zero OS input.");
        });
        Assert.Equal(OperationState.Completed, (await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(20))).State);
        Assert.Equal(4, calls);
    }

    [Theory]
    [InlineData("no-lease")]
    [InlineData("stale")]
    [InlineData("saved-ocr")]
    [InlineData("wrong-control")]
    [InlineData("not-ready")]
    public async Task GuardFailuresNeverReachTransport(string failure)
    {
        await using var scheduler = new OperationScheduler(_root, _root); var calls = 0;
        var ticket = ModuleOperation.SubmitOwned(scheduler, Bundle(), Binding(), async (context, token) =>
        {
            var proposal = Proposal();
            if (failure == "saved-ocr") proposal = proposal with { Recognition = proposal.Recognition with { Frame = proposal.Recognition.Frame with { TextOrigin = "saved-ocr" } } };
            if (failure == "wrong-control") proposal = proposal with { Recognition = proposal.Recognition with { Controls = [] } };
            var input = new ModuleOwnedInput(failure == "no-lease" ? context with { Input = null } : context, Binding(),
                ModuleScreenSessionTests.Geometry, () => failure != "not-ready", () => failure == "stale" ? 1000 : 200,
                (_, _, _) => { calls++; return Task.FromResult(new PetDanceMenuReceipt(true, true, "bad")); });
            Assert.False((await input.DispatchAsync(proposal, token)).Accepted);
            return OperationOutcome.Completed("guard rejected");
        });
        Assert.Equal(OperationState.Completed, (await ticket.Completion).State); Assert.Equal(0, calls);
    }

    [Fact]
    public async Task OneLeaseCannotDispatchConcurrentActions()
    {
        await using var scheduler = new OperationScheduler(_root, _root); var calls = 0;
        var ticket = ModuleOperation.SubmitOwned(scheduler, Bundle(), Binding(), async (context, token) =>
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var input = new ModuleOwnedInput(context, Binding(), ModuleScreenSessionTests.Geometry, () => true, () => 200,
                async (_, _, _) => { calls++; entered.SetResult(); await release.Task; return new(true, true, "fake release"); });
            var first = input.DispatchAsync(Proposal(), token); await entered.Task;
            Assert.False((await input.DispatchAsync(Proposal("two"), token)).Accepted);
            release.SetResult(); Assert.True((await first).Accepted);
            return OperationOutcome.Completed("serialized fake input");
        });
        Assert.Equal(OperationState.Completed, (await ticket.Completion).State); Assert.Equal(1, calls);
    }

    [Fact]
    public async Task UnconfirmedTransportExceptionRequiresRecoveryInsteadOfClaimingRelease()
    {
        await using var scheduler = new OperationScheduler(_root, _root);
        var ticket = ModuleOperation.SubmitOwned(scheduler, Bundle(), Binding(), async (context, token) =>
        {
            var input = new ModuleOwnedInput(context, Binding(), ModuleScreenSessionTests.Geometry, () => true, () => 200,
                (_, _, _) => throw new IOException("simulated transport failure"));
            var receipt = await input.DispatchAsync(Proposal(), token);
            Assert.False(receipt.Accepted); Assert.False(receipt.InputReleased);
            return new(OperationState.CleanupFailed, "simulated unconfirmed release", []);
        });
        Assert.Equal(OperationState.CleanupFailed, (await ticket.Completion).State);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("retry", _root));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
