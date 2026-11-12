using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101QuestConversationOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "quest-operation-" + Guid.NewGuid().ToString("N"));
    private string Locks => Path.Combine(_root, "locks");
    private readonly WizwalkerLaunch _launch;
    private readonly WizwalkerTarget _target;
    private readonly string _selection;
    public W101QuestConversationOperationTests()
    {
        Directory.CreateDirectory(_root);
        string File(string name) { var path = Path.Combine(_root, name); System.IO.File.WriteAllText(path, "inert"); return path; }
        _launch = new(File("python.exe"), File("w101_ui_tree.py"), _root);
        File("wizwalker_adapter.py"); var image = File("WizardGraphicalClient.exe");
        _target = W101QuestConversationTests.Target with { ImagePath = image, ImageSha256 = GameExperiments.Digest(image) };
        _selection = Path.Combine(_root, "selection.json");
        GameExperiments.WriteAtomic(_selection, W101QuestConversationTests.Selected);
    }

    [Theory]
    [InlineData("success", 3)]
    [InlineData("unchanged", 1)]
    [InlineData("takeover", 0)]
    [InlineData("wrong-opening", 0)]
    public async Task ScheduledConversationUsesOneOwnerAndActualOutcomes(string mode, int expectedPresses)
    {
        double time = 10000; int presses = 0; bool taken = false;
        var runtime = new W101QuestConversationRuntime(_ => true, () => 42, (_, _) => !taken,
            (context, target, _, index, _) =>
            {
                Assert.Same(context.Input, GameInputLease.Current);
                Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competitor", Locks));
                Assert.Contains(_selection, context.Request.Pins.Keys);
                time += 3000; if (mode == "takeover") taken = true;
                var page = mode == "unchanged" || index == 0 ? W101QuestConversationTests.Selected.DialogueBefore : index < 3 ? "Page " + index : null;
                if (mode == "wrong-opening") page = "Unselected conversation";
                var read = W101QuestConversationTests.Read(time, page, page == null ? "Next objective" : null) with { Target = target };
                return Task.FromResult(read);
            }, (_, release) => { if (!release) presses++; return true; }, () => time, () => W101QuestConversationTests.Stamp(time));
        await using var scheduler = new OperationScheduler(_root, Locks);
        var receipt = await W101QuestConversationOperation.Submit(scheduler, new(_target, true), _launch, _selection, true, runtime).Completion;
        Assert.Equal(expectedPresses, presses);
        Assert.Equal(mode == "success" ? OperationState.Completed : OperationState.Failed, receipt.State);
        Assert.All(receipt.Artifacts, path => Assert.True(System.IO.File.Exists(path)));
        using var next = GameInputLease.Acquire("next", Locks); Assert.True(next.CanDispatch);
    }

    [Fact]
    public async Task CardPageLimitCapsSelectedFileAndPinsOriginalInput()
    {
        double time = 10000; int presses = 0;
        GameExperiments.WriteAtomic(_selection, W101QuestConversationTests.Selected with { MaxPages = 12 });
        var runtime = new W101QuestConversationRuntime(_ => true, () => 42, (_, _) => true,
            (context, target, _, index, _) =>
            {
                Assert.Contains(_selection, context.Request.Pins.Keys); time += 3000;
                return Task.FromResult(W101QuestConversationTests.Read(time, index == 0 ? W101QuestConversationTests.Selected.DialogueBefore : "Next page") with { Target = target });
            }, (_, release) => { if (!release) presses++; return true; }, () => time, () => W101QuestConversationTests.Stamp(time));
        await using var scheduler = new OperationScheduler(_root, Locks);
        var prepared = W101QuestConversationOperation.Prepare(new(_target, true), _launch, _selection, true, runtime, maximumPages: 1);
        var receipt = await scheduler.Submit(prepared.Request, prepared.Execute).Completion;
        Assert.Equal(1, presses); Assert.Equal(OperationState.Failed, receipt.State);
        Assert.Equal(12, W101QuestConversationOperation.ReadSelection(_selection).MaxPages);
    }

    [Fact]
    public async Task QuestActionsDefaultOff()
    {
        await using var scheduler = new OperationScheduler(_root, Locks);
        Assert.Throws<InvalidOperationException>(() => W101QuestConversationOperation.Submit(scheduler, new(_target, true), _launch, _selection, false));
        Assert.Empty(OperationScheduler.History(_root));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
