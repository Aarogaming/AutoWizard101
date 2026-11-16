using ProjectMaelstrom.Utilities;
namespace ProjectMaelstrom.Tests;

public sealed class W101WorldGateVisitTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public async Task ReleasedCheckpointRechecksTargetAndOwnerAfterFocusWait(bool targetReady, bool recoveryDuringWait, bool expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "gate-checkpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var lease = GameInputLease.Acquire("test-gate", directory);
            using var scope = GameInputLease.EnterScope(lease);
            var request = new OperationRequest(Guid.NewGuid().ToString("N"), "test.gate", "1", 30, true, "fixture", new Dictionary<string, string>());
            var context = new OperationContext(request, directory, lease);
            var ready = false;
            var waits = 0;
            var result = await W101WorldGateVisit.ReleasedCheckpointAsync(context, () => ready, _ =>
            {
                waits++;
                ready = targetReady;
                if (recoveryDuringWait) lease.RequireRecovery("uncertain cleanup during pause");
                return Task.FromResult(true);
            }, default);
            Assert.Equal(1, waits);
            Assert.Equal(expected, result);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(true, false, 1)]
    [InlineData(false, false, 0)]
    [InlineData(true, true, 0)]
    public async Task GateCleanupWaitsForFocusWithoutReplayingTravel(bool focusReturns, bool recovery, int expectedAttempts)
    {
        var directory = Path.Combine(Path.GetTempPath(), "gate-focus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var lease = GameInputLease.Acquire("test-gate", directory);
            using var scope = GameInputLease.EnterScope(lease);
            if (recovery) lease.RequireRecovery("synthetic uncertainty");
            var request = new OperationRequest(Guid.NewGuid().ToString("N"), "test.gate", "1", 30, true, "fixture", new Dictionary<string, string>());
            var context = new OperationContext(request, directory, lease);
            var ready = false; var attempts = 0; var waits = 0;
            var result = await W101WorldGateVisit.FinalizeIdleAsync(context, () => ready,
                _ => { waits++; ready = focusReturns; return Task.FromResult(focusReturns); },
                (_, _) => { attempts++; Assert.Same(lease, GameInputLease.Current); return Task.FromResult(OperationOutcome.Completed("fresh Idle observation")); }, default);
            Assert.Equal(expectedAttempts, attempts);
            Assert.Equal(recovery ? 0 : 1, waits);
            Assert.Equal(expectedAttempts, result.Attempts);
            Assert.Equal(recovery ? "cleanup-uncertain" : focusReturns ? null : "quiet-resume-unavailable", result.SkippedReason);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("World Gate", 0, true)]
    [InlineData("World Gatekeeper", 0, false)]
    [InlineData("World Gate", 3000, false)]
    public void InteractionRequiresExactFreshStableTitle(string title, int ageMs, bool expected)
    {
        var now = DateTime.UtcNow.Ticks;
        var states = new Dictionary<string, bool?> { ["interactionPrompt"] = true, ["dialogue"] = false, ["menu"] = false, ["nativeIdle"] = false };
        var reading = new W101UiTreeResult(1, "test", W101UiTreeOperation.Version, WizwalkerOperation.SourceSha256,
            W101QuestConversationTests.Target, "observed", "test", false, states, now,
            now - TimeSpan.FromMilliseconds(ageMs).Ticks, 100, true, true, 2, 2, 0, new(states),
            InteractionTitle: title, StableInteractionTitle: title);
        Assert.Equal(expected, W101WorldGateVisit.PromptMatches(reading, now));
        Assert.False(W101WorldGateVisit.PromptMatches(reading with { StableInteractionTitle = null }, now));
        var menuStates = new Dictionary<string, bool?>(states) { ["menu"] = true };
        var overlay = reading with { States = menuStates, StableStates = new(menuStates) };
        Assert.False(W101WorldGateVisit.PromptMatches(overlay, now));
        Assert.Equal(expected, W101WorldGateVisit.CanReinteractOverlay(overlay, now));
        Assert.False(W101WorldGateVisit.CanReinteractOverlay(overlay with { Loading = true }, now));
        Assert.False(W101WorldGateVisit.CanReinteractOverlay(overlay with { StableInteractionTitle = null }, now));
        menuStates["nativeIdle"] = true;
        Assert.False(W101WorldGateVisit.CanReinteractOverlay(overlay, now));
    }
    [Theory]
    [InlineData("World Gate Press X", false)]
    [InlineData("SPIRAL MAP Available Worlds Wizard City Krokotopia EXIT Go To World", true)]
    [InlineData("SPIRAL MAP Available Worlds Zafaria Avalon EXIT Go To World", true)]
    [InlineData("SPIRAL MAP AVAILABLE WRLDS ZAFARIA EXIT GO TO WORLD'", true)]
    [InlineData("Wizard City", false)]
    [InlineData("Shop Mooshu Dragonspyre", false)]
    public void MenuNeedsPageIndependentHeadingAndControls(string text, bool expected) =>
        Assert.Equal(expected, W101WorldGateVisit.MenuMatches(text));
}
