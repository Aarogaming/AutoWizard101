using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101TraceTailTests
{
    [Fact]
    public void OversizedUnterminatedRecordEndsTransport()
    {
        string path = Path.GetTempFileName();
        try
        {
            var lines = new List<string>();
            using var tail = new W101TraceTail(path, lines.Add);
            File.WriteAllText(path, new string('x', 262145));
            for (int i = 0; i < 4; i++) tail.Poll();
            Assert.Throws<InvalidDataException>(tail.Poll);
            Assert.Empty(lines);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void PartialRecordsWaitForNewlineAndDoNotReplay()
    {
        string path = Path.GetTempFileName();
        try
        {
            var lines = new List<string>();
            using var tail = new W101TraceTail(path, lines.Add);
            File.WriteAllText(path, "{\"x\":"); tail.Poll(); Assert.Empty(lines);
            File.AppendAllText(path, "1}\n"); tail.Poll(); tail.Poll();
            Assert.Equal(new[] { "{\"x\":1}" }, lines);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void TruncationEndsTransport()
    {
        string path = Path.GetTempFileName();
        try
        {
            using var tail = new W101TraceTail(path, _ => { });
            File.WriteAllText(path, "{}\n"); tail.Poll(); File.WriteAllText(path, "");
            Assert.Throws<InvalidDataException>(tail.Poll);
            Assert.Throws<InvalidOperationException>(tail.Poll);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void InvalidEncodingIsNotDelivered()
    {
        string path = Path.GetTempFileName();
        try
        {
            var lines = new List<string>();
            using var tail = new W101TraceTail(path, lines.Add);
            File.WriteAllBytes(path, [255, 10]);
            Assert.Throws<System.Text.DecoderFallbackException>(tail.Poll); Assert.Empty(lines);
        }
        finally { File.Delete(path); }
    }
}
