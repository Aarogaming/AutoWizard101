using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceSoundOnsetRecoveryTests
{
    private static PetDanceAudioObservation Sound(double at, string direction)
        => new(at, at + 220, "cue-candidate", direction, .95, .2, true, at + 225);

    private static PetDanceAudioObservation[] Sounds()
        => [Sound(1150, "Left"), Sound(1650, "Down"), Sound(2150, "Right")];

    private static PetDanceSoundSequenceResult Assess(DancePhase preceding = DancePhase.Success,
        double watchAt = 1500, PetDanceAudioObservation[]? audio = null, DancePhase? intermediary = null)
    {
        var tracker = new PetDanceSoundSequence();
        tracker.Observe(new(1000, preceding, DanceCue.Neutral), []);
        if (intermediary.HasValue) tracker.Observe(new(1100, intermediary.Value, DanceCue.Unknown), []);
        tracker.Observe(new(watchAt, DancePhase.Watch, DanceCue.Neutral), []);
        return Assert.IsType<PetDanceSoundSequenceResult>(tracker.Observe(new(2700, DancePhase.Go, DanceCue.Neutral), audio ?? Sounds()));
    }

    [Fact]
    public void RecordedHugeCaptureGapRecoversFourActualSoundsWithoutInventingVisualFrames()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "TestData", "PetDance", "huge-watch-onset-gap.json")));
        var root = fixture.RootElement;
        var audio = JsonSerializer.Deserialize<PetDanceAudioObservation[]>(root.GetProperty("audio_events").GetRawText())!;
        var tracker = new PetDanceSoundSequence(root.GetProperty("expected").GetInt32());
        PetDanceSoundSequenceResult? result = null;
        foreach (var row in root.GetProperty("events").EnumerateArray())
            result = tracker.Observe(new(row.GetProperty("qpc_ms").GetDouble(),
                Enum.Parse<DancePhase>(row.GetProperty("phase").GetString()!),
                Enum.Parse<DanceCue>(row.GetProperty("cue").GetString()!)), audio) ?? result;
        Assert.NotNull(result);
        Assert.True(result.Accepted);
        Assert.Equal(new[] { "left", "left", "down", "right" }, result.Cues);
        Assert.Equal(1994625.8044, result.AudioOnsetBoundaryMs!.Value, 3);
        Assert.Equal(1995198.0117, result.ObservedWatchStartMs!.Value, 3);
        Assert.Equal(0, result.Evidence[0].MatchingFrames);
        Assert.Equal("visual-unclear-audio-preferred", result.Evidence[0].VisualStatus);
        Assert.Contains("before the first captured Watch frame", result.Reason);
    }

    [Theory]
    [InlineData((int)DancePhase.Success)]
    [InlineData((int)DancePhase.Failure)]
    [InlineData((int)DancePhase.Resolving)]
    public void OnlyExplicitImmediateResultBoundaryAllowsBoundedAudioRecovery(int preceding)
    {
        var result = Assess((DancePhase)preceding);
        Assert.True(result.Accepted);
        Assert.Equal(1000, result.AudioOnsetBoundaryMs);
        Assert.Equal(1500, result.ObservedWatchStartMs);
        Assert.Equal(new[] { "left", "down", "right" }, result.Cues);
        Assert.All(result.Evidence, item => Assert.Equal(0, item.MatchingFrames));
    }

    [Theory]
    [InlineData((int)DancePhase.Unknown)]
    [InlineData((int)DancePhase.Loading)]
    [InlineData((int)DancePhase.Ready)]
    [InlineData((int)DancePhase.Go)]
    public void OtherOrInterveningScreenStatesCannotExpandTheAudioWindow(int phase)
    {
        Assert.False(Assess((DancePhase)phase).Accepted);
        Assert.False(Assess(intermediary: (DancePhase)phase).Accepted);
        Assert.Null(Assess(intermediary: (DancePhase)phase).AudioOnsetBoundaryMs);
    }

    [Fact]
    public void OnsetRecoveryHasAHard750MillisecondLimit()
    {
        Assert.True(Assess(watchAt: 1750).Accepted);
        Assert.False(Assess(watchAt: 1750.001).Accepted);
        Assert.False(Assess(watchAt: 2000).Accepted);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("weak")]
    [InlineData("late")]
    [InlineData("gap-before-Watch")]
    [InlineData("at-boundary")]
    [InlineData("extra")]
    public void RecoveredAudioRetainsCountCausalityAndContinuityRequirements(string fault)
    {
        var sounds = Sounds();
        var audio = fault switch
        {
            "missing" => sounds[1..],
            "weak" => [sounds[0] with { Similarity = .79 }, .. sounds[1..]],
            "late" => [sounds[0] with { ProcessedQpcMs = 2701 }, .. sounds[1..]],
            "gap-before-Watch" => [.. sounds, new(1250, 1250, "clock-gap", null, 0, 0, false, 1251)],
            "at-boundary" => [Sound(1000, "Left"), .. sounds[1..]],
            "extra" => [.. sounds, Sound(2400, "Up")],
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        Assert.False(Assess(audio: audio).Accepted);
    }

    [Fact]
    public void EarlierSoundsStayOutsideTheObservedBoundary()
    {
        var result = Assess(audio: [Sound(950, "Up"), .. Sounds()]);
        Assert.True(result.Accepted);
        Assert.Equal(new[] { "left", "down", "right" }, result.Cues);
    }
}
