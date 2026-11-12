using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceSoundSequenceTests
{
    private static PetDanceAudioObservation Sound(double at, string direction)
        => new(at, at + 220, "cue-candidate", direction, .95, .2, true, at + 225);

    private static PetDanceAudioObservation[] Sounds() => [Sound(1200, "Left"), Sound(1700, "Down"), Sound(2200, "Down")];

    private static PetDanceSoundVisualSample[] Visual(bool full = true)
    {
        var rows = new List<PetDanceSoundVisualSample> { new(1000, DancePhase.Watch, DanceCue.Unknown) };
        foreach (var sound in Sounds())
        {
            var cue = full ? Enum.Parse<DanceCue>(sound.Direction!) : DanceCue.Unknown;
            rows.Add(new(sound.CandidateQpcMs - 50, DancePhase.Watch, DanceCue.Unknown));
            rows.Add(new(sound.CandidateQpcMs, DancePhase.Watch, cue));
            rows.Add(new(sound.CandidateQpcMs + 75, DancePhase.Watch, cue));
            rows.Add(new(sound.CandidateQpcMs + 150, DancePhase.Watch, DanceCue.Fading));
        }
        rows.Add(new(3000, DancePhase.Go, DanceCue.Neutral));
        return rows.ToArray();
    }

    [Fact]
    public void IndependentRepeatedSoundsUseClearFramesDespiteUnknownOnsetsAndFades()
    {
        var result = PetDanceSoundSequence.Assess(Visual(), Sounds(), 1000, 3000, 3);
        Assert.True(result.Accepted);
        Assert.Equal("corroborated", result.Status);
        Assert.Equal(new[] { "left", "down", "down" }, result.Cues);
        Assert.All(result.Evidence, cue => { Assert.Equal(2, cue.MatchingFrames); Assert.Equal("corroborated", cue.VisualStatus); });
    }

    [Fact]
    public void UnclearArrowPixelsKeepCompleteSoundSequenceButDoNotClaimAgreement()
    {
        var result = PetDanceSoundSequence.Assess(Visual(false), Sounds(), 1000, 3000, 3);
        Assert.True(result.Accepted);
        Assert.Equal("audio-led-visual-uncertain", result.Status);
        Assert.Equal(new[] { "left", "down", "down" }, result.Cues);
        Assert.All(result.Evidence, cue => Assert.Equal("visual-unclear-audio-preferred", cue.VisualStatus));
    }

    [Fact]
    public void ConflictingFullArrowIsRecordedRatherThanSilentlyCalledCorroboration()
    {
        var visual = Visual().Select(sample => sample.QpcMs == 1200 ? sample with { Cue = DanceCue.Right } : sample).ToArray();
        var result = PetDanceSoundSequence.Assess(visual, Sounds(), 1000, 3000, 3);
        Assert.True(result.Accepted);
        Assert.Equal("audio-led-visual-uncertain", result.Status);
        Assert.Equal(1, result.Evidence[0].ConflictingFrames);
        Assert.Equal("visual-conflict-audio-preferred", result.Evidence[0].VisualStatus);
        Assert.Equal("left", result.Cues[0]);
    }

    [Fact]
    public void SoundDoesNotSupplyMissingWatchOrGoScreenState()
    {
        Assert.False(PetDanceSoundSequence.Assess(Visual().Where(s => s.QpcMs != 1000).ToArray(), Sounds(), 1000, 3000, 3).Accepted);
        Assert.False(PetDanceSoundSequence.Assess(Visual().Where(s => s.Phase != DancePhase.Go).ToArray(), Sounds(), 1000, 3000, 3).Accepted);
    }

    [Fact]
    public void MissingExtraLateWeakAndTooCloseAudioRemainIntegrityFailures()
    {
        Assert.False(PetDanceSoundSequence.Assess(Visual(), Sounds()[..2], 1000, 3000, 3).Accepted);
        Assert.False(PetDanceSoundSequence.Assess(Visual(), [.. Sounds(), Sound(2500, "Up")], 1000, 3000, 3).Accepted);
        foreach (var bad in new[]
        {
            Sounds()[0] with { ProcessedQpcMs = null }, Sounds()[0] with { ProcessedQpcMs = 3001 },
            Sounds()[0] with { Similarity = .799 }, Sounds()[0] with { Margin = .079 },
            Sounds()[0] with { AvailableQpcMs = 1300 }, Sounds()[0] with { Direction = "Unknown" },
            Sounds()[0] with { CandidateQpcMs = 1650, AvailableQpcMs = 1870, ProcessedQpcMs = 1875 }
        })
            Assert.False(PetDanceSoundSequence.Assess(Visual(), [bad, Sounds()[1], Sounds()[2]], 1000, 3000, 3).Accepted);
    }

    [Theory]
    [InlineData("clock-gap")]
    [InlineData("packet-discontinuity")]
    [InlineData("timestamp-error")]
    [InlineData("analysis-queue-gap")]
    [InlineData("reset")]
    public void LostAudioContinuityIsNotJustVisualUncertainty(string reason)
    {
        var audio = Sounds().Append(new PetDanceAudioObservation(1600, 1600, reason, null, 0, 0, false, 1601)).ToArray();
        Assert.False(PetDanceSoundSequence.Assess(Visual(), audio, 1000, 3000, 3).Accepted);
    }

    [Fact]
    public void MidpointWindowsNeverReuseOneVisualFrameForTwoOccurrences()
    {
        var audio = new[] { Sound(1200, "Down"), Sound(1450, "Down"), Sound(2200, "Down") };
        var visual = new[]
        {
            new PetDanceSoundVisualSample(1000, DancePhase.Watch, DanceCue.Unknown),
            new(1250, DancePhase.Watch, DanceCue.Down), new(1325, DancePhase.Watch, DanceCue.Down),
            new(1400, DancePhase.Watch, DanceCue.Down), new(2200, DancePhase.Watch, DanceCue.Down),
            new(2275, DancePhase.Watch, DanceCue.Down), new(3000, DancePhase.Go, DanceCue.Neutral)
        };
        var result = PetDanceSoundSequence.Assess(visual, audio, 1000, 3000, 3);
        Assert.True(result.Accepted);
        Assert.Equal(1, result.Evidence[0].MatchingFrames);
        Assert.Equal(1, result.Evidence[1].MatchingFrames);
        Assert.Equal(1400, result.Evidence[1].FirstMatchMs);
        Assert.Equal("partial-visual-support-audio-preferred", result.Evidence[0].VisualStatus);
        Assert.Equal("partial-visual-support-audio-preferred", result.Evidence[1].VisualStatus);
    }

    [Fact]
    public void TrackerProposesOnlyOnceAndAdvancesOnlyAfterAcceptedSuccess()
    {
        var tracker = new PetDanceSoundSequence();
        PetDanceSoundSequenceResult? proposal = null;
        foreach (var sample in Visual()) proposal = tracker.Observe(sample, Sounds()) ?? proposal;
        Assert.True(proposal!.Accepted);
        Assert.Null(tracker.Observe(new(3020, DancePhase.Go, DanceCue.Unknown), Sounds()));
        Assert.Null(tracker.Observe(new(3050, DancePhase.Unknown, DanceCue.Unknown), Sounds()));
        Assert.Null(tracker.Observe(new(3100, DancePhase.Go, DanceCue.Neutral), Sounds()));
        Assert.Equal(3, tracker.Expected);
        tracker.Observe(new(3300, DancePhase.Success, DanceCue.Neutral), []);
        Assert.Equal(4, tracker.Expected);
        tracker.Observe(new(3320, DancePhase.Success, DanceCue.Neutral), []);
        Assert.Equal(4, tracker.Expected);
    }

    [Fact]
    public void FailureAndUnrelatedSuccessDoNotAdvanceExpectedLength()
    {
        var tracker = new PetDanceSoundSequence();
        tracker.Observe(new(500, DancePhase.Success, DanceCue.Neutral), []);
        foreach (var sample in Visual()) tracker.Observe(sample, Sounds());
        tracker.Observe(new(3300, DancePhase.Failure, DanceCue.Neutral), []);
        Assert.Equal(3, tracker.Expected);
        tracker.Observe(new(3400, DancePhase.Success, DanceCue.Neutral), []);
        Assert.Equal(3, tracker.Expected);
    }

    [Fact]
    public void LoadingDiscardsPendingRoundAndGoWithoutNewWatchCannotPropose()
    {
        var tracker = new PetDanceSoundSequence();
        tracker.Observe(new(1000, DancePhase.Watch, DanceCue.Unknown), []);
        tracker.Observe(new(1100, DancePhase.Loading, DanceCue.Unknown), []);
        Assert.Null(tracker.Observe(new(3000, DancePhase.Go, DanceCue.Neutral), Sounds()));
        Assert.False(tracker.NeedsGoAssessment);
    }

    private static PetDanceSoundSequenceResult LaterRound(PetDanceSoundSequence tracker, double start, int count,
        Func<PetDanceAudioObservation[], PetDanceAudioObservation[]>? audioChange = null)
    {
        string[] directions = ["Right", "Up", "Left", "Down"];
        var audio = Enumerable.Range(0, count).Select(index => Sound(start + 200 + index * 500, directions[index % directions.Length])).ToArray();
        Assert.Null(tracker.Observe(new(start, DancePhase.Watch, DanceCue.Neutral), []));
        foreach (var cue in audio)
        {
            var direction = Enum.Parse<DanceCue>(cue.Direction!);
            Assert.Null(tracker.Observe(new(cue.CandidateQpcMs, DancePhase.Watch, direction), []));
            Assert.Null(tracker.Observe(new(cue.CandidateQpcMs + 75, DancePhase.Watch, direction), []));
        }
        var combined = Sounds().Concat(audioChange == null ? audio : audioChange(audio)).ToArray();
        return Assert.IsType<PetDanceSoundSequenceResult>(tracker.Observe(
            new(start + 500 * count, DancePhase.Go, DanceCue.Neutral), combined));
    }

    private static PetDanceSoundSequence HandledFirstRound()
    {
        var tracker = new PetDanceSoundSequence();
        foreach (var sample in Visual()) tracker.Observe(sample, Sounds());
        Assert.False(tracker.NeedsGoAssessment);
        return tracker;
    }

    [Theory]
    [InlineData("Unknown", 3)]
    [InlineData("Unknown", 4)]
    [InlineData("Resolving", 3)]
    [InlineData("Resolving", 4)]
    public void FreshWatchAfterMissedFeedbackUsesOnlyNewQualifiedAudio(string intermediate, int count)
    {
        var tracker = HandledFirstRound();
        Assert.Null(tracker.Observe(new(3200, Enum.Parse<DancePhase>(intermediate), DanceCue.Unknown), []));
        var result = LaterRound(tracker, 4000, count);
        Assert.True(result.Accepted);
        Assert.Equal("outcome-unconfirmed", result.Status);
        Assert.Contains("Previous round outcome unconfirmed", result.Reason);
        Assert.Equal(count, result.Cues.Length);
        Assert.Equal("right", result.Cues[0]); // Previous response began Left.
        Assert.All(result.Evidence, cue => Assert.True(cue.CandidateQpcMs >= 4000));
        Assert.Equal(3, tracker.Expected); // No invented Success or advancement.
        Assert.False(tracker.NeedsGoAssessment);
        Assert.Null(tracker.Observe(new(6500, DancePhase.Go, DanceCue.Neutral), Sounds()));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("old-only")]
    [InlineData("weak")]
    [InlineData("late")]
    [InlineData("gap")]
    [InlineData("jump")]
    public void FeedbackRecoveryDoesNotRelaxAudioIntegrity(string fault)
    {
        var tracker = HandledFirstRound();
        var result = LaterRound(tracker, 4000, fault == "jump" ? 5 : 3, audio => fault switch
        {
            "missing" => audio[..2],
            "old-only" => [],
            "weak" => [audio[0] with { Similarity = .799 }, .. audio[1..]],
            "late" => [audio[0] with { ProcessedQpcMs = 6000 }, .. audio[1..]],
            "gap" => [.. audio, new(4600, 4600, "clock-gap", null, 0, 0, false, 4601)],
            _ => audio
        });
        Assert.False(result.Accepted);
        Assert.Empty(result.Cues);
        Assert.Equal("withheld", result.Status);
        Assert.Contains("Previous round outcome unconfirmed", result.Reason);
        Assert.Equal(3, tracker.Expected);
    }

    [Fact]
    public void RepeatedWatchWithoutResponseCannotGrantAnExtraCue()
    {
        var tracker = new PetDanceSoundSequence();
        tracker.Observe(new(3500, DancePhase.Watch, DanceCue.Neutral), []);
        var result = LaterRound(tracker, 4000, 4);
        Assert.False(result.Accepted);
        Assert.Equal("withheld", result.Status);
        Assert.DoesNotContain("outcome unconfirmed", result.Reason);
    }

    [Theory]
    [InlineData("Success", 5)]
    [InlineData("Failure", 4)]
    public void ExplicitFeedbackAfterRecoveredPreviewUsesObservedLengthOnlyOnce(string feedback, int nextExpected)
    {
        var tracker = HandledFirstRound();
        Assert.True(LaterRound(tracker, 4000, 4).Accepted);
        tracker.Observe(new(6200, Enum.Parse<DancePhase>(feedback), DanceCue.Neutral), []);
        Assert.Equal(nextExpected, tracker.Expected);
        tracker.Observe(new(6250, Enum.Parse<DancePhase>(feedback), DanceCue.Neutral), []);
        Assert.Equal(nextExpected, tracker.Expected);
        var result = LaterRound(tracker, 7000, nextExpected);
        Assert.True(result.Accepted);
        Assert.NotEqual("outcome-unconfirmed", result.Status);
    }

    [Fact]
    public void ConsecutiveMissingFeedbackUsesEachObservedLengthWithoutInventingOutcomes()
    {
        var tracker = HandledFirstRound();
        Assert.True(LaterRound(tracker, 4000, 4).Accepted);
        Assert.True(LaterRound(tracker, 8000, 5).Accepted);
        Assert.Equal(3, tracker.Expected);
        tracker.Observe(new(11000, DancePhase.Success, DanceCue.Neutral), []);
        Assert.Equal(6, tracker.Expected);
        tracker.Observe(new(11050, DancePhase.Success, DanceCue.Neutral), []);
        Assert.Equal(6, tracker.Expected);
    }

    [Fact]
    public void ResolvingWithoutSampledGoStillSeparatesFreshPreview()
    {
        var tracker = new PetDanceSoundSequence();
        tracker.Observe(new(1000, DancePhase.Watch, DanceCue.Neutral), []);
        tracker.Observe(new(3200, DancePhase.Resolving, DanceCue.Neutral), []);
        var result = LaterRound(tracker, 4000, 4);
        Assert.True(result.Accepted);
        Assert.Equal("outcome-unconfirmed", result.Status);
        Assert.Equal(3, tracker.Expected);
    }

    [Fact]
    public void FeedbackRecoveryNeverExceedsSevenCues()
    {
        var tracker = new PetDanceSoundSequence(7);
        Assert.True(LaterRound(tracker, 4000, 7).Accepted);
        Assert.True(LaterRound(tracker, 9000, 7).Accepted);
        Assert.False(LaterRound(tracker, 14000, 8).Accepted);
        Assert.Equal(7, tracker.Expected);
    }

    [Fact]
    public void SelectedVisualFallbackKeepsAudioFeedbackAccountingSynchronized()
    {
        var tracker = new PetDanceSoundSequence();
        Assert.Throws<InvalidOperationException>(() => tracker.NotifyResponseSelected(3));
        PetDanceSoundSequenceResult? result = null;
        foreach (var sample in Visual()) result = tracker.Observe(sample, Sounds()[..2]) ?? result;
        Assert.False(result!.Accepted);
        Assert.Throws<InvalidOperationException>(() => tracker.NotifyResponseSelected(4));
        tracker.NotifyResponseSelected(3);
        tracker.Observe(new(3300, DancePhase.Success, DanceCue.Neutral), []);
        Assert.Equal(4, tracker.Expected);
        tracker.Observe(new(3350, DancePhase.Success, DanceCue.Neutral), []);
        Assert.Equal(4, tracker.Expected);
        Assert.Throws<InvalidOperationException>(() => tracker.NotifyResponseSelected(4));
        Assert.True(LaterRound(tracker, 4000, 4).Accepted);
    }

    [Fact]
    public void SelectedModalityUsesFrozenRecoveryBoundsNotOwnProposedCount()
    {
        var tracker = HandledFirstRound();
        Assert.True(LaterRound(tracker, 4000, 4).Accepted);
        Assert.Throws<InvalidOperationException>(() => tracker.NotifyResponseSelected(5));
        tracker.NotifyResponseSelected(3);
        tracker.Observe(new(6200, DancePhase.Success, DanceCue.Neutral), []);
        Assert.Equal(4, tracker.Expected);
    }

    [Fact]
    public void NewPreviewSignalDoesNotResetOnTransientUnknownWithinWatch()
    {
        var tracker = new PetDanceSoundSequence();
        tracker.Observe(new(1000, DancePhase.Watch, DanceCue.Neutral), []);
        Assert.True(tracker.NewPreview);
        tracker.Observe(new(1050, DancePhase.Unknown, DanceCue.Unknown), []);
        tracker.Observe(new(1100, DancePhase.Watch, DanceCue.Neutral), []);
        Assert.False(tracker.NewPreview);
        Assert.Equal(1000, tracker.WatchStartMs);
        tracker.Observe(new(3000, DancePhase.Go, DanceCue.Neutral), Sounds());
        tracker.Observe(new(3100, DancePhase.Unknown, DanceCue.Unknown), []);
        tracker.Observe(new(4000, DancePhase.Watch, DanceCue.Neutral), []);
        Assert.True(tracker.NewPreview);
        Assert.Equal(4000, tracker.WatchStartMs);
        Assert.True(tracker.NeedsGoAssessment);
    }

    [Fact]
    public void OfflineReviewPreservesCounterfactualStatusAndRefusesOverwrite()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Maelstrom-Sound-Review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "live.json"); var output = Path.Combine(directory, "review.json");
            File.WriteAllText(source, JsonSerializer.Serialize(new
            {
                schema_version = 1,
                audio_required = true,
                audio_snapshot_complete = true,
                events = Visual().Select(sample => new { qpc_ms = sample.QpcMs, phase = sample.Phase.ToString(), cue = sample.Cue.ToString() }),
                audio_events = Sounds()
            }));
            PetDanceSoundSequence.Review(source, output);
            using var json = JsonDocument.Parse(File.ReadAllText(output));
            Assert.Equal(3, json.RootElement.GetProperty("proposed_keys").GetInt32());
            Assert.Equal(0, json.RootElement.GetProperty("inputs_sent").GetInt32());
            Assert.True(json.RootElement.GetProperty("counterfactual").GetBoolean());
            Assert.Throws<IOException>(() => PetDanceSoundSequence.Review(source, output));
        }
        finally { Directory.Delete(directory, true); }
    }
}
