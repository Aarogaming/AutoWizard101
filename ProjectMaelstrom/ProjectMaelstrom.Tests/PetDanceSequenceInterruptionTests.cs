using ProjectMaelstrom.Utilities;
namespace ProjectMaelstrom.Tests;

public sealed class PetDanceSequenceInterruptionTests
{
    [Theory]
    [InlineData("Go")]
    [InlineData("Failure")]
    [InlineData("Loading")]
    public void InterruptedPreviewNeedsKnownBoundaryBeforeNewWatch(string boundaryName)
    {
        var boundary = Enum.Parse<DancePhase>(boundaryName);
        var visual = new PetDanceVisualSequence(4);
        var sound = new PetDanceSoundSequence(4);
        visual.Observe(new(100, DancePhase.Watch, DanceCue.Up));
        sound.Observe(new(100, DancePhase.Watch, DanceCue.Up), []);
        visual.InterruptPreview(); sound.InterruptPreview();
        foreach (var sample in new[] { (200d, DancePhase.Watch), (300d, DancePhase.Unknown), (400d, DancePhase.Watch) })
        {
            Assert.Null(visual.Observe(new(sample.Item1, sample.Item2, DanceCue.Up)));
            Assert.Null(sound.Observe(new(sample.Item1, sample.Item2, DanceCue.Up), []));
            Assert.False(visual.NewPreview); Assert.False(sound.NewPreview);
            Assert.Null(visual.WatchStartMs); Assert.Null(sound.WatchStartMs);
        }
        visual.Observe(new(500, boundary, DanceCue.Unknown));
        sound.Observe(new(500, boundary, DanceCue.Unknown), []);
        visual.Observe(new(600, DancePhase.Watch, DanceCue.Left));
        sound.Observe(new(600, DancePhase.Watch, DanceCue.Left), []);
        Assert.True(visual.NewPreview); Assert.True(sound.NewPreview);
        Assert.Equal(600d, visual.WatchStartMs); Assert.Equal(600d, sound.WatchStartMs);
        Assert.Equal(4, visual.Expected); Assert.Equal(4, sound.Expected);
        Assert.Throws<InvalidDataException>(() => visual.Observe(new(550, DancePhase.Watch, DanceCue.Up)));
        Assert.Throws<InvalidDataException>(() => sound.Observe(new(550, DancePhase.Watch, DanceCue.Up), []));
    }
}
