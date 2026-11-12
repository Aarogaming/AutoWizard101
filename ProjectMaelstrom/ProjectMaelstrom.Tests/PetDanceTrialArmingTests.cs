using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceTrialArmingTests
{
    [Fact]
    public void MenuAndLoadingActivityCannotArmOrFreezeAnEarlyBaseline()
    {
        var gate = new PetDanceTrialArming(true, 10);
        foreach (var phase in new[] { DancePhase.Unknown, DancePhase.Ready, DancePhase.Loading, DancePhase.Go, DancePhase.Success })
            Assert.False(gate.TryArm(phase, true, true, true, 1000, 50));
        Assert.False(gate.Armed);
        Assert.Equal(10, gate.Baseline);
        Assert.True(gate.TryArm(DancePhase.Watch, true, true, true, 500, 51));
        Assert.True(gate.Armed);
        Assert.Equal(51, gate.Baseline);
    }

    [Theory]
    [InlineData(false, true, true, 500)]
    [InlineData(true, false, true, 500)]
    [InlineData(true, true, false, 500)]
    [InlineData(true, true, true, 499.99)]
    [InlineData(true, true, true, -1)]
    [InlineData(true, true, true, double.NaN)]
    [InlineData(true, true, true, double.PositiveInfinity)]
    public void StaleUnfocusedHeldOrNonquietWatchCannotEnableInput(bool fresh, bool targetReady, bool noKeysHeld, double quiet)
    {
        var gate = new PetDanceTrialArming(true, 1);
        Assert.False(gate.TryArm(DancePhase.Watch, fresh, targetReady, noKeysHeld, quiet, 7));
        Assert.False(gate.Armed);
        Assert.Equal(1, gate.Baseline);
    }

    [Fact]
    public void LaterActivityCannotRebaseAnArmedTrial()
    {
        var gate = new PetDanceTrialArming(true, 1);
        Assert.True(gate.TryArm(DancePhase.Watch, true, true, true, 500, 7));
        Assert.False(gate.TryArm(DancePhase.Loading, true, true, true, 1000, 8));
        Assert.False(gate.TryArm(DancePhase.Watch, true, true, true, 1000, 8));
        Assert.Equal(7, gate.Baseline);
        Assert.NotEqual(8, gate.Baseline); // Caller sees revision mismatch and stops; no automatic resume.
    }

    [Fact]
    public void OriginalVisualOnlyModeRetainsImmediateInitialBaseline()
    {
        var gate = new PetDanceTrialArming(false, 42);
        Assert.True(gate.Armed);
        Assert.Equal(42, gate.Baseline);
        Assert.False(gate.TryArm(DancePhase.Watch, true, true, true, 500, 99));
        Assert.Equal(42, gate.Baseline);
    }
}
