using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class VisualStateRecognizerTests
{
    [Fact]
    public void FixedRegionIgnoresChangesOutsideSelectedArea()
    {
        using var red = Solid(Color.Red);
        using var blue = Solid(Color.Blue);
        var region = new Rectangle(10, 10, 40, 30);
        using var redCrop = red.Clone(region, red.PixelFormat);
        using var blueCrop = blue.Clone(region, blue.PixelFormat);
        var model = new VisualStateRecognizer("test", "hash", new[] {
            new VisualExample("red", "a", 160, 100, VisualStateRecognizer.Features(redCrop), region),
            new VisualExample("blue", "b", 160, 100, VisualStateRecognizer.Features(blueCrop), region)
        });
        using var changed = Solid(Color.Lime);
        using (var graphics = Graphics.FromImage(changed)) graphics.FillRectangle(Brushes.Red, region);
        Assert.Equal("red", model.Predict(changed).State);
        using (var graphics = Graphics.FromImage(changed)) graphics.FillRectangle(Brushes.Blue, region);
        Assert.Equal("blue", model.Predict(changed).State);
        using (var graphics = Graphics.FromImage(changed)) graphics.FillRectangle(Brushes.Yellow, region);
        Assert.Null(model.Predict(changed).State);
    }
    private static Bitmap Solid(Color color, int width = 160, int height = 100)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    [Fact]
    public void MatchingIsOrderIndependentAndRejectsUnknownShapeAndAmbiguity()
    {
        using var red = Solid(Color.Red);
        using var blue = Solid(Color.Blue);
        using var green = Solid(Color.Lime);
        using var wrongShape = Solid(Color.Red, 100, 160);
        var examples = new[] { new VisualExample("red", "red", 160, 100, VisualStateRecognizer.Features(red)), new VisualExample("blue", "blue", 160, 100, VisualStateRecognizer.Features(blue)) };
        var model = new VisualStateRecognizer("Test", "hash", examples);
        Assert.Equal("blue", model.Predict(blue).State);
        Assert.Equal("red", model.Predict(red).State);
        Assert.Equal("blue", model.Predict(blue).State);
        Assert.Null(model.Predict(green).State);
        Assert.Null(model.Predict(wrongShape).State);
        var ambiguous = new VisualStateRecognizer("Test", "hash", new[] { examples[0], examples[0] with { State = "other", Hash = "different-file" } });
        Assert.Null(ambiguous.Predict(red).State);
        var sameImageDifferentRegions = new VisualStateRecognizer("Test", "hash", new[] { examples[0] with { Region = new Rectangle(0, 0, 40, 40) }, examples[0] with { State = "other", Region = new Rectangle(40, 0, 40, 40) } });
        Assert.Equal(2, sameImageDifferentRegions.ExampleCount);
        Assert.Throws<InvalidDataException>(() => new VisualStateRecognizer("Test", "hash", new[] { examples[0], examples[0] with { State = "conflict" } }));
        Assert.Throws<InvalidDataException>(() => new VisualStateRecognizer("Test", "hash", new[] { examples[0] }));
    }

    [Fact]
    public void PackLearningChecksImagesAndEvaluationExcludesTrainingDuplicates()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-vision-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var red = Solid(Color.Red);
            using var blue = Solid(Color.Blue);
            var events = new List<string>();
            foreach (var pair in new[] { ("red", red), ("blue", blue) })
            {
                var filename = pair.Item1 + ".png";
                pair.Item2.Save(Path.Combine(root, filename), ImageFormat.Png);
                var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, filename))));
                events.Add(JsonSerializer.Serialize(new { kind = "observation", payload = new { state = pair.Item1, label_source = "human", image = filename, sha256 = hash, width = 160, height = 100 } }));
            }
            File.WriteAllLines(Path.Combine(root, "events.jsonl"), events);
            File.WriteAllText(Path.Combine(root, "session.json"), "{\"schema_version\":1,\"events_file\":\"events.jsonl\"}");
            var packPath = Path.Combine(root, "pack.json");
            File.WriteAllText(packPath, "{\"schema_version\":1,\"status\":\"draft\",\"title\":\"Colors\",\"version\":\"1\",\"fixtures\":[\"session.json\"]}");
            var model = VisualStateRecognizer.Learn(packPath);
            Assert.Equal(2, model.StateCount);
            Assert.Equal("red", model.Predict(red).State);
            File.WriteAllText(packPath, "{\"schema_version\":2,\"status\":\"draft\",\"title\":\"Colors\",\"version\":\"2\",\"fixtures\":[\"session.json\"],\"actions\":[{\"id\":\"left\",\"title\":\"Move left\",\"key\":\"LEFT\",\"duration_ms\":60}],\"skills\":[]}");
            var withActions = VisualStateRecognizer.Learn(packPath);
            Assert.Equal(2, withActions.StateCount);
            Assert.Equal("red", withActions.Predict(red).State);
            Assert.NotEqual(model.PackHash, withActions.PackHash);
            var schemaTwo = File.ReadAllText(packPath);
            File.WriteAllText(packPath, schemaTwo.Replace("\"schema_version\":2", "\"schema_version\":3"));
            Assert.Throws<InvalidDataException>(() => VisualStateRecognizer.Learn(packPath));
            File.WriteAllText(packPath, schemaTwo);
            var recording = TeachingRecording.Start(root, "Test", "Test");
            recording.AddFrame(red);
            recording.Finish();
            recording.Label(0, "red");
            using var report = JsonDocument.Parse(JsonSerializer.Serialize(model.Evaluate(recording)));
            Assert.Equal(0, report.RootElement.GetProperty("tested").GetInt32());
            Assert.Equal(1, report.RootElement.GetProperty("excluded_training_duplicates").GetInt32());
            using var slightlyDifferent = Solid(Color.FromArgb(250, 2, 2));
            var heldOut = TeachingRecording.Start(root, "Test", "Test");
            heldOut.AddFrame(slightlyDifferent);
            heldOut.Finish();
            heldOut.Label(0, "red");
            using var checkedReport = JsonDocument.Parse(JsonSerializer.Serialize(model.Evaluate(heldOut)));
            Assert.Equal(1, checkedReport.RootElement.GetProperty("correct").GetInt32());
            File.WriteAllText(Path.Combine(root, "red.png"), "tampered");
            Assert.Throws<InvalidDataException>(() => VisualStateRecognizer.Learn(packPath));
            File.WriteAllText(packPath, "{\"schema_version\":1,\"status\":\"draft\",\"fixtures\":[\"../outside.json\"]}");
            Assert.Throws<InvalidDataException>(() => VisualStateRecognizer.Learn(packPath));
        }
        finally { Directory.Delete(root, true); }
    }
}
