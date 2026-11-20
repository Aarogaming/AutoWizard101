using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101IdleCycleTests
{
    [Fact]
    public void WakeAlreadyAwakeCompletesWithoutInputButRequiresValidEvidence()
    {
        var cycle = new W101IdleCycle(Binding, "test", "wake");
        Assert.True(cycle.Observe(Frame(100, "AwakeCandidate")).Completed);
        Assert.Equal(0, cycle.Actions);
        foreach (var frame in new[] { Frame(100, "AwakeCandidate") with { Fresh = false },
            Frame(100, "AwakeCandidate") with { ControlVerified = false },
            Frame(100, "AwakeCandidate") with { TargetReady = false },
            Frame(100, "AwakeCandidate") with { InputOwned = false } })
            Assert.False(new W101IdleCycle(Binding, "test", "wake").Observe(frame).Completed);
    }

    [Fact]
    public void WakeClosesObservedOptionsAndVerifiesAwake()
    {
        var cycle = new W101IdleCycle(Binding, "test", "wake");
        var wake = cycle.Observe(Frame(100, "NativeIdle")); Accept(cycle, wake, 140);
        var close = cycle.Observe(Frame(200, "MenuObserved"));
        Assert.Equal(W101IdleCycle.CloseMenu, close.Action); Accept(cycle, close, 240);
        Assert.True(cycle.Observe(Frame(300, "AwakeCandidate")).Completed);
        Assert.Equal(2, cycle.Actions);
    }

    [Fact]
    public void IdleEntryWorksFromAwakeAndIsAlreadyCompleteInIdle()
    {
        var cycle = new W101IdleCycle(Binding, "test", "idle");
        var menu = cycle.Observe(Frame(100, "AwakeCandidate"));
        Assert.Equal(W101IdleCycle.OpenMenu, menu.Action); Accept(cycle, menu, 140);
        var idle = cycle.Observe(Frame(200, "MenuObserved"));
        Assert.Equal(W101IdleCycle.SelectIdle, idle.Action); Accept(cycle, idle, 240);
        Assert.True(cycle.Observe(Frame(300, "NativeIdle")).Completed);
        var already = new W101IdleCycle(Binding, "test", "idle");
        Assert.True(already.Observe(Frame(100, "NativeIdle")).Completed);
        Assert.Equal(0, already.Actions);
    }

    private static readonly RoutineBinding Binding = W101IdleCycle.Bind(W101QuestConversationTests.Target, "geometry", "test");
    private static RoutineObservation Frame(double time, string scene) => new(time, scene, Binding, true, true, true, true, true);
    private static void Accept(W101IdleCycle cycle, ModuleDecision decision, double time) =>
        cycle.RecordReceipt(new(decision.ProposalId!, decision.ActionId!, time, true, true));

    [Theory]
    [InlineData("AwakeCandidate", 3)]
    [InlineData("MenuObserved", 2)]
    public void BothObservedWakeDestinationsCompleteWithoutFixedPlayback(string destination, int actions)
    {
        var cycle = new W101IdleCycle(Binding, "test");
        var wake = cycle.Observe(Frame(100, "NativeIdle"));
        Assert.Equal(W101IdleCycle.Wake, wake.Action); Accept(cycle, wake, 140);
        var next = cycle.Observe(Frame(200, destination));
        if (destination == "AwakeCandidate")
        {
            Assert.Equal(W101IdleCycle.OpenMenu, next.Action); Accept(cycle, next, 240);
            next = cycle.Observe(Frame(300, "MenuObserved"));
        }
        Assert.Equal(W101IdleCycle.SelectIdle, next.Action); Accept(cycle, next, 340);
        Assert.True(cycle.Observe(Frame(400, "NativeIdle")).Completed);
        Assert.Equal(actions, cycle.Actions);
        Assert.False(cycle.Observe(Frame(500, "AwakeCandidate")).IsProposal);
    }

    [Theory]
    [InlineData("AwakeCandidate")]
    [InlineData("AwakeFromIdle")]
    [InlineData("MenuObserved")]
    [InlineData("OutsideReady")]
    public void CannotEnterWithoutObservedIdleAndOwnWake(string scene)
    {
        var cycle = new W101IdleCycle(Binding, "test");
        Assert.False(cycle.Observe(Frame(100, scene)).IsProposal);
        Assert.Equal(0, cycle.Actions);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("rejected")]
    [InlineData("unreleased")]
    [InlineData("foreign")]
    [InlineData("stale")]
    [InlineData("old")]
    public void WakeRequiresExactReleasedReceiptAndNewFreshOutcome(string fault)
    {
        var cycle = new W101IdleCycle(Binding, "test");
        var wake = cycle.Observe(Frame(100, "NativeIdle"));
        if (fault != "missing") cycle.RecordReceipt(new(fault == "foreign" ? "other" : wake.ProposalId!, wake.ActionId!, 140,
            fault != "rejected", fault != "unreleased"));
        var frame = Frame(fault == "old" ? 140 : 200, "AwakeCandidate") with { Fresh = fault != "stale" };
        var next = cycle.Observe(frame);
        Assert.False(next.IsProposal); Assert.False(next.Completed); Assert.Equal(1, cycle.Actions);
    }

    [Fact]
    public void UnchangedIdleTimesOutWithoutAlternateKeyOrRetry()
    {
        var cycle = new W101IdleCycle(Binding, "test");
        var wake = cycle.Observe(Frame(100, "NativeIdle")); Accept(cycle, wake, 140);
        Assert.False(cycle.Observe(Frame(200, "NativeIdle")).IsProposal);
        Assert.True(cycle.Observe(Frame(15100, "NativeIdle")).Stopped);
        Assert.Equal(1, cycle.Actions);
    }

    [Fact]
    public void LoadingPausesButDoesNotRenewDeadline()
    {
        var cycle = new W101IdleCycle(Binding, "test");
        var wake = cycle.Observe(Frame(100, "NativeIdle")); Accept(cycle, wake, 140);
        Assert.False(cycle.Observe(Frame(500, "Loading")).IsProposal);
        Assert.True(cycle.Observe(Frame(60100, "Loading")).Stopped);
    }

    [Theory]
    [InlineData("takeover")]
    [InlineData("cancel")]
    [InlineData("binding")]
    [InlineData("ownership")]
    public void LostAuthorityStopsCycle(string fault)
    {
        var cycle = new W101IdleCycle(Binding, "test");
        var frame = Frame(100, "NativeIdle") with
        {
            UserActive = fault == "takeover",
            Cancelled = fault == "cancel",
            InputOwned = fault != "ownership",
            Binding = fault == "binding" ? Binding with { GeometryId = "other" } : Binding
        };
        Assert.True(cycle.Observe(frame).Stopped); Assert.Equal(0, cycle.Actions);
    }

    [Fact]
    public void MenuControlAndIdleOutcomeRemainRequired()
    {
        var cycle = new W101IdleCycle(Binding, "test");
        var wake = cycle.Observe(Frame(100, "NativeIdle")); Accept(cycle, wake, 140);
        Assert.False(cycle.Observe(Frame(200, "MenuObserved") with { ControlVerified = false }).IsProposal);
        var click = cycle.Observe(Frame(300, "MenuObserved")); Accept(cycle, click, 340);
        Assert.False(cycle.Observe(Frame(400, "MenuObserved")).Completed);
        Assert.True(cycle.Observe(Frame(500, "NativeIdle")).Completed);
    }

    [Fact]
    public void ForeignOutcomeAndVisitCannotGrantPermission()
    {
        Assert.Throws<InvalidDataException>(() => new W101IdleCycle(Binding, "other"));
        var cycle = new W101IdleCycle(Binding, "test");
        var wake = cycle.Observe(Frame(100, "NativeIdle")); Accept(cycle, wake, 140);
        Assert.False(cycle.Observe(Frame(200, "AwakeCandidate") with { OutcomeForProposalId = "other" }).IsProposal);
    }
}
