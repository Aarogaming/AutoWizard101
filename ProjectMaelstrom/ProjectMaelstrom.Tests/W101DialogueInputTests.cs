using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101DialogueInputTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "quest-input-" + Guid.NewGuid().ToString("N"));
    private static ModuleDecision Proposal => new("menu", "entry", "proposal", "p1", W101QuestConversation.ActionId,
        new(W101QuestConversation.ActionId, "keyboard-pulse", "SPACE", 40, W101QuestStep.ControlId));
    private OperationContext Context(GameInputLease lease) => new(new("run", "quest", "v1", 60, true, "target", new Dictionary<string, string>()), _root, lease);

    [Theory]
    [InlineData("success")]
    [InlineData("down-timeout")]
    [InlineData("down-throws")]
    [InlineData("cancel")]
    [InlineData("takeover")]
    public async Task ReleaseIsAttemptedAfterEveryDownIncludingAmbiguousFailures(string mode)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        using var cancel = new CancellationTokenSource();
        var messages = new List<bool>(); var ready = true;
        bool Send(bool release)
        {
            messages.Add(release);
            if (!release && mode == "cancel") cancel.Cancel();
            if (!release && mode == "takeover") ready = false;
            if (!release && mode == "down-throws") throw new IOException();
            return release || mode != "down-timeout";
        }
        var result = await W101DialogueInput.DispatchAsync(Proposal, Context(lease), () => ready, Send, () => 1000, cancel.Token);
        Assert.Equal(new[] { false, true }, messages);
        Assert.True(result.InputReleased); Assert.Equal(mode == "success", result.Accepted);
    }

    [Fact]
    public async Task UnconfirmedReleaseLatchesRecovery()
    {
        using (var lease = GameInputLease.Acquire("test", _root))
        {
            using var scope = GameInputLease.EnterScope(lease);
            var receipt = await W101DialogueInput.DispatchAsync(Proposal, Context(lease), () => true, release => !release, () => 1000, default);
            Assert.False(receipt.InputReleased); Assert.True(lease.RecoveryRequired);
        }
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", _root));
    }

    [Fact]
    public async Task UnregisteredActionsAndMissingAuthoritySendNothing()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var calls = 0;
        bool Send(bool release) { calls++; return true; }
        Assert.False((await W101DialogueInput.DispatchAsync(Proposal with { ActionId = "other" }, Context(lease), () => true, Send, () => 0, default)).Accepted);
        Assert.False((await W101DialogueInput.DispatchAsync(Proposal, Context(lease), () => false, Send, () => 0, default)).Accepted);
        Assert.Equal(0, calls);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
