using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PetDanceAudioVisualAgreementTests
{
    private static PetDanceAudioObservation Cue(double time, string name) => new(time, time + 270, "detected", name, .95, .2, true);

    [Fact]
    public void RepeatedIndependentSoundsMustMatchEveryVisualOccurrence()
    {
        var audio = new[] { Cue(1200, "Up"), Cue(1700, "Up"), Cue(2200, "Right") };
        Assert.True(PetDanceAudioVisualAgreement.Compare(["up", "up", "right"], audio, 1000, 3000).Agrees);
        Assert.False(PetDanceAudioVisualAgreement.Compare(["up", "up", "right"], audio.Take(2), 1000, 3000).Agrees);
        Assert.False(PetDanceAudioVisualAgreement.Compare(["up", "right", "up"], audio, 1000, 3000).Agrees);
    }

    [Fact]
    public void LateEventsCannotRetroactivelyConfirmGo()
    {
        var audio = new[] { Cue(1200, "Up"), Cue(1700, "Down"), Cue(2800, "Right") };
        var result = PetDanceAudioVisualAgreement.Compare(["up", "down", "right"], audio, 1000, 3000);
        Assert.False(result.Agrees);
        Assert.Equal(2, result.Audio.Length);
    }

    [Theory]
    [InlineData("clock-gap")]
    [InlineData("discontinuity")]
    [InlineData("timestamp-error")]
    [InlineData("clock-overlap")]
    [InlineData("nonmonotonic-packet-time")]
    [InlineData("invalid-pcm-packet")]
    [InlineData("unsupported-packet-flags")]
    [InlineData("analysis-duration-limit")]
    [InlineData("reset")]
    public void BrokenContinuityInvalidatesTheRound(string status)
    {
        var audio = new[] { Cue(1200, "Left"), Cue(1700, "Down"), Cue(2200, "Right"),
            new PetDanceAudioObservation(1500, 1500, status, null, 0, 0, false) };
        Assert.False(PetDanceAudioVisualAgreement.Compare(["left", "down", "right"], audio, 1000, 3000).Agrees);
    }

    [Fact]
    public void DuplicateAndExtraEventsDoNotBecomeAConfirmedSequence()
    {
        var audio = new[] { Cue(1200, "Left"), Cue(1700, "Down"), Cue(1700, "Down"), Cue(2200, "Right") };
        Assert.False(PetDanceAudioVisualAgreement.Compare(["left", "down", "right"], audio, 1000, 3000).Agrees);
    }

    [Fact]
    public void EarlierMenuSoundsAndNonemittedWindowsAreExcluded()
    {
        var audio = new[] { Cue(100, "Down"), Cue(1200, "Left"), Cue(1700, "Down"), Cue(2200, "Right"),
            Cue(2300, "Up") with { Emitted = false } };
        Assert.True(PetDanceAudioVisualAgreement.Compare(["left", "down", "right"], audio, 1000, 3000).Agrees);
    }

    [Fact]
    public void SourceSamplesAvailableBeforeGoDoNotExcuseLateProcessing()
    {
        var audio = new[] { Cue(1200, "Left"), Cue(1700, "Down"), Cue(2200, "Right") with { ProcessedQpcMs = 3001 } };
        Assert.False(PetDanceAudioVisualAgreement.Compare(["left", "down", "right"], audio, 1000, 3000).Agrees);
    }
}
