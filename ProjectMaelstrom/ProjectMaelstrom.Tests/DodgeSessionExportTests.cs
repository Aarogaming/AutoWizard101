using System.Drawing;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class DodgeSessionExportTests
{
    [Fact]
    public async Task SavedFramesProduceTimestampedReplayWithoutInputs()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-dodge-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = TeachingRecording.Start(root, "Dodge", "test");
            using var frame = new Bitmap(480, 750);
            using (var graphics = Graphics.FromImage(frame)) graphics.Clear(Color.FromArgb(55, 95, 94));
            recording.AddImportedFrame(frame, 100);
            using (var graphics = Graphics.FromImage(frame))
            using (var player = new SolidBrush(Color.FromArgb(150, 130, 130)))
            using (var threat = new SolidBrush(Color.FromArgb(210, 50, 70)))
            {
                graphics.FillEllipse(player, 210, 380, 54, 54);
                graphics.FillEllipse(Brushes.White, 224, 394, 26, 26);
                graphics.FillEllipse(threat, 340, 385, 30, 30);
            }
            recording.AddImportedFrame(frame, 350);
            recording.Finish();
            var path = await DodgeSessionExport.RunAsync(recording, root, CancellationToken.None);
            using var manifest = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(1, manifest.RootElement.GetProperty("schema_version").GetInt32());
            var rows = File.ReadAllLines(Path.Combine(Path.GetDirectoryName(path)!, "events.jsonl"));
            Assert.Equal(2, rows.Length);
            using var first = JsonDocument.Parse(rows[0]);
            using var second = JsonDocument.Parse(rows[1]);
            Assert.Equal("unknown", first.RootElement.GetProperty("payload").GetProperty("state").GetString());
            Assert.Equal("dodge-advice-left", second.RootElement.GetProperty("payload").GetProperty("state").GetString());
            Assert.Equal(350, second.RootElement.GetProperty("time_ms").GetDouble());
            Assert.All(rows, row => Assert.DoesNotContain("action_receipt", row));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CancellationDoesNotCreateSession()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-dodge-cancel-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recording = TeachingRecording.Start(root, "Dodge", "test");
            using var image = new Bitmap(480, 750);
            recording.AddImportedFrame(image, 0);
            recording.Finish();
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DodgeSessionExport.RunAsync(recording, root, cancel.Token));
            Assert.False(Directory.Exists(Path.Combine(root, "sessions")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
