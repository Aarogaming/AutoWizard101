using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceAudioAvailabilityTests
{
    private static readonly PetDanceSoundSequenceResult Sound = new("complete", ["up", "left", "down"], true, "test", []);
    private static readonly PetDanceVisualSequenceResult Visual = new("visual-complete", ["up", "left", "down"], true, "test", []);

    [Fact]
    public void FailedBackupKeepsQualifiedVisualResponseButNeverReusesSound()
    {
        var audio = new PetDanceAudioAvailability(true, true);
        Assert.Equal("sound-fallback", audio.Select(null, Sound).Source);
        Assert.True(audio.Observe(false));
        Assert.True(audio.MayContinue);
        Assert.Equal("visual", audio.Select(Visual, Sound).Source);
        Assert.False(audio.Select(null, Sound).Accepted);
        Assert.False(audio.Observe(true));
        Assert.False(audio.Available);
        Assert.False(audio.Select(null, Sound).Accepted);
    }

    [Fact]
    public void SoundLedLegacyPlayerStillRequiresHealthyAudio()
    {
        var audio = new PetDanceAudioAvailability(true, false);
        Assert.True(audio.Required);
        Assert.True(audio.Observe(false));
        Assert.False(audio.MayContinue);
        Assert.False(audio.Select(null, Sound).Accepted);
    }

    [Fact]
    public void DisabledAudioNeverProvidesAResponse()
    {
        var audio = new PetDanceAudioAvailability(false, true);
        Assert.False(audio.Observe(false));
        Assert.True(audio.MayContinue);
        Assert.False(audio.Select(null, Sound).Accepted);
        Assert.Equal("visual", audio.Select(Visual, null).Source);
    }
}
