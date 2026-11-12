using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class BackgroundRecoveryProbeTests
{
    private static WindowsRecoveryObservation Valid => new(true, true, true, false, true, false, 42, true);
    [Fact]
    public void BackgroundModeRequiresExplicitSelection()
    {
        var challenge = new InputRecoveryChallenge("test", 100);
        Assert.False(new WindowsInputRecoveryProbe(() => Valid, () => 100).Read(challenge, true).InputStateKnown);
        var sample = new WindowsInputRecoveryProbe(() => Valid, () => 100, false).Read(challenge, true);
        Assert.True(sample.InputStateKnown);
        Assert.True(sample.InputsReleased);
    }

    [Fact]
    public void BackgroundModeStillRejectsUnavailableDesktopAndUnknownOrHeldInput()
    {
        foreach (var state in new[] { Valid with { SessionActive = false }, Valid with { DesktopAccessible = false },
            Valid with { SameDesktop = false }, Valid with { StableForeground = false },
            Valid with { InputReadable = false }, Valid with { AnyHeld = true } })
        {
            var sample = new WindowsInputRecoveryProbe(() => state, () => 100, false)
                .Read(new InputRecoveryChallenge("test", 100), true);
            Assert.False(sample.InputsReleased);
        }
    }
}
