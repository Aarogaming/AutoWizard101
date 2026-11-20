using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101ModuleInputTests : IDisposable
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpiredPrePressEvidenceDoesNotUndoDeliveryButTakeoverStillStops(bool stillOwned)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var fresh = true;
        var result = await W101ModuleInput.DispatchAsync(Proposal(), Context(lease), () => fresh,
            (_, release) => { if (!release) fresh = false; return true; }, () => 1000, default,
            continuing: () => stillOwned);
        Assert.Equal(stillOwned, result.Accepted); Assert.True(result.InputReleased);
    }
    [Fact]
    public async Task ExactWakeBindingUsesOnlyEscape()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var calls = new List<(string, bool)>();
        var action = W101IdleCycle.Wake;
        var proposal = new ModuleDecision("cycle", "wake", "test", "wake-1", action.ActionId, action);
        var result = await W101ModuleInput.DispatchAsync(proposal, Context(lease), () => true,
            (key, release) => { calls.Add((key, release)); return true; }, () => 1000, default);
        Assert.True(result.Accepted); Assert.True(result.InputReleased);
        Assert.Equal(new[] { ("ESCAPE", false), ("ESCAPE", true) }, calls);
    }
    private readonly string _root = Path.Combine(Path.GetTempPath(), "module-input-" + Guid.NewGuid().ToString("N"));
    private static ModuleDecision Proposal(string key = "X") => new("menu", "entry", "proposal", "p1",
        key == "X" ? "game.interact" : "game.open-menu",
        key == "X" ? new("game.interact", "keyboard-pulse", "X", 40, "w101.interaction") :
            new("game.open-menu", "keyboard-pulse", "ESCAPE", 40, "outside-menu-command"));
    private OperationContext Context(GameInputLease? lease) => new(new("run", "module", "v1", 60, true, "target",
        new Dictionary<string, string>()), _root, lease);

    [Theory]
    [InlineData("X", "success")]
    [InlineData("ESCAPE", "success")]
    [InlineData("X", "down-timeout")]
    [InlineData("X", "down-throws")]
    [InlineData("X", "cancel")]
    [InlineData("ESCAPE", "takeover")]
    public async Task AttemptedDownAlwaysGetsRelease(string key, string mode)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        using var cancel = new CancellationTokenSource();
        var messages = new List<(string, bool)>(); var ready = true;
        bool Send(string selected, bool release)
        {
            messages.Add((selected, release));
            if (!release && mode == "cancel") cancel.Cancel();
            if (!release && mode == "takeover") ready = false;
            if (!release && mode == "down-throws") throw new IOException();
            return release || mode != "down-timeout";
        }
        var result = await W101ModuleInput.DispatchAsync(Proposal(key), Context(lease), () => ready, Send, () => 1000, cancel.Token);
        Assert.Equal(new[] { (key, false), (key, true) }, messages);
        Assert.True(result.InputReleased);
        Assert.Equal(mode == "success", result.Accepted);
        Assert.Equal("p1", result.ProposalId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnconfirmedReleaseRequiresRecovery(bool throws)
    {
        using (var lease = GameInputLease.Acquire("test", _root))
        {
            using var scope = GameInputLease.EnterScope(lease);
            bool Send(string key, bool release)
            {
                if (release && throws) throw new IOException();
                return !release;
            }
            var result = await W101ModuleInput.DispatchAsync(Proposal(), Context(lease), () => true, Send, () => 1000, default);
            Assert.False(result.InputReleased);
            Assert.True(lease.RecoveryRequired);
        }
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", _root));
    }

    [Fact]
    public async Task ExactBindingsAndCurrentOwnerAreRequired()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var calls = 0;
        bool Send(string key, bool release) { calls++; return true; }
        var original = Proposal();
        var invalid = new[]
        {
            original with { ActionId = "other" },
            original with { Action = original.Action! with { Key = "SPACE" } },
            original with { Action = original.Action! with { DurationMs = 400 } },
            original with { Action = original.Action! with { ControlId = "other" } },
            original with { Action = null }
        };
        foreach (var proposal in invalid)
            Assert.False((await W101ModuleInput.DispatchAsync(proposal, Context(lease), () => true, Send, () => 0, default)).Accepted);
        Assert.False((await W101ModuleInput.DispatchAsync(original, Context(null), () => true, Send, () => 0, default)).Accepted);
        var context = Context(lease);
        Assert.False((await W101ModuleInput.DispatchAsync(original, context with { Request = context.Request with { WritesGame = false } },
            () => true, Send, () => 0, default)).Accepted);
        Assert.False((await W101ModuleInput.DispatchAsync(original, context with { Request = context.Request with { DelegatedInput = true } },
            () => true, Send, () => 0, default)).Accepted);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task TakeoverBetweenAdmissionAndDownSendsNothing()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var checks = 0; var calls = 0;
        var result = await W101ModuleInput.DispatchAsync(Proposal(), Context(lease), () => ++checks == 1,
            (_, _) => { calls++; return true; }, () => 0, default);
        Assert.False(result.Accepted);
        Assert.True(result.InputReleased);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task PreCancelledRequestSendsNothing()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var result = await W101ModuleInput.DispatchAsync(Proposal(), Context(lease), () => true,
            (_, _) => throw new InvalidOperationException("Must not send"), () => 0, new CancellationToken(true));
        Assert.False(result.Accepted);
        Assert.True(result.InputReleased);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
