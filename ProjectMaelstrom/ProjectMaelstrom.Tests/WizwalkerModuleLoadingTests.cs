using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class WizwalkerModuleLoadingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wizwalker-modules-" + Guid.NewGuid().ToString("N"));
    private readonly WizwalkerLaunch _launch;
    private readonly WizwalkerTarget _target;
    private string Locks => Path.Combine(_root, "locks");
    public WizwalkerModuleLoadingTests()
    {
        Directory.CreateDirectory(_root);
        string Fixture(string name) { var path = Path.Combine(_root, name); File.WriteAllText(path, "inert fixture"); return path; }
        _launch = new(Fixture("python.exe"), Fixture("worker.py"), _root);
        var image = Fixture("WizardGraphicalClient.exe");
        _target = new(42, 123, 100, "WizardGraphicalClient", image, GameExperiments.Digest(image));
    }
    private static WizwalkerResult Result(WizwalkerRequest request, bool loading) =>
        new(1, request.RunId, request.Operation, request.SourceSha256, request.Target, "observed", "loading-state-observed",
            loading, DateTime.UtcNow.Ticks, 1, true);
    private OperationRequest Request(WizwalkerRuntime runtime) =>
        WizwalkerOperation.Prepare(new(_target, true), _launch, runtime, "test-module", "v1") with { Target = ModuleOwnedInput.TargetId(_target.Window) };

    [Fact]
    public async Task WorkerResultsDriveSharedMenusUnderSameOwnerAndPreserveReceiptsAcrossLoading()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        double now = 0; int calls = 0, dispatches = 0;
        var states = new[] { true, false, false, true, false, false };
        var runtime = new WizwalkerRuntime(_ => true, (_, request, context, _) =>
        {
            Assert.Same(context.Input, GameInputLease.Current);
            Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competitor", Locks));
            Assert.EndsWith("module-loading-" + calls.ToString("D3"), context.Directory);
            return Task.FromResult(Result(request, states[calls++]));
        });
        var receipt = await scheduler.Submit(Request(runtime), async (context, token) =>
        {
            var bundle = W101ModulePrograms.NativeIdle();
            var binding = bundle.Bind(context.Request.Target, "1282x760", context.Request.Id);
            var provider = new WizwalkerModuleLoading(context, new(_target, true), _launch, binding, runtime, () => now);
            var screen = new ModuleScreenSession(bundle, binding, ModuleScreenSessionTests.Geometry, W101ModulePrograms.Policy(),
                loadingRequirement: WizwalkerModuleLoading.Requirement);
            var input = new ModuleOwnedInput(context, binding, ModuleScreenSessionTests.Geometry, () => true, () => now,
                (_, guard, _) => { Assert.True(guard()); dispatches++; return Task.FromResult(new PetDanceMenuReceipt(true, true, "fake-UI")); });
            for (int i = 0; i < states.Length; i++)
            {
                now = i * 500;
                var sample = await provider.ReadAsync(token);
                var image = ModuleScreenSessionTests.Evidence(i < 3 ? "graphics-menu" : "native-idle", now, binding);
                image = image with { Frame = image.Frame with { TextOrigin = "local-windows-ocr" } };
                now += 20;
                var decision = screen.Observe(image, now, ModuleScreenSessionTests.Authority, sample);
                if (decision.Decision.IsProposal) screen.RecordReceipt(await input.DispatchAsync(decision, token));
                if (i < 5) Assert.False(decision.Decision.Completed);
            }
            Assert.True(screen.Status.Completed); Assert.Equal(1, dispatches);
            return OperationOutcome.Completed("Synthetic client readings, saved OCR, fake UI; zero real game input.");
        }).Completion;
        Assert.Equal(OperationState.Completed, receipt.State); Assert.Equal(6, calls);
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("pins")]
    [InlineData("target")]
    [InlineData("visit")]
    public async Task RejectsUnadmittedAttachment(string fault)
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        int calls = 0;
        var runtime = new WizwalkerRuntime(_ => true, (_, request, _, _) => { calls++; return Task.FromResult(Result(request, false)); });
        var receipt = await scheduler.Submit(Request(runtime), (context, _) =>
        {
            var binding = W101ModulePrograms.NativeIdle().Bind(context.Request.Target, "1282x760", context.Request.Id);
            if (fault == "pins") context = context with { Request = context.Request with { Pins = new Dictionary<string, string>() } };
            if (fault == "target") binding = binding with { TargetId = "other" };
            if (fault == "visit") binding = binding with { ContextId = binding.ContextId + "other" };
            Assert.Throws<InvalidOperationException>(() => new WizwalkerModuleLoading(context, new(_target, fault != "disabled"), _launch, binding, runtime));
            return Task.FromResult(OperationOutcome.Completed("rejected"));
        }).Completion;
        Assert.Equal(OperationState.Completed, receipt.State); Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("old")]
    [InlineData("slow")]
    [InlineData("cleanup")]
    public async Task FailedOrDelayedReadingDoesNotBecomeClear(string fault)
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        double now = 0;
        var runtime = new WizwalkerRuntime(_ => true, (_, request, _, _) =>
        {
            var result = Result(request, false);
            if (fault == "unavailable") result = result with { Status = "unavailable", Loading = null };
            if (fault == "old") result = result with { ObservedUtcTicks = DateTime.UtcNow.AddSeconds(-2).Ticks };
            if (fault == "slow") now = 1001;
            if (fault == "cleanup") result = result with { CleanupConfirmed = false };
            return Task.FromResult(result);
        });
        var receipt = await scheduler.Submit(Request(runtime), async (context, token) =>
        {
            var binding = W101ModulePrograms.NativeIdle().Bind(context.Request.Target, "1282x760", context.Request.Id);
            var provider = new WizwalkerModuleLoading(context, new(_target, true), _launch, binding, runtime, () => now);
            Assert.Null((await provider.ReadAsync(token)).Loading);
            return OperationOutcome.Completed("No clear state invented.");
        }).Completion;
        Assert.Equal(fault == "cleanup" ? OperationState.CleanupFailed : OperationState.Completed, receipt.State);
    }

    [Fact]
    public async Task ReadingBudgetAndCadenceNeverReturnCachedClear()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        double now = 0; int calls = 0;
        var runtime = new WizwalkerRuntime(_ => true, (_, request, _, _) => { calls++; return Task.FromResult(Result(request, false)); });
        var receipt = await scheduler.Submit(Request(runtime), async (context, token) =>
        {
            var binding = W101ModulePrograms.NativeIdle().Bind(context.Request.Target, "1282x760", context.Request.Id);
            var provider = new WizwalkerModuleLoading(context, new(_target, true), _launch, binding, runtime, () => now);
            for (int i = 0; i < 32; i++)
            {
                now = i * 500;
                Assert.Equal(false, (await provider.ReadAsync(token)).Loading);
                Assert.Null((await provider.ReadAsync(token)).Loading);
            }
            now = 16000; Assert.Null((await provider.ReadAsync(token)).Loading);
            return OperationOutcome.Completed("budget bounded");
        }).Completion;
        Assert.Equal(OperationState.Completed, receipt.State); Assert.Equal(32, calls);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
