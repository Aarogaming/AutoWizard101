using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class VideoFileInspectionTests
{
    [Fact]
    public void ReadsActualFrameCountAndDurationFromProbeReport()
    {
        var inspection = VideoFileInspection.Parse("""{"streams":[{"nb_read_frames":"2","duration":"4.500000"}],"frames":[{"best_effort_timestamp_time":"0.000"},{"best_effort_timestamp_time":"4.400"}]}""");
        Assert.Equal(2, inspection.Frames);
        Assert.Equal(4.5, inspection.DurationSeconds);
        Assert.Equal(new double[] { 0, 4400 }, inspection.FrameTimesMs);
        inspection.EnsureMatchesSubmission(2);
        Assert.Throws<InvalidDataException>(() => inspection.EnsureMatchesSubmission(3));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"streams\":[]}")]
    [InlineData("{\"streams\":[{}]}")]
    [InlineData("{\"streams\":[{\"nb_read_frames\":\"N/A\",\"duration\":\"4.5\"}]}")]
    [InlineData("{\"streams\":[{\"nb_read_frames\":\"3\",\"duration\":\"NaN\"}]}")]
    [InlineData("{\"streams\":[{\"nb_read_frames\":\"0\",\"duration\":\"4.5\"}]}")]
    public void RejectsMissingOrUnusableEvidence(string json)
    {
        Assert.Throws<InvalidDataException>(() => VideoFileInspection.Parse(json));
    }
}
