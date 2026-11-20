using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceMultimodalSequenceTests
{
    [Fact]
    public void VisualThenAudioFallbackThenVisualKeepsRoundLengthsSynchronized()
    {
        var visual = new PetDanceVisualSequence();
        var sound = new PetDanceSoundSequence();
        double start = 1000;
        for (var count = 3; count <= 5; count++)
        {
            var notes = new List<PetDanceAudioObservation>();
            for (var index = 0; index < count; index++)
            {
                var direction = index % 2 == 0 ? DanceCue.Left : DanceCue.Up;
                var onset = start + index * 500 + 100;
                notes.Add(new(onset, onset + 220, "cue-candidate", direction.ToString(), .95, .2, true, onset + 225));
                for (var offset = 0; offset < 500; offset += 50)
                {
                    var time = start + index * 500 + offset;
                    var cue = offset is >= 100 and <= 200 ? direction : DanceCue.Neutral;
                    if (count == 4 && index == 1 && offset is >= 100 and <= 200) cue = DanceCue.Unknown;
                    Assert.Null(visual.Observe(new(time, DancePhase.Watch, cue)));
                    Assert.Null(sound.Observe(new(time, DancePhase.Watch, cue), []));
                }
            }
            var go = start + count * 500;
            var visualResult = visual.Observe(new(go, DancePhase.Go, DanceCue.Neutral));
            var soundResult = sound.Observe(new(go, DancePhase.Go, DanceCue.Neutral), notes);
            Assert.NotNull(visualResult); Assert.NotNull(soundResult);
            var selected = PetDanceResponseSelection.Select(visualResult.Cues, visualResult.Accepted,
                soundResult.Cues, soundResult.Accepted);
            Assert.True(selected.Accepted);
            Assert.Equal(count == 4 ? "sound-fallback" : "visual", selected.Source);
            Assert.Equal(count, selected.Cues.Length);
            visual.NotifyResponseSelected(selected.Cues.Length);
            sound.NotifyResponseSelected(selected.Cues.Length);
            Assert.Null(visual.Observe(new(go + 100, DancePhase.Success, DanceCue.Neutral)));
            Assert.Null(sound.Observe(new(go + 100, DancePhase.Success, DanceCue.Neutral), []));
            Assert.Equal(count + 1, visual.Expected);
            Assert.Equal(visual.Expected, sound.Expected);
            start = go + 200;
        }
    }
}
