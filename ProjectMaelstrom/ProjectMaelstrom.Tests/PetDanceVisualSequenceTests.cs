using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceVisualSequenceTests
{
    private sealed class Rig(int expected = 3)
    {
        public PetDanceVisualSequence Tracker { get; } = new(expected);
        public double Time { get; private set; }
        public PetDanceVisualSequenceResult? Step(DanceCue cue, DancePhase phase = DancePhase.Watch,
            double delta = 25, DanceCue fadeOf = DanceCue.Unknown, double opacity = double.NaN)
        {
            Time += delta;
            return Tracker.Observe(new(Time, phase, cue, fadeOf, opacity));
        }
        public void Hold(DanceCue cue, int milliseconds, DanceCue fadeOf = DanceCue.Unknown, double opacity = double.NaN)
        { for (var elapsed = 0; elapsed < milliseconds; elapsed += 25) Step(cue, fadeOf: fadeOf, opacity: opacity); }
        public void Arrow(DanceCue direction)
        { Hold(DanceCue.Neutral, 100); Hold(direction, 250); }
        public PetDanceVisualSequenceResult Go() => Assert.IsType<PetDanceVisualSequenceResult>(Step(DanceCue.Neutral, DancePhase.Go));
    }

    [Fact]
    public void StableAppearancesProduceOneOrderedVisualSequence()
    {
        var rig = new Rig();
        foreach (var cue in new[] { DanceCue.Up, DanceCue.Left, DanceCue.Down }) rig.Arrow(cue);
        Assert.Equal(new[] { "up", "left", "down" }, rig.Tracker.CurrentCues);
        var result = rig.Go();
        Assert.True(result.Accepted);
        Assert.Equal("visual-complete", result.Status);
        Assert.Equal("visual", result.Source);
        Assert.All(result.Evidence, evidence =>
        {
            Assert.True(evidence.SupportingFrames >= 2);
            Assert.True(evidence.ConfirmedQpcMs - evidence.OnsetQpcMs >= 50);
        });
        Assert.Null(rig.Step(DanceCue.Unknown, DancePhase.Go));
    }

    [Fact]
    public void HeldDirectionIsNotRepeatedButConfirmedTroughSeparatesRepeats()
    {
        var rig = new Rig();
        rig.Hold(DanceCue.Up, 1000);
        Assert.Single(rig.Tracker.Appearances);
        rig.Arrow(DanceCue.Up); rig.Arrow(DanceCue.Up);
        var result = rig.Go();
        Assert.True(result.Accepted);
        Assert.Equal(new[] { "up", "up", "up" }, result.Cues);
        Assert.Equal("confirmed-trough-repeat", result.Evidence[1].Transition);
        Assert.Equal("confirmed-trough-repeat", result.Evidence[2].Transition);
    }

    [Fact]
    public void RisingFadesDoNotPoisonFirstArrowOrRevokeRepeatTrough()
    {
        var rig = new Rig();
        rig.Hold(DanceCue.Neutral, 100);
        for (var occurrence = 0; occurrence < 3; occurrence++)
        {
            foreach (var opacity in new[] { .2, .45, .65 }) rig.Step(DanceCue.Fading, fadeOf: DanceCue.Right, opacity: opacity);
            rig.Hold(DanceCue.Right, 150);
            rig.Step(DanceCue.Fading, fadeOf: DanceCue.Right, opacity: .5);
            rig.Hold(DanceCue.Fading, 50, DanceCue.Right, .2);
        }
        var result = rig.Go();
        Assert.True(result.Accepted);
        Assert.Equal(new[] { "right", "right", "right" }, result.Cues);
    }

    [Fact]
    public void ClearDirectionChangesNeedNoInventedNeutralFrame()
    {
        var rig = new Rig();
        rig.Hold(DanceCue.Left, 300); rig.Hold(DanceCue.Down, 300); rig.Hold(DanceCue.Right, 300);
        var result = rig.Go();
        Assert.True(result.Accepted);
        Assert.Equal(new[] { "left", "down", "right" }, result.Cues);
        Assert.Equal("direction-change", result.Evidence[1].Transition);
    }

    [Fact]
    public void BriefUnknownOnsetsDoNotEraseLaterClearAppearances()
    {
        var rig = new Rig();
        foreach (var direction in new[] { DanceCue.Left, DanceCue.Up, DanceCue.Right })
        {
            rig.Hold(DanceCue.Neutral, 100); rig.Hold(DanceCue.Unknown, 50); rig.Hold(direction, 250);
        }
        Assert.True(rig.Go().Accepted);
    }

    [Fact]
    public void SameDirectionAfterUnconfirmedTransitionIsUncertainNotAnInventedRepeat()
    {
        var rig = new Rig();
        rig.Arrow(DanceCue.Up);
        rig.Step(DanceCue.Fading, fadeOf: DanceCue.Up, opacity: .5);
        rig.Hold(DanceCue.Up, 250);
        Assert.Single(rig.Tracker.Appearances);
        rig.Arrow(DanceCue.Down); rig.Arrow(DanceCue.Right);
        var result = rig.Go();
        Assert.False(result.Accepted);
        Assert.Equal(3, result.Cues.Length);
        Assert.Contains("repeat count is uncertain", result.Reason);
    }

    [Theory]
    [InlineData("source-gap")]
    [InlineData("long-unknown")]
    public void SignificantGapsRetainPartialEvidenceForFallback(string mode)
    {
        var rig = new Rig(); rig.Arrow(DanceCue.Left);
        if (mode == "source-gap") rig.Step(DanceCue.Unknown, delta: 200);
        else rig.Hold(DanceCue.Unknown, 200);
        rig.Arrow(DanceCue.Down); rig.Arrow(DanceCue.Right);
        var result = rig.Go();
        Assert.False(result.Accepted);
        Assert.Equal("visual-uncertain", result.Status);
        Assert.Equal(3, result.Cues.Length);
        Assert.Contains("150 ms", result.Reason);
    }

    [Fact]
    public void TooCloseDifferentShapesCannotBecomeIndependentOccurrences()
    {
        var rig = new Rig();
        rig.Hold(DanceCue.Up, 100); rig.Hold(DanceCue.Left, 100); rig.Hold(DanceCue.Down, 100);
        var result = rig.Go();
        Assert.False(result.Accepted);
        Assert.Contains("250 ms", result.Reason);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void FreshPreviewRecoversMissingFeedbackWithoutReusingOldCues(int count)
    {
        var rig = new Rig();
        for (var index = 0; index < 3; index++) rig.Arrow(DanceCue.Left);
        Assert.True(rig.Go().Accepted);
        Assert.Null(rig.Step(DanceCue.Unknown, DancePhase.Unknown));
        Assert.Null(rig.Step(DanceCue.Neutral, DancePhase.Go));
        rig.Step(DanceCue.Neutral);
        Assert.True(rig.Tracker.NewPreview);
        Assert.Empty(rig.Tracker.CurrentCues);
        for (var index = 0; index < count; index++) rig.Arrow(DanceCue.Right);
        var result = rig.Go();
        Assert.True(result.Accepted);
        Assert.Equal("outcome-unconfirmed", result.Status);
        Assert.Equal(count, result.Cues.Length);
        Assert.All(result.Cues, cue => Assert.Equal("right", cue));
        Assert.Equal(3, rig.Tracker.Expected);
    }

    [Fact]
    public void UnknownWithinPreviewDoesNotAnnounceNewPreviewOrClearCues()
    {
        var rig = new Rig(); rig.Arrow(DanceCue.Left);
        var start = rig.Tracker.WatchStartMs;
        rig.Step(DanceCue.Unknown, DancePhase.Unknown); rig.Step(DanceCue.Neutral);
        Assert.False(rig.Tracker.NewPreview);
        Assert.Equal(start, rig.Tracker.WatchStartMs);
        Assert.Single(rig.Tracker.Appearances);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void MissingFeedbackDoesNotPermitMissingOrJumpingCueCounts(int count)
    {
        var rig = new Rig(); for (var index = 0; index < 3; index++) rig.Arrow(DanceCue.Left);
        Assert.True(rig.Go().Accepted);
        for (var index = 0; index < count; index++) rig.Arrow(DanceCue.Right);
        Assert.False(rig.Go().Accepted);
    }

    [Fact]
    public void SelectedAudioFallbackKeepsVisualFeedbackAccountingSynchronized()
    {
        var rig = new Rig();
        Assert.Throws<InvalidOperationException>(() => rig.Tracker.NotifyResponseSelected(3));
        rig.Arrow(DanceCue.Left); rig.Arrow(DanceCue.Right);
        Assert.False(rig.Go().Accepted);
        Assert.Throws<InvalidOperationException>(() => rig.Tracker.NotifyResponseSelected(4));
        rig.Tracker.NotifyResponseSelected(3);
        rig.Step(DanceCue.Neutral, DancePhase.Success);
        Assert.Equal(4, rig.Tracker.Expected);
        rig.Step(DanceCue.Neutral, DancePhase.Success);
        Assert.Equal(4, rig.Tracker.Expected);
        Assert.Throws<InvalidOperationException>(() => rig.Tracker.NotifyResponseSelected(4));
        for (var index = 0; index < 4; index++) rig.Arrow(DanceCue.Up);
        Assert.True(rig.Go().Accepted);
    }

    [Fact]
    public void LoadingClearsAppearancesAndGoCannotReuseThem()
    {
        var rig = new Rig(); rig.Arrow(DanceCue.Left);
        rig.Step(DanceCue.Unknown, DancePhase.Loading);
        Assert.Empty(rig.Tracker.CurrentCues);
        Assert.Null(rig.Step(DanceCue.Neutral, DancePhase.Go));
        Assert.False(rig.Tracker.NeedsGoAssessment);
    }

    [Fact]
    public void Live06ShortFullRunIsConfirmedByItsQualifiedFallingShape()
    {
        var rig = new Rig();
        rig.Step(DanceCue.Up, delta: 54.4302, opacity: .9678);
        rig.Step(DanceCue.Up, delta: 33.3334, opacity: .9013);
        Assert.Empty(rig.Tracker.Appearances);
        rig.Step(DanceCue.Fading, delta: 133.3306, fadeOf: DanceCue.Up, opacity: .6169);
        var first = Assert.Single(rig.Tracker.Appearances);
        Assert.Equal(2, first.SupportingFrames);
        Assert.Equal(1, first.FadeSupportingFrames);
        Assert.Equal(87.7636, first.LastFullQpcMs!.Value, 6);
        Assert.Equal(221.0942, first.ConfirmedQpcMs, 6);
        Assert.Equal(33.3334, first.LastFullQpcMs.Value - first.OnsetQpcMs, 6);
        rig.Arrow(DanceCue.Right); rig.Arrow(DanceCue.Down);
        Assert.True(rig.Go().Accepted);
    }

    [Theory]
    [InlineData("one-full")]
    [InlineData("fade-only")]
    [InlineData("wrong-direction")]
    [InlineData("long-gap")]
    [InlineData("brightening")]
    public void FadeConfirmationCannotInventOrExtendUnqualifiedFullEvidence(string fault)
    {
        var rig = new Rig();
        if (fault != "fade-only") rig.Step(DanceCue.Up, delta: 50, opacity: .9);
        if (fault is not ("one-full" or "fade-only")) rig.Step(DanceCue.Up, delta: 33.3334, opacity: .8);
        rig.Step(DanceCue.Fading, delta: fault == "long-gap" ? 151 : fault == "brightening" ? 10 : 50,
            fadeOf: fault == "wrong-direction" ? DanceCue.Down : DanceCue.Up, opacity: .6);
        if (fault == "brightening") rig.Step(DanceCue.Fading, delta: 20, fadeOf: DanceCue.Up, opacity: .65);
        if (fault is "one-full" or "fade-only")
            foreach (var opacity in new[] { .4, .2, .15 }) rig.Step(DanceCue.Fading, fadeOf: DanceCue.Up, opacity: opacity);
        Assert.Empty(rig.Tracker.Appearances);
        rig.Arrow(DanceCue.Right); rig.Arrow(DanceCue.Down);
        Assert.False(rig.Go().Accepted);
    }

    [Fact]
    public void Live06RepeatBridgesUnknownBetweenTwoActualTroughSamples()
    {
        var rig = new Rig(); rig.Arrow(DanceCue.Up);
        rig.Step(DanceCue.Fading, fadeOf: DanceCue.Up, opacity: .223455);
        rig.Step(DanceCue.Unknown, delta: 50, opacity: .116099);
        rig.Step(DanceCue.Neutral, delta: 49.998, opacity: .008461);
        rig.Step(DanceCue.Up, delta: 49.999, opacity: 1.0053);
        rig.Step(DanceCue.Up, delta: 50.0002, opacity: .9013);
        rig.Step(DanceCue.Up, delta: 66.667, opacity: .7548);
        Assert.Equal(2, rig.Tracker.Appearances.Count);
        Assert.Equal("confirmed-trough-repeat", rig.Tracker.Appearances[1].Transition);
        rig.Hold(DanceCue.Up, 50); // Keep the unrelated next arrow beyond the 250 ms occurrence bound.
        rig.Arrow(DanceCue.Right);
        var result = rig.Go();
        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(new[] { "up", "up", "right" }, result.Cues);
    }

    [Fact]
    public void Live06PreviouslySuccessfulRepeatRemainsSeparated()
    {
        var rig = new Rig(); rig.Arrow(DanceCue.Left);
        rig.Step(DanceCue.Fading, fadeOf: DanceCue.Left, opacity: .1881);
        rig.Step(DanceCue.Neutral, delta: 33.332, opacity: .1004);
        rig.Step(DanceCue.Left, delta: 49.999, opacity: 1.2899);
        rig.Hold(DanceCue.Left, 150); // Preserve the repeat timing; space the unrelated next arrow normally.
        rig.Arrow(DanceCue.Down);
        var result = rig.Go();
        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(new[] { "left", "left", "down" }, result.Cues);
    }

    [Theory]
    [InlineData("unknown-only")]
    [InlineData("long-unknown")]
    [InlineData("source-gap")]
    [InlineData("old-positive")]
    [InlineData("high-fade")]
    [InlineData("high-unknown")]
    [InlineData("full-arrow")]
    [InlineData("full-unknown")]
    public void TroughBridgeStillRequiresTwoPositiveSamplesWithoutInterveningHighEvidence(string fault)
    {
        var rig = new Rig(); rig.Arrow(DanceCue.Up);
        rig.Step(DanceCue.Fading, fadeOf: DanceCue.Up, opacity: .2);
        switch (fault)
        {
            case "unknown-only": rig.Step(DanceCue.Unknown, opacity: .1); break;
            case "long-unknown": rig.Hold(DanceCue.Unknown, 200, opacity: .1); break;
            case "source-gap": rig.Step(DanceCue.Unknown, delta: 151, opacity: .1); break;
            case "old-positive": rig.Step(DanceCue.Unknown, delta: 75, opacity: .1); break;
            case "high-fade": rig.Step(DanceCue.Fading, fadeOf: DanceCue.Up, opacity: .5); break;
            case "high-unknown": rig.Step(DanceCue.Unknown, opacity: .8); break;
            case "full-arrow": rig.Step(DanceCue.Up, opacity: 1); break;
            case "full-unknown": rig.Step(DanceCue.Up, DancePhase.Unknown, opacity: 1); break;
        }
        if (fault != "unknown-only") rig.Step(DanceCue.Neutral, delta: fault == "old-positive" ? 100 : 25, opacity: .01);
        rig.Hold(DanceCue.Up, 100);
        Assert.Single(rig.Tracker.Appearances);
        rig.Arrow(DanceCue.Right); rig.Arrow(DanceCue.Down);
        Assert.False(rig.Go().Accepted);
    }
}
