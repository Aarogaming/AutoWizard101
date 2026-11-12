using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101CombatInputTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "combat-input-" + Guid.NewGuid().ToString("N"));
    private static readonly Size Geometry = new(1280, 720);
    private readonly WizwalkerTarget _target = new(123, 42, 100, "WizardGraphicalClient",
        Path.Combine(Path.GetTempPath(), "inert-WizardGraphicalClient.exe"), new string('a', 64));
    private static W101CombatDispatch Proposal(string id = "p1") => new(id, W101CombatTurnOperation.SelectCard,
        "snapshot1", "geometry1", new("card-slot", "card-instance", W101CombatControlRole.Card, new(.25f, .5f, .25f, .2f), true));
    private OperationContext Context(GameInputLease? lease) => new(new("test", W101CombatTurnOperation.Id, "v1", 60,
        true, W101QuestStep.TargetBinding(_target), new Dictionary<string, string>()), _root, lease);
    private W101CombatInput Input(Func<WizwalkerTarget, Point, bool, bool> send,
        Func<WizwalkerTarget, bool>? current = null, Func<WizwalkerTarget, Size>? geometry = null,
        Func<int, CancellationToken, Task>? delay = null) => new(_target, "geometry1", Geometry,
        new(current ?? (_ => true), geometry ?? (_ => Geometry), send, () => 1000,
            delay ?? ((_, token) => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask)));

    [Theory]
    [InlineData(0, 0, 1, 1, 1280, 720, 640, 360)]
    [InlineData(0, 0, .1f, .1f, 100, 100, 5, 5)]
    [InlineData(.9f, .9f, .1f, .1f, 100, 100, 94, 94)]
    [InlineData(0, 0, 1, 1, 1, 1, 0, 0)]
    [InlineData(.99999f, .99999f, .00001f, .00001f, 32768, 32768, 32767, 32767)]
    public void NormalizedBoundsMapToClientRasterWithoutDesktopOffsets(float x, float y, float width, float height,
        int clientWidth, int clientHeight, int expectedX, int expectedY)
    {
        Assert.True(W101CombatInput.TryMapCenter(new(x, y, width, height), new(clientWidth, clientHeight), out var point));
        Assert.Equal(new Point(expectedX, expectedY), point);
        var packed = (long)W101CombatInput.PackClientPoint(point);
        Assert.Equal(point.X, (int)(packed & 0xffff));
        Assert.Equal(point.Y, (int)((packed >> 16) & 0xffff));
    }

    [Theory]
    [InlineData(float.NaN, 0, .1f, .1f, 1280, 720)]
    [InlineData(0, float.PositiveInfinity, .1f, .1f, 1280, 720)]
    [InlineData(0, 0, float.PositiveInfinity, .1f, 1280, 720)]
    [InlineData(-.1f, 0, .1f, .1f, 1280, 720)]
    [InlineData(0, 0, 0, .1f, 1280, 720)]
    [InlineData(0, 0, .1f, -.1f, 1280, 720)]
    [InlineData(.9f, .9f, .2f, .2f, 1280, 720)]
    [InlineData(1, 0, float.Epsilon, .1f, 1280, 720)]
    [InlineData(0, 1, .1f, float.Epsilon, 1280, 720)]
    [InlineData(0, 0, .1f, .1f, 0, 720)]
    [InlineData(0, 0, .1f, .1f, 1280, 32769)]
    public void InvalidOrUnrepresentableGeometryNeverMaps(float x, float y, float width, float height, int cw, int ch) =>
        Assert.False(W101CombatInput.TryMapCenter(new(x, y, width, height), new(cw, ch), out _));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(32768, 0)]
    public void MessagePackingRejectsWrappedCoordinates(int x, int y) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => W101CombatInput.PackClientPoint(new(x, y)));

    [Theory]
    [InlineData("no-lease")]
    [InlineData("no-scope")]
    [InlineData("read-only")]
    [InlineData("delegated")]
    [InlineData("wrong-target")]
    [InlineData("wrong-window")]
    [InlineData("resized")]
    [InlineData("geometry-throws")]
    [InlineData("not-ready")]
    [InlineData("cancelled")]
    [InlineData("wrong-geometry-id")]
    [InlineData("arbitrary-action")]
    [InlineData("wrong-role")]
    [InlineData("disabled-control")]
    [InlineData("invalid-bounds")]
    [InlineData("dead-target")]
    [InlineData("unknown-life")]
    public async Task InvalidAuthorityOrControlSendsNoMessages(string fault)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = fault == "no-scope" ? null : GameInputLease.EnterScope(lease);
        using var cancellation = new CancellationTokenSource();
        var context = Context(fault == "no-lease" ? null : lease);
        if (fault == "read-only") context = context with { Request = context.Request with { WritesGame = false } };
        if (fault == "delegated") context = context with { Request = context.Request with { DelegatedInput = true } };
        if (fault == "wrong-target") context = context with { Request = context.Request with { Target = "other" } };
        if (fault == "cancelled") cancellation.Cancel();
        var proposal = Proposal();
        if (fault == "wrong-geometry-id") proposal = proposal with { GeometryId = "other" };
        if (fault == "arbitrary-action") proposal = proposal with { ActionId = "click-anything" };
        if (fault == "wrong-role") proposal = proposal with { Control = proposal.Control with { Role = W101CombatControlRole.Pass } };
        if (fault == "disabled-control") proposal = proposal with { Control = proposal.Control with { Enabled = false } };
        if (fault == "invalid-bounds") proposal = proposal with { Control = proposal.Control with { Bounds = new(-1, 0, 1, 1) } };
        if (fault is "dead-target" or "unknown-life") proposal = proposal with
        {
            ActionId = W101CombatTurnOperation.SelectTarget,
            Control = proposal.Control with { Role = W101CombatControlRole.Enemy, Alive = fault == "dead-target" ? false : null }
        };
        var calls = 0;
        var input = Input((_, _, _) => { calls++; return true; }, _ => fault != "wrong-window",
            _ => fault == "geometry-throws" ? throw new IOException() : fault == "resized" ? new Size(1920, 1080) : Geometry);
        var result = await input.DispatchAsync(proposal, context, () => fault != "not-ready", cancellation.Token);
        Assert.False(result.Accepted); Assert.True(result.InputReleased);
        Assert.Equal(0, calls); Assert.False(lease.RecoveryRequired);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("down-timeout")]
    [InlineData("down-throws")]
    [InlineData("cancel-in-send")]
    [InlineData("cancel-in-hold")]
    [InlineData("takeover")]
    [InlineData("resize-during-hold")]
    [InlineData("delay-throws")]
    public async Task EveryAttemptedDownGetsAnUpDespiteCancellationOrReadinessLoss(string mode)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        using var cancellation = new CancellationTokenSource();
        var messages = new List<bool>(); var ready = true; var size = Geometry;
        bool Send(WizwalkerTarget target, Point point, bool release)
        {
            Assert.Equal(_target, target); Assert.Equal(new Point(480, 432), point);
            messages.Add(release);
            if (!release && mode == "cancel-in-send") cancellation.Cancel();
            if (!release && mode == "down-throws") throw new IOException();
            return release || mode != "down-timeout";
        }
        Task Delay(int milliseconds, CancellationToken token)
        {
            Assert.Equal(40, milliseconds);
            if (mode == "takeover") ready = false;
            if (mode == "resize-during-hold") size = new(1920, 1080);
            if (mode == "cancel-in-hold") cancellation.Cancel();
            if (mode == "delay-throws") throw new IOException();
            token.ThrowIfCancellationRequested(); return Task.CompletedTask;
        }
        var result = await Input(Send, geometry: _ => size, delay: Delay).DispatchAsync(Proposal(), Context(lease), () => ready, cancellation.Token);
        Assert.Equal(new[] { false, true }, messages);
        Assert.True(result.InputReleased); Assert.Equal(mode == "success", result.Accepted);
        Assert.False(lease.RecoveryRequired);
    }

    [Fact]
    public async Task PressGateIsRecheckedAfterProposalPreparation()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var gates = 0; var sent = 0;
        var input = Input((_, _, _) => { sent++; return true; });
        Assert.False((await input.DispatchAsync(Proposal(), Context(lease), () => ++gates == 1, default)).Accepted);
        Assert.Equal(2, gates); Assert.Equal(0, sent);
        Assert.False((await input.DispatchAsync(Proposal(), Context(lease), () => true, default)).Accepted);
        Assert.Equal(0, sent);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("exception")]
    [InlineData("replaced-target")]
    [InlineData("notice-failure")]
    public async Task UnknownReleaseBlocksGameplayEvenWhenWarningCannotBeWritten(string mode)
    {
        using (var lease = GameInputLease.AcquireForTesting("test", _root, writeWarning:
            mode == "notice-failure" ? (_, _) => throw new IOException("offline write failure") : null))
        {
            using var scope = GameInputLease.EnterScope(lease);
            var current = true; var messages = new List<bool>();
            bool Send(WizwalkerTarget target, Point point, bool release)
            {
                messages.Add(release);
                if (!release && mode == "replaced-target") current = false;
                if (release && mode == "exception") throw new IOException();
                return !release;
            }
            var input = Input(Send, _ => current);
            var receipt = await input.DispatchAsync(Proposal(), Context(lease), () => true, default);
            Assert.False(receipt.Accepted); Assert.False(receipt.InputReleased);
            Assert.True(lease.RecoveryRequired); Assert.False(lease.CanDispatch);
            Assert.Equal(mode == "replaced-target" ? new[] { false } : new[] { false, true }, messages);
            Assert.False((await input.DispatchAsync(Proposal("another"), Context(lease), () => true, default)).Accepted);
        }
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("retry", _root));
    }

    [Fact]
    public async Task ConcurrentAndReplayedProposalsCannotAddPresses()
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<bool>();
        var input = Input((_, _, release) => { calls.Add(release); return true; }, delay: (_, _) => held.Task);
        var first = input.DispatchAsync(Proposal(), Context(lease), () => true, default);
        Assert.False((await input.DispatchAsync(Proposal("second"), Context(lease), () => true, default)).Accepted);
        Assert.Equal(new[] { false }, calls);
        held.SetResult(); Assert.True((await first).Accepted);
        Assert.False((await input.DispatchAsync(Proposal(), Context(lease), () => true, default)).Accepted);
        Assert.Equal(new[] { false, true }, calls);
    }

    [Theory]
    [InlineData(0, W101CombatTurnOperation.SelectCard)]
    [InlineData(3, W101CombatTurnOperation.Pass)]
    [InlineData(1, W101CombatTurnOperation.SelectTarget)]
    [InlineData(2, W101CombatTurnOperation.SelectTarget)]
    public async Task OnlyRegisteredControlRolesProduceMatchingReceipts(int roleValue, string action)
    {
        using var lease = GameInputLease.Acquire("test", _root);
        using var scope = GameInputLease.EnterScope(lease);
        var p = Proposal() with { ActionId = action, Control = Proposal().Control with { Role = (W101CombatControlRole)roleValue, Alive = true } };
        var receipt = await Input((_, _, _) => true).DispatchAsync(p, Context(lease), () => true, default);
        Assert.Equal(new RoutineReceipt("p1", action, 1000, true, true), receipt);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
