using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PetDanceFadeTests
{
    [Fact]
    public void IndependentFootageRequiresExplicitEvaluationAndKeepsItsOwnSourceIdentity()
    {
        var calibration = new string('a', 64); var observation = new string('b', 64);
        Assert.Throws<InvalidDataException>(() => PetDancePanelReplay.ValidateVideoSources(calibration, observation, false));
        Assert.True(PetDancePanelReplay.ValidateVideoSources(calibration, observation, true));
        Assert.False(PetDancePanelReplay.ValidateVideoSources(calibration, calibration.ToUpperInvariant(), true));
        Assert.False(PetDancePanelReplay.ValidateVideoSources(calibration, calibration, false));
        Assert.Throws<InvalidDataException>(() => PetDancePanelReplay.ValidateVideoSources("unknown", observation, true));
        Assert.Throws<InvalidDataException>(() => PetDancePanelReplay.ValidateVideoSources(calibration, null, true));
    }

    private readonly Size _size = new(1282, 760);
    [Fact]
    public void PacketRejectsDuplicatedSkippedFramesAndMissingClockBeforeReadingImages()
    {
        var packet = new DancePanelPacket("panels.rgb", new string('A', 64), [0, 16, 33], new string('B', 64));
        PetDancePanelReplay.ValidatePacket(packet);
        Assert.Throws<InvalidDataException>(() => PetDancePanelReplay.ValidatePacket(packet with { TimesMs = [0, 0, 33] }));
        Assert.Throws<InvalidDataException>(() => PetDancePanelReplay.ValidatePacket(packet with { TimesMs = null! }));
        Assert.Throws<InvalidDataException>(() => PetDancePanelReplay.ValidatePacket(packet with { SourceVideoSha256 = "unknown" }));
    }

    [Fact]
    public void OutcomeNeedsContinuousFreshEvidenceAndMonotonicTime()
    {
        var gate = new DanceOutcomeGate();
        Assert.Equal(DancePhase.Resolving, gate.Observe(0, DancePhase.Resolving, DancePhase.Success));
        Assert.Equal(DancePhase.Unknown, gate.Observe(30, DancePhase.Unknown, DancePhase.Unknown));
        Assert.Equal(DancePhase.Resolving, gate.Observe(60, DancePhase.Resolving, DancePhase.Success));
        Assert.Equal(DancePhase.Success, gate.Observe(120, DancePhase.Resolving, DancePhase.Success));
        Assert.Equal(DancePhase.Resolving, gate.Observe(400, DancePhase.Resolving, DancePhase.Success));
        Assert.Throws<InvalidDataException>(() => gate.Observe(400, DancePhase.Resolving, DancePhase.Success));
        Assert.Throws<InvalidDataException>(() => gate.Observe(-1, DancePhase.Resolving, DancePhase.Success));
    }
    private double _time;
    private DanceDecision Sample(PetDanceSequence sequence, DancePhase phase, DanceCue cue, int count = 3,
        DanceCue fadeOf = DanceCue.Unknown, double opacity = double.NaN)
    {
        DanceDecision result = null!;
        for (var i = 0; i < count; i++) { _time += 50; result = sequence.Observe(_time, _size, phase, cue, fadeOf, opacity); }
        return result;
    }
    private void Arrow(PetDanceSequence sequence, DanceCue cue)
    {
        Sample(sequence, DancePhase.Watch, cue);
        Sample(sequence, DancePhase.Watch, DanceCue.Fading, 1, cue, .5);
        Sample(sequence, DancePhase.Watch, DanceCue.Fading, 2, cue, .2);
    }
    [Fact]
    public void LoadingPausesWithoutFailureAndDiscardsOldProposalsBeforeFreshWatch()
    {
        var sequence = new PetDanceSequence(3);
        Assert.False(Sample(sequence, DancePhase.Loading, DanceCue.Unknown, 400).Rejected);
        Sample(sequence, DancePhase.Watch, DanceCue.Neutral);
        foreach (var cue in new[] { DanceCue.Up, DanceCue.Left, DanceCue.Up }) Arrow(sequence, cue);
        Assert.Equal(3, Sample(sequence, DancePhase.Go, DanceCue.Neutral, 1).Proposals.Length);
        Sample(sequence, DancePhase.Success, DanceCue.Neutral);
        var loading = Sample(sequence, DancePhase.Loading, DanceCue.Unknown, 400);
        Assert.False(loading.Rejected); Assert.Empty(loading.Cues); Assert.Empty(loading.Proposals);
        Assert.Empty(Sample(sequence, DancePhase.Go, DanceCue.Unknown, 1).Proposals);
        // A fresh scene must not continue the prior partial sequence or count loading as Success.
        Sample(sequence, DancePhase.Loading, DanceCue.Unknown, 1);
        Sample(sequence, DancePhase.Watch, DanceCue.Neutral);
        foreach (var cue in new[] { DanceCue.Right, DanceCue.Down, DanceCue.Right, DanceCue.Up }) Arrow(sequence, cue);
        var resumed = Sample(sequence, DancePhase.Go, DanceCue.Neutral, 1);
        Assert.False(resumed.Rejected, resumed.Reason); Assert.Equal(4, resumed.Proposals.Length);
    }
    [Fact]
    public void KnownFadeRearmsRepeatedArrowsWithoutNeedingLongBlankFrames()
    {
        var sequence = new PetDanceSequence(3);
        Sample(sequence, DancePhase.Watch, DanceCue.Neutral);
        Arrow(sequence, DanceCue.Right); Arrow(sequence, DanceCue.Right); Arrow(sequence, DanceCue.Up);
        var result = Sample(sequence, DancePhase.Go, DanceCue.Neutral, 1);
        Assert.False(result.Rejected, result.Reason);
        Assert.Equal(new[] { "dance-right", "dance-right", "dance-up" }, result.Proposals);
        Assert.Empty(Sample(sequence, DancePhase.Go, DanceCue.Neutral).Proposals);
    }
    [Theory]
    [InlineData("wrong-direction")]
    [InlineData("brightening")]
    [InlineData("overlong")]
    [InlineData("unknown")]
    public void InvalidFadeEvidenceStillRejectsTheRound(string problem)
    {
        var sequence = new PetDanceSequence(3);
        Sample(sequence, DancePhase.Watch, DanceCue.Neutral);
        Sample(sequence, DancePhase.Watch, DanceCue.Up);
        var result = Sample(sequence, DancePhase.Watch, DanceCue.Fading, 1, DanceCue.Up, .4);
        result = problem switch
        {
            "wrong-direction" => Sample(sequence, DancePhase.Watch, DanceCue.Fading, 1, DanceCue.Left, .2),
            "brightening" => Sample(sequence, DancePhase.Watch, DanceCue.Fading, 1, DanceCue.Up, .6),
            "overlong" => Sample(sequence, DancePhase.Watch, DanceCue.Fading, 8, DanceCue.Up, .4),
            _ => Sample(sequence, DancePhase.Watch, DanceCue.Unknown)
        };
        Assert.True(result.Rejected);
        Assert.Empty(Sample(sequence, DancePhase.Go, DanceCue.Neutral).Proposals);
    }
    [Fact]
    public void LowFadeAndSingleNeutralFrameFormOneObservedTrough()
    {
        var sequence = new PetDanceSequence(3);
        Sample(sequence, DancePhase.Watch, DanceCue.Neutral);
        foreach (var cue in new[] { DanceCue.Right, DanceCue.Right, DanceCue.Up })
        {
            Sample(sequence, DancePhase.Watch, cue);
            Sample(sequence, DancePhase.Watch, DanceCue.Fading, 1, cue, .2);
            Sample(sequence, DancePhase.Watch, DanceCue.Neutral, 1);
        }
        Assert.Equal(3, Sample(sequence, DancePhase.Go, DanceCue.Neutral, 1).Proposals.Length);
    }
    [Fact]
    public void TwoNeutralSamplesAfterKnownFadeQualifyACompleteTrough()
    {
        var sequence = new PetDanceSequence(3);
        Sample(sequence, DancePhase.Watch, DanceCue.Neutral);
        foreach (var cue in new[] { DanceCue.Up, DanceCue.Left, DanceCue.Down })
        {
            Sample(sequence, DancePhase.Watch, cue);
            Sample(sequence, DancePhase.Watch, DanceCue.Fading, 1, cue, .31);
            Sample(sequence, DancePhase.Watch, DanceCue.Neutral, 2);
        }
        Assert.Equal(3, Sample(sequence, DancePhase.Go, DanceCue.Neutral, 1).Proposals.Length);
    }
    [Fact]
    public void PartialFadeDoesNotQualifyGoAndResolvingIsNotSuccess()
    {
        var sequence = new PetDanceSequence(3);
        Sample(sequence, DancePhase.Watch, DanceCue.Neutral);
        Arrow(sequence, DanceCue.Down); Arrow(sequence, DanceCue.Left);
        Sample(sequence, DancePhase.Watch, DanceCue.Up);
        Sample(sequence, DancePhase.Watch, DanceCue.Fading, 2, DanceCue.Up, .5);
        Assert.True(Sample(sequence, DancePhase.Go, DanceCue.Fading, 1).Rejected);
        Sample(sequence, DancePhase.Resolving, DanceCue.Neutral);
        Assert.True(Sample(sequence, DancePhase.Watch, DanceCue.Neutral).Rejected);
    }
    [Fact]
    public void OutcomeIsAppliedOnlyOnceAndAllowsTheNextRoundToBeginAtItsFirstCue()
    {
        var sequence = new PetDanceSequence(3);
        Sample(sequence, DancePhase.Watch, DanceCue.Neutral);
        foreach (var cue in new[] { DanceCue.Up, DanceCue.Down, DanceCue.Left }) Arrow(sequence, cue);
        Assert.Equal(3, Sample(sequence, DancePhase.Go, DanceCue.Neutral, 1).Proposals.Length);
        Sample(sequence, DancePhase.Success, DanceCue.Neutral);
        Sample(sequence, DancePhase.Resolving, DanceCue.Neutral);
        Sample(sequence, DancePhase.Success, DanceCue.Neutral);
        foreach (var cue in new[] { DanceCue.Up, DanceCue.Down, DanceCue.Left, DanceCue.Right }) Arrow(sequence, cue);
        var result = Sample(sequence, DancePhase.Go, DanceCue.Neutral, 1);
        Assert.False(result.Rejected, result.Reason); Assert.Equal(4, result.Proposals.Length);
    }
    [Fact]
    public void PixelFitRejectsUnexplainedContentAndKeepsFadeSeparateFromDirection()
    {
        Assert.Equal(DanceCue.Up, PetDancePanelVision.Classify(50, 1, 5, 20, DanceCue.Up).Cue);
        Assert.Equal((DanceCue.Fading, DanceCue.Up), PetDancePanelVision.Classify(20, .3, 6, 5, DanceCue.Up));
        Assert.Equal(DanceCue.Neutral, PetDancePanelVision.Classify(3, .04, 2, .1, DanceCue.Up).Cue);
        Assert.Equal(DanceCue.Unknown, PetDancePanelVision.Classify(50, .3, 30, 5, DanceCue.Up).Cue);
        Assert.Equal(DanceCue.Unknown, PetDancePanelVision.Classify(50, 1, 5, 1, DanceCue.Up).Cue);
        Assert.Equal(DanceCue.Unknown, PetDancePanelVision.Classify(50, double.NaN, 5, 20, DanceCue.Up).Cue);
    }
}
