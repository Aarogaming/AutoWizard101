using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class W101DemonstrationTests
{
    [Theory]
    [InlineData(1280, 720, 1280, 720)]
    [InlineData(1282, 759, 1282, 760)]
    [InlineData(1281, 759, 1282, 760)]
    public void EncoderRequestsEvenDimensionsWithoutClaimingAPixelMapping(int width, int height, int outputWidth, int outputHeight)
    {
        var size = VideoGeometry.EncodedSize(width, height);
        Assert.Equal(new Size(outputWidth, outputHeight), size);
        Assert.InRange(size.Width - width, 0, 1);
        Assert.InRange(size.Height - height, 0, 1);
    }

    [Fact]
    public void RateConversionIsExplicitAndCannotHideLostOrStretchedFootage()
    {
        double[] submitted = [1000, 1050, 1100];
        var converted = new VideoFileInspection(7, .117, [0, 16, 33, 50, 66, 83, 100]);
        Assert.Throws<InvalidDataException>(() => VideoGeometry.IsRateConverted(converted, submitted, false));
        Assert.True(VideoGeometry.IsRateConverted(converted, submitted, true));
        Assert.Throws<InvalidDataException>(() => VideoGeometry.IsRateConverted(converted with { DurationSeconds = 1 }, submitted, true));
        Assert.Throws<InvalidDataException>(() => VideoGeometry.IsRateConverted(converted with { Frames = 2 }, submitted, true));
        Assert.False(VideoGeometry.IsRateConverted(new(3, .117, [0, 50, 100]), submitted, false));
    }

    [Fact]
    public void BoundsAreFiniteAndExistingMovementScopeStaysSmall()
    {
        Assert.Throws<InvalidOperationException>(() => VideoGeometry.EncodedSize(0, 720));
        Assert.Throws<InvalidOperationException>(() => VideoGeometry.EncodedSize(9000, 9000));
        Assert.Throws<ArgumentOutOfRangeException>(() => W101DemonstrationRecorder.ValidateSeconds(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => W101DemonstrationRecorder.ValidateSeconds(181));
        W101DemonstrationRecorder.ValidateSeconds(180);
        Assert.DoesNotContain("End", GameplayInputRecorder.KeyNames);
        Assert.Contains("End", GameplayInputRecorder.KeyNamesFor(GameplayInputProfile.Wizard101Navigation));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameplayInputTimeline((GameplayInputProfile)42));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 5)]
    public void NavigationRecordingIsOptInAndReportsItsActualAllowlist(int profileValue, int expectedEvents)
    {
        var profile = (GameplayInputProfile)profileValue;
        var directory = Path.Combine(Path.GetTempPath(), "maelstrom-w101-input-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var timeline = new GameplayInputTimeline(profile); timeline.Start(100, 100, true);
            GameplayInputRecorder.RecordKeyboard(timeline, 0x100, 0x23, 0, 110, profile);
            GameplayInputRecorder.RecordKeyboard(timeline, 0x101, 0x23, 0, 120, profile);
            GameplayInputRecorder.RecordKeyboard(timeline, 0x100, 0x4D, 0, 130, profile);
            GameplayInputRecorder.RecordKeyboard(timeline, 0x101, 0x4D, 0, 140, profile);
            GameplayInputRecorder.RecordKeyboard(timeline, 0x100, 0x51, 0, 150, profile); // Q stays excluded.
            timeline.Stop(160);
            timeline.Save(directory, new(1, 2, 3, "game", "not retained"));
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "input-events.json")));
            Assert.Equal(expectedEvents, json.RootElement.GetProperty("Events").GetArrayLength());
            Assert.Equal(profile.ToString(), json.RootElement.GetProperty("InputProfile").GetString());
            Assert.Equal(GameplayInputRecorder.KeyNamesFor(profile), json.RootElement.GetProperty("KeyAllowlist").EnumerateArray().Select(x => x.GetString()));
        }
        finally { Directory.Delete(directory, true); }
    }
}
