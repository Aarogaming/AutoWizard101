using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class NavigationArrivalTests
{
    private static NavigationArrivalContract Portal(string destination = "pavilion") =>
        new(NavigationArrivalKind.ZoneTransition, "pid-start", "commons", "before", destination);
    private static NavigationArrivalObservation Sample(double time, bool? loading = false, string? zone = "pavilion",
        string? epoch = "after", bool released = true) => new(time, "pid-start", zone, epoch, loading, true, released, InputReleasedMs: released ? 105 : null);
    private static NavigationArrivalGate Gate(NavigationArrivalContract? contract = null) => new(contract ?? Portal(), 100, 1000, 100);

    [Fact]
    public void ReleaseBooleanCannotPromotePreReleaseOrUnstampedEvidence()
    {
        foreach (double? release in new double?[] { null, 130, 140, double.NaN, 99 })
        {
            var gate = Gate(); gate.Observe(Sample(110, true), 110);
            Assert.Equal(NavigationArrivalState.Waiting,
                gate.Observe(Sample(130) with { InputReleasedMs = release }, 130).State);
        }
    }

    [Fact]
    public void PortalNeedsLoadingReleaseAndTwoFreshTargetReadings()
    {
        var gate = Gate();
        Assert.Equal(NavigationArrivalState.Waiting, gate.Observe(Sample(110), 110).State);
        Assert.Equal(NavigationArrivalState.Waiting, gate.Observe(Sample(120, true, null, null, false), 120).State);
        Assert.Equal(NavigationArrivalState.Waiting, gate.Observe(Sample(130, released: false), 130).State);
        Assert.Equal(NavigationArrivalState.Confirming, gate.Observe(Sample(140), 140).State);
        var result = gate.Observe(Sample(150), 150);
        Assert.Equal(NavigationArrivalState.Verified, result.State);
        Assert.Equal(120, result.LoadingObservedMs);
        Assert.Equal(150, result.ArrivalObservedMs);
        Assert.Equal("after", result.ArrivalEpoch);
    }

    [Fact]
    public void SameZonePortalRequiresNewEpoch()
    {
        var gate = Gate(Portal("commons"));
        gate.Observe(Sample(110, true), 110);
        Assert.Equal(NavigationArrivalState.Waiting, gate.Observe(Sample(120, zone: "commons", epoch: "before"), 120).State);
        Assert.Equal(NavigationArrivalState.Confirming, gate.Observe(Sample(130, zone: "commons"), 130).State);
        Assert.Equal(NavigationArrivalState.Verified, gate.Observe(Sample(140, zone: "commons"), 140).State);
    }

    [Theory]
    [InlineData("commons", "after")]
    [InlineData("other-zone", "after")]
    [InlineData("pavilion", "before")]
    [InlineData("pavilion", null)]
    public void WrongZoneOrUnqualifiedEpochNeverConfirms(string zone, string? epoch)
    {
        var gate = Gate();
        gate.Observe(Sample(110, true), 110);
        for (int i = 0; i < 3; i++) Assert.Equal(NavigationArrivalState.Waiting,
            gate.Observe(Sample(120 + i, zone: zone, epoch: epoch), 120 + i).State);
    }

    [Fact]
    public void StalePreDispatchAndFutureLoadingCannotAuthorizeArrival()
    {
        foreach (var sample in new[] { Sample(90, true), Sample(110, true), Sample(400, true), Sample(220, true) with { Fresh = false } })
        {
            var gate = Gate();
            Assert.False(gate.Observe(sample, 250).SawLoading);
            Assert.Equal(NavigationArrivalState.Waiting, gate.Observe(Sample(260), 260).State);
            Assert.Equal(NavigationArrivalState.Waiting, gate.Observe(Sample(270), 270).State);
        }
    }

    [Fact]
    public void DuplicateUnknownAndEpochChangesResetConfirmation()
    {
        var gate = Gate();
        gate.Observe(Sample(110, true), 110);
        gate.Observe(Sample(120), 120);
        Assert.Equal(NavigationArrivalState.Waiting, gate.Observe(Sample(120), 130).State);
        Assert.Equal(NavigationArrivalState.Confirming, gate.Observe(Sample(140), 140).State);
        Assert.Equal(NavigationArrivalState.Waiting, gate.Observe(Sample(150, null), 150).State);
        Assert.Equal(NavigationArrivalState.Confirming, gate.Observe(Sample(160), 160).State);
        Assert.Equal(NavigationArrivalState.Confirming, gate.Observe(Sample(170, epoch: "third"), 170).State);
        Assert.Equal(NavigationArrivalState.Verified, gate.Observe(Sample(180, epoch: "third"), 180).State);
    }

    [Fact]
    public void TargetChangeCancellationClockFailureAndTimeoutAreTerminal()
    {
        var target = Gate();
        Assert.Equal(NavigationArrivalState.Failed, target.Observe(Sample(110) with { TargetIdentity = "restarted" }, 110).State);
        Assert.Equal(NavigationArrivalState.Failed, target.Observe(Sample(120), 120).State);
        Assert.Equal(NavigationArrivalState.Failed, Gate().Observe(Sample(110), 110, true).State);
        Assert.Equal(NavigationArrivalState.Failed, Gate().Observe(Sample(110), double.NaN).State);
        Assert.Equal(NavigationArrivalState.TimedOut, Gate().Observe(Sample(1100), 1100).State);
    }

    [Fact]
    public void PromptAndCoordinateArrivalsKeepDistinctCriteria()
    {
        var prompt = Gate(new(NavigationArrivalKind.InteractionPrompt, "pid-start", "commons", "before", "commons", "Dance Game"));
        var s = Sample(110, zone: "commons", epoch: "before") with { Position = new(0, 0, 0), Prompt = "Other Game" };
        Assert.Equal(NavigationArrivalState.Waiting, prompt.Observe(s, 110).State);
        Assert.Equal(NavigationArrivalState.Confirming, prompt.Observe(s with { TimeMs = 120, Prompt = " Dance Game " }, 120).State);
        Assert.Equal(NavigationArrivalState.Verified, prompt.Observe(s with { TimeMs = 130, Prompt = "dance game" }, 130).State);

        var coordinate = Gate(new(NavigationArrivalKind.Coordinate, "pid-start", "commons", "before", "commons", DestinationPoint: new(0, 0, 0), Radius: 5, VerticalTolerance: 2));
        Assert.Equal(NavigationArrivalState.Waiting, coordinate.Observe(s with { Position = new(0, 0, 3) }, 110).State);
        Assert.Equal(NavigationArrivalState.Confirming, coordinate.Observe(s with { TimeMs = 120 }, 120).State);
        Assert.Equal(NavigationArrivalState.Verified, coordinate.Observe(s with { TimeMs = 130 }, 130).State);
    }

    [Fact]
    public void InvalidContractsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => Gate(Portal() with { DestinationZone = "" }));
        Assert.Throws<InvalidDataException>(() => Gate(Portal() with { Kind = NavigationArrivalKind.InteractionPrompt }));
        Assert.Throws<InvalidDataException>(() => Gate(new(NavigationArrivalKind.Coordinate, "pid", "a", "epoch", "a")));
        Assert.Throws<InvalidDataException>(() => new NavigationArrivalGate(Portal(), 100, 1000, double.NaN));
    }
}
