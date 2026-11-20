using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class GameplayInputTimelineTests
{
    [Fact]
    public void OnlyFocusedInputsWithinCaptureAreSavedOnVideoClock()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maelstrom-input-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var timeline = new GameplayInputTimeline();
            timeline.Input(99, true, "key_down", "W");
            timeline.Start(100, 102, false);
            timeline.Input(103, false, "key_down", "A");
            timeline.Focus(105, true);
            timeline.Input(111.5, true, "key_down", "W");
            timeline.Input(116, true, "mouse_down", "Left", 12, 18);
            timeline.Input(120, false, "key_up", "W");
            timeline.Stop(130);
            timeline.Input(131, true, "key_down", "D");
            timeline.Save(directory, new GameWindow(1, 2, 3, "game", "private title"));
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "input-events.json")));
            var root = doc.RootElement;
            Assert.Equal(2, root.GetProperty("FirstFrameDeliveryOffsetMs").GetDouble());
            var events = root.GetProperty("Events").EnumerateArray().ToArray();
            Assert.Equal(new[] { "focus_lost", "focus_gained", "key_down", "mouse_down", "focus_lost" }, events.Select(e => e.GetProperty("Type").GetString()));
            Assert.Equal(11.5, events[2].GetProperty("TimeMs").GetDouble());
            Assert.Equal("W", events[2].GetProperty("Control").GetString());
            Assert.Equal(12, events[3].GetProperty("X").GetInt32());
            Assert.DoesNotContain("private title", root.ToString());
            Assert.False(root.GetProperty("Truncated").GetBoolean());
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NoInputFileIsWrittenIfVideoNeverStarts()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maelstrom-input-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var timeline = new GameplayInputTimeline();
            timeline.Stop(10);
            timeline.Start(11, 12, true);
            timeline.Save(directory, new GameWindow(1, 2, 3, "game", "game"));
            Assert.False(File.Exists(Path.Combine(directory, "input-events.json")));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void VerifiedLogIsBoundToTheExactVideoAndNeverOverwrites()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maelstrom-input-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var video = Path.Combine(directory, "capture.mp4");
            File.WriteAllText(video, "test video");
            var timeline = new GameplayInputTimeline();
            timeline.Start(100, 101, true);
            timeline.Input(105, true, "key_down", "Left", injected: true);
            timeline.Stop(110);
            var target = new GameWindow(1, 2, 3, "game", "game");
            timeline.Save(directory, target, videoVerified: true);
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "input-events.json")));
            var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(video)));
            Assert.Equal(expected, doc.RootElement.GetProperty("VideoSha256").GetString());
            Assert.True(doc.RootElement.GetProperty("Events")[1].GetProperty("Injected").GetBoolean());
            Assert.Throws<IOException>(() => timeline.Save(directory, target, videoVerified: true));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void KeyboardMappingRecordsAllowedDownAndUpButNotOtherText()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maelstrom-input-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var timeline = new GameplayInputTimeline();
            timeline.Start(100, 100, true);
            GameplayInputRecorder.RecordKeyboard(timeline, 0x100, 0x57, 0x10, 110); // Injected W down.
            GameplayInputRecorder.RecordKeyboard(timeline, 0x101, 0x57, 0x10, 120);
            GameplayInputRecorder.RecordKeyboard(timeline, 0x100, 0x51, 0, 125); // Q is outside the allowlist.
            timeline.Stop(130);
            timeline.Save(directory, new GameWindow(1, 2, 3, "game", "game"));
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "input-events.json")));
            var events = doc.RootElement.GetProperty("Events").EnumerateArray().ToArray();
            Assert.Equal(3, events.Length);
            Assert.Equal("key_down", events[1].GetProperty("Type").GetString());
            Assert.Equal("key_up", events[2].GetProperty("Type").GetString());
            Assert.All(events.Skip(1), e => Assert.True(e.GetProperty("Injected").GetBoolean()));
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public void MouseMappingKeepsButtonTransitionsAndSignedWheel()
    {
        Assert.Equal(("mouse_down", "Left", (int?)null), GameplayInputRecorder.ClassifyMouse(0x201, 0));
        Assert.Equal(("mouse_up", "Right", (int?)null), GameplayInputRecorder.ClassifyMouse(0x205, 0));
        Assert.Equal(("mouse_wheel", (string?)null, (int?)-120), GameplayInputRecorder.ClassifyMouse(0x20A, 0xFF880000));
        Assert.Null(GameplayInputRecorder.ClassifyMouse(0x20B, 0).Type);
    }
}
