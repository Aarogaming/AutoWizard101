using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101NavigationInputTests : IDisposable
{
    [Theory]
    [InlineData("W", "game.move-forward")]
    [InlineData("S", "game.move-backward")]
    public async Task LoadingApproachReleasesEitherDirectionWhenGuardDrops(string key, string id)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = Context(lease);
        context = context with { Request = context.Request with { Operation = W101NavigationVisit.Id } };
        var proposal = new ModuleDecision("route", "segment", "proposal", "p1", id, new(id, "keyboard-pulse", key, 15000, "w101.walk-until-loading"));
        var calls = new List<(string, bool)>();
        var result = await W101NavigationInput.DispatchAsync(proposal, context, Target, () => true,
            (k, up) => { calls.Add((k, up)); return true; }, () => 1, default, () => false);
        Assert.False(result.Accepted); Assert.True(result.InputReleased);
        Assert.Equal(new[] { (key, false), (key, true) }, calls);
    }
    [Fact]
    public async Task ReleaseRetriesTemporaryLoadingFailureWithoutAnotherPress()
    {
        var attempts = 0;
        Assert.True(await W101NavigationInput.ReleaseAsync(() => ++attempts == 3));
        Assert.Equal(3, attempts);
        Assert.False(await W101NavigationInput.ReleaseAsync(() => false, 1));
    }
    [Fact]
    public async Task AssistedHoldStillReleasesImmediatelyOnTakeover()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var owner = GameInputLease.EnterScope(lease);
        var context = Context(lease);
        context = context with { Request = context.Request with { Operation = W101NavigationVisit.Id } };
        var proposal = Proposal(duration: 3000);
        proposal = proposal with { Action = proposal.Action! with { ControlId = "w101.assisted-navigation" } };
        var calls = new List<bool>(); var checks = 0;
        var result = await W101NavigationInput.DispatchAsync(proposal, context, Target, () => true,
            (_, up) => { calls.Add(up); return true; }, () => 1, default, () => ++checks < 3);
        Assert.False(result.Accepted); Assert.True(result.InputReleased);
        Assert.Equal(new[] { false, true }, calls);
        calls.Clear();
        await W101NavigationInput.DispatchAsync(proposal, Context(lease), Target, () => true,
            (_, up) => { calls.Add(up); return true; }, () => 1, default);
        Assert.Empty(calls);
    }
    private const string Target = "pid-123-start-456";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "navigation-input-" + Guid.NewGuid().ToString("N"));
    private static ModuleDecision Proposal(string key = "W", int duration = 20)
    {
        var id = key switch { "W" => "game.move-forward", "A" => "game.turn-left", _ => "game.turn-right" };
        return new("route", "segment", "proposal", "p1", id, new(id, "keyboard-pulse", key, duration, "w101.navigation"));
    }
    private OperationContext Context(GameInputLease? lease) => new(new("run", "route", "v1", 60, true,
        Target, new Dictionary<string, string>()), _root, lease);

    [Theory]
    [InlineData("W")]
    [InlineData("A")]
    [InlineData("D")]
    public async Task ExactKeysReceivePairedBoundedPulse(string key)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var calls = new List<(string, bool)>();
        var result = await W101NavigationInput.DispatchAsync(Proposal(key), Context(lease), Target, () => true,
            (k, up) => { calls.Add((k, up)); return true; }, () => 1, default);
        Assert.True(result.Accepted); Assert.True(result.InputReleased);
        Assert.Equal(new[] { (key, false), (key, true) }, calls);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("throws")]
    [InlineData("cancel")]
    [InlineData("takeover")]
    public async Task AttemptedDownAlwaysReleases(string mode)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        using var cancel = new CancellationTokenSource();
        var continuing = true;
        var calls = new List<bool>();
        bool Send(string key, bool up)
        {
            calls.Add(up);
            if (!up && mode == "throws") throw new IOException();
            if (!up && mode == "cancel") cancel.Cancel();
            if (!up && mode == "takeover") continuing = false;
            return up || mode != "failed";
        }
        var result = await W101NavigationInput.DispatchAsync(Proposal(duration: 300), Context(lease), Target,
            () => true, Send, () => 1, cancel.Token, () => continuing);
        Assert.False(result.Accepted); Assert.True(result.InputReleased);
        Assert.Equal(new[] { false, true }, calls);
    }

    [Fact]
    public async Task HoldChecksTakeoverRepeatedlyWithoutRequiringFreshInitialEvidence()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var checks = 0; var fresh = true; var calls = new List<bool>();
        var result = await W101NavigationInput.DispatchAsync(Proposal(duration: 300), Context(lease), Target, () => fresh,
            (_, up) => { calls.Add(up); fresh = false; return true; }, () => 1, default, () => ++checks < 3);
        Assert.False(result.Accepted); Assert.True(result.InputReleased); Assert.Equal(3, checks);
        Assert.Equal(new[] { false, true }, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnconfirmedReleaseLatchesRecovery(bool throws)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var result = await W101NavigationInput.DispatchAsync(Proposal(), Context(lease), Target, () => true,
            (_, up) => { if (up && throws) throw new IOException(); return !up; }, () => 1, default);
        Assert.False(result.InputReleased); Assert.True(lease.RecoveryRequired);
    }

    [Fact]
    public async Task RejectsWrongBindingsOwnershipTargetAndCancellationBeforeInput()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var original = Proposal(); var calls = 0;
        bool Send(string key, bool up) { calls++; return true; }
        var invalid = new[]
        {
            Proposal("S"), Proposal(duration: 0), Proposal(duration: 301),
            original with { ActionId = "other" }, original with { Action = null },
            original with { Action = original.Action! with { Kind = "other" } },
            original with { Action = original.Action! with { ControlId = "other" } }
        };
        foreach (var proposal in invalid)
            Assert.False((await W101NavigationInput.DispatchAsync(proposal, Context(lease), Target, () => true, Send, () => 1, default)).Accepted);
        foreach (var context in new[] { Context(null), Context(lease) with { Request = Context(lease).Request with { WritesGame = false } },
            Context(lease) with { Request = Context(lease).Request with { DelegatedInput = true } },
            Context(lease) with { Request = Context(lease).Request with { Target = "pid-123-start-789" } } })
            Assert.False((await W101NavigationInput.DispatchAsync(original, context, Target, () => true, Send, () => 1, default)).Accepted);
        Assert.False((await W101NavigationInput.DispatchAsync(original, Context(lease), Target, () => true, Send, () => 1, new CancellationToken(true))).Accepted);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task TakeoverBetweenAdmissionAndDownSendsNothing()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var checks = 0; var calls = 0;
        var result = await W101NavigationInput.DispatchAsync(Proposal(), Context(lease), Target, () => ++checks == 1,
            (_, _) => { calls++; return true; }, () => 1, default);
        Assert.False(result.Accepted); Assert.Equal(0, calls);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
