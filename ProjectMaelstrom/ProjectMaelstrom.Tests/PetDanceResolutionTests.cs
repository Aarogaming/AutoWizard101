using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.InteropServices;
using ProjectMaelstrom.Utilities;
using Xunit.Abstractions;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceResolutionMapTests
{
    internal static readonly PetDanceCaptureGeometry Calibration = new(new Size(1282, 759), new Rectangle(1, 38, 1280, 720), 120, "test-measured-calibration");

    [Theory]
    [InlineData(1280, 720, 1, 38, 120)]
    [InlineData(1920, 1080, 8, 31, 96)]
    [InlineData(2560, 1440, 12, 44, 144)]
    public void SameAspectMappingUsesClientPixelsAndMeasuredBorders(int width, int height, int left, int top, int dpi)
    {
        var source = new PetDanceCaptureGeometry(new(width + left + 9, height + top + 5), new(left, top, width, height), dpi, "test-measured-source");
        var map = new PetDanceResolutionMap(Calibration, source);
        Assert.Equal(width / 1280d, map.ResolutionScale);
        Assert.Equal(new PointF(left, top), map.ToSource(new(1, 38)));
        Assert.Equal(new PointF(left + width, top + height), map.ToSource(new(1281, 758)));
        foreach (var point in new[] { new PointF(1, 38), new(641, 398), new(777.25f, 631.5f), new(1281, 758) })
        {
            var restored = map.ToCalibration(map.ToSource(point));
            Assert.InRange(Math.Abs(restored.X - point.X), 0, .001);
            Assert.InRange(Math.Abs(restored.Y - point.Y), 0, .001);
        }
        var grid = map.Grid(4);
        Assert.True(source.ClientBounds.Contains(grid.SourceBounds));
        Assert.Equal(top + height * 3 / 4, grid.SourceBounds.Top);
        Assert.Equal(left + width / 3, grid.SourceBounds.Left);
        Assert.InRange(grid.SearchPixels, 1, PetDanceResolutionMap.MaximumSearchPixels);
    }

    [Fact]
    public void LargeBroadGridsStayBoundedWithOneUniformSamplingStep()
    {
        var map = new PetDanceResolutionMap(Calibration, new(new(3856, 2208), new(8, 40, 3840, 2160), 192, "test-4k"));
        foreach (var rows in new[] { 2, 4 })
        {
            var grid = map.Grid(rows);
            Assert.True(grid.SamplingStep > 1);
            Assert.InRange(grid.SearchPixels, 1, 250000);
            Assert.InRange(grid.SearchSize.Width * grid.SamplingStep - grid.SourceBounds.Width, 0, grid.SamplingStep - 1);
            Assert.InRange(grid.SearchSize.Height * grid.SamplingStep - grid.SourceBounds.Height, 0, grid.SamplingStep - 1);
        }
    }

    [Fact]
    public void GeometryCannotGuessAspectLetterboxingChromeOrDpi()
    {
        Assert.Throws<InvalidDataException>(() => new PetDanceResolutionMap(Calibration,
            new(new(1920, 1200), new(0, 0, 1920, 1200), 96, "16:10-needs-profile")));
        foreach (var invalid in new[]
        {
            Calibration with { ClientBounds = new(-1, 0, 1280, 720) },
            Calibration with { ClientBounds = new(1, 40, 1280, 720) },
            Calibration with { ClientBounds = Rectangle.Empty },
            Calibration with { CaptureSize = Size.Empty },
            Calibration with { Dpi = 0 }, Calibration with { Evidence = "" }
        }) Assert.Throws<InvalidDataException>(() => new PetDanceResolutionMap(Calibration, invalid));
        var map = new PetDanceResolutionMap(Calibration, Calibration);
        Assert.Throws<InvalidDataException>(() => map.ToSource(new(100, 12)));
        Assert.Throws<InvalidDataException>(() => map.ToCalibration(new(float.NaN, 50)));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Grid(3));
    }
}

public sealed class PetDanceRecordedResolutionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("watch-down", 1280, 720, 1, 38)]
    [InlineData("watch-down", 1920, 1080, 8, 31)]
    [InlineData("watch-down", 2560, 1440, 12, 44)]
    [InlineData("go", 1280, 720, 1, 38)]
    [InlineData("go", 1920, 1080, 8, 31)]
    [InlineData("go", 2560, 1440, 12, 44)]
    public void SavedGameplayPixelsRetainTheirPhaseThroughDeclaredClientTransforms(string label, int width, int height, int left, int top)
    {
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "TestData", "PetDance", "Resolution");
        using var provenance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot, "provenance.json")));
        foreach (var entry in provenance.RootElement.GetProperty("entries").EnumerateArray())
        {
            var bytes = File.ReadAllBytes(Path.Combine(fixtureRoot, entry.GetProperty("fixture").GetString()!));
            Assert.Equal(entry.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)));
        }
        using var canonical = new Bitmap(1282, 759, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(canonical))
        using (var grid = new Bitmap(Path.Combine(fixtureRoot, "recorded-" + label + "-grid.png")))
        using (var upper = new Bitmap(Path.Combine(fixtureRoot, "recorded-" + label + "-top.png")))
        {
            graphics.Clear(Color.CornflowerBlue);
            graphics.DrawImageUnscaled(grid, 427, 379);
            graphics.DrawImageUnscaled(upper, 512, 100);
        }
        var geometry = new PetDanceCaptureGeometry(new(width + left + 5, height + top + 4),
            new(left, top, width, height), 144, "synthetic-transform-of-recorded-pixels-not-native-capture");
        var mapping = new PetDanceResolutionMap(PetDanceResolutionMapTests.Calibration, geometry);
        using var source = new Bitmap(geometry.CaptureSize.Width, geometry.CaptureSize.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(source))
        {
            graphics.Clear(Color.Magenta);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(canonical, geometry.ClientBounds, PetDanceResolutionMapTests.Calibration.ClientBounds, GraphicsUnit.Pixel);
        }
        using var vision = new PetDancePanelVision(PetDancePreset.Load(AppContext.BaseDirectory).ProfilePath);
        var expected = label == "go" ? DancePhase.Go : DancePhase.Watch;
        var durations = new List<double>();
        var allocatedStart = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 15; i++)
        {
            var watch = Stopwatch.StartNew();
            using var panel = vision.Extract(source, mapping, mapping.ResolutionScale);
            var observed = vision.Read(panel, 100 + i * 33);
            watch.Stop();
            Assert.Equal(expected, observed.Phase);
            if (label == "watch-down") Assert.Equal(DanceCue.Down, observed.Cue);
            Assert.True(observed.Layout?.Qualified);
            if (i >= 3) durations.Add(watch.Elapsed.TotalMilliseconds);
        }
        var bytesPerFrame = (GC.GetAllocatedBytesForCurrentThread() - allocatedStart) / 15;
        durations.Sort();
        output.WriteLine($"Recorded {label}; synthetic client {width}x{height}; 12 warmed extracts: mean {durations.Average():F3} ms, p95 {durations[(int)Math.Ceiling(durations.Count * .95) - 1]:F3} ms; managed allocation {bytesPerFrame} bytes/frame. Excludes capture and source image construction; not live qualification.");
    }
}

public sealed class PetDanceNativeResolutionLayoutTests
{
    [Theory]
    [InlineData(1280, 720, 1, 38, 1d)]
    [InlineData(1920, 1080, 8, 31, 1d)]
    [InlineData(2560, 1440, 12, 44, 1d)]
    [InlineData(1920, 1080, 8, 31, 1.5d)]
    [InlineData(2560, 1440, 12, 44, 2d)]
    public void NativeGridRecognizesIndependentUiScalesWithoutResizingTheGame(int width, int height, int left, int top, double multiplier)
    {
        using var fixture = new Fixture();
        var sourceGeometry = Geometry(width, height, left, top);
        var map = new PetDanceResolutionMap(PetDanceResolutionMapTests.Calibration, sourceGeometry);
        var layout = fixture.Layout();
        foreach (var uiScale in new[] { 1d, 1.5 })
            foreach (var phase in new[] { DancePhase.Watch, DancePhase.Go, DancePhase.Resolving })
            {
                var physicalScale = multiplier * uiScale;
                using var source = fixture.Scene(sourceGeometry, phase, physicalScale);
                Assert.True(layout.TryExtract(source, map, out var panel, out var seen, multiplier));
                using (panel)
                {
                    Assert.NotNull(seen);
                    Assert.True(seen.NativeResolution);
                    Assert.True(seen.Qualified);
                    Assert.Equal(phase, seen.Phase);
                    Assert.Equal(physicalScale, seen.Scale);
                    Assert.Equal(new Size(256, 140), panel!.Size);
                    Assert.InRange(seen.SearchPixels, 1, 250000);
                    // The upper panel comes from its measured client-top anchor,
                    // independently of the bottom phase plate's screen position.
                    var topPixel = panel.GetPixel(124, 18);
                    Assert.True(topPixel.R > 180 && topPixel.G < 60);
                }
            }
    }

    [Fact]
    public void SearchRefinementWorksAboveTheDirectPixelBudget()
    {
        using var fixture = new Fixture();
        var geometry = Geometry(2560, 1440, 8, 40);
        var map = new PetDanceResolutionMap(PetDanceResolutionMapTests.Calibration, geometry);
        Assert.True(map.Grid(4).SamplingStep > 1);
        using var source = fixture.Scene(geometry, DancePhase.Go, 3);
        Assert.True(fixture.Layout().TryExtract(source, map, out var panel, out var seen, 2));
        using (panel)
        {
            Assert.True(seen!.SearchSamplingStep > 1);
            Assert.Equal(DancePhase.Go, seen.Phase);
        }
    }

    [Fact]
    public void MissingAmbiguousOrUnsupportedUiCannotReuseAPreviousPhase()
    {
        using var fixture = new Fixture();
        var geometry = Geometry(1920, 1080, 8, 31);
        var map = new PetDanceResolutionMap(PetDanceResolutionMapTests.Calibration, geometry);
        var layout = fixture.Layout();
        using var go = fixture.Scene(geometry, DancePhase.Go, 1);
        Assert.True(layout.TryExtract(go, map, out var panel, out _)); panel!.Dispose();
        using var blank = new Bitmap(geometry.CaptureSize.Width, geometry.CaptureSize.Height);
        Assert.False(layout.TryExtract(blank, map, out panel, out var seen));
        Assert.Null(panel); Assert.Null(seen);
        using var noRing = fixture.Scene(geometry, DancePhase.Go, 1, button: false);
        Assert.False(layout.TryExtract(noRing, map, out _, out _));
        using var unsupported = fixture.Scene(geometry, DancePhase.Go, 1.27);
        Assert.False(layout.TryExtract(unsupported, map, out _, out _));
        using var ambiguous = fixture.Scene(geometry, DancePhase.Go, 1, second: true);
        Assert.False(layout.TryExtract(ambiguous, map, out panel, out seen));
        Assert.Null(panel); Assert.Equal("ambiguous-native-layout", seen!.Status);
    }

    [Fact]
    public void BorderOrFrameMismatchAndInvalidScaleFailBeforePixelsAreGuessed()
    {
        using var fixture = new Fixture();
        var geometry = Geometry(1920, 1080, 8, 31);
        var map = new PetDanceResolutionMap(PetDanceResolutionMapTests.Calibration, geometry);
        using var image = fixture.Scene(geometry, DancePhase.Go, 1);
        using var wrongSize = new Bitmap(1920, 1080);
        Assert.Throws<InvalidDataException>(() => fixture.Layout().TryExtract(wrongSize, map, out _, out _));
        foreach (var scale in new[] { 0d, double.NaN, double.PositiveInfinity, 4.1 })
            Assert.Throws<InvalidDataException>(() => fixture.Layout().TryExtract(image, map, out _, out _, scale));
        // The existing entry point remains exact-size only and cannot activate
        // native-resolution support accidentally through an old caller.
        Assert.False(fixture.Layout().TryExtract(image, out _, out _));
    }

    [Fact]
    public void NewPanelOverloadLeavesUnrecognizedTransparentFramesUnknownNotLoading()
    {
        var preset = PetDancePreset.Load(AppContext.BaseDirectory);
        using var vision = new PetDancePanelVision(preset.ProfilePath);
        var geometry = Geometry(1920, 1080, 8, 31);
        var map = new PetDanceResolutionMap(PetDanceResolutionMapTests.Calibration, geometry);
        using var transparent = new Bitmap(geometry.CaptureSize.Width, geometry.CaptureSize.Height, PixelFormat.Format32bppArgb);
        using var panel = vision.Extract(transparent, map);
        var observation = vision.Read(panel, 10);
        Assert.Equal(DancePhase.Unknown, observation.Phase);
        Assert.Equal(DanceCue.Unknown, observation.Cue);
        Assert.False(observation.Layout?.Qualified == true);
        Assert.Throws<InvalidDataException>(() => vision.Extract(transparent));
    }

    [Fact]
    public void InvisibleRgbBelowAlphaZeroCannotQualifyAVisibleLayout()
    {
        using var fixture = new Fixture();
        var geometry = Geometry(1280, 720, 1, 38);
        var map = new PetDanceResolutionMap(PetDanceResolutionMapTests.Calibration, geometry);
        using var opaque = fixture.Scene(geometry, DancePhase.Go, 1);
        using var transparent = opaque.Clone(new Rectangle(Point.Empty, opaque.Size), PixelFormat.Format32bppArgb);
        var data = transparent.LockBits(new Rectangle(Point.Empty, transparent.Size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var row = new byte[transparent.Width * 4];
            for (var y = 0; y < transparent.Height; y++)
            {
                var pointer = IntPtr.Add(data.Scan0, y * data.Stride);
                Marshal.Copy(pointer, row, 0, row.Length);
                for (var alpha = 3; alpha < row.Length; alpha += 4) row[alpha] = 0;
                Marshal.Copy(row, 0, pointer, row.Length);
            }
        }
        finally { transparent.UnlockBits(data); }
        Assert.False(fixture.Layout().TryExtract(transparent, map, out var panel, out var observed));
        Assert.Null(panel);
        Assert.Null(observed);
    }

    private static PetDanceCaptureGeometry Geometry(int width, int height, int left, int top) =>
        new(new(width + left + 7, height + top + 4), new(left, top, width, height), 144, "synthetic-ui-render-not-live-qualification");

    private sealed class Fixture : IDisposable
    {
        private static readonly Color Background = Color.FromArgb(35, 65, 110);
        private readonly Dictionary<string, Bitmap> _references = [];
        private readonly DancePanelProfile _profile = new(1, 1282, 759, [512, 100, 256, 40],
            [512, 647, 256, 100], [98, 74, 64, 64], [13, 51, 232, 28], [38, 0, 180, 40], [31, 30, 19], [], "synthetic-only");

        public Fixture()
        {
            foreach (var phase in new[] { DancePhase.Watch, DancePhase.Go, DancePhase.Resolving })
                _references.Add(phase.ToString(), Panel(phase, true));
        }
        public PetDanceUiLayout Layout() => new(_profile, _references, new([1, 1.5]));

        public Bitmap Scene(PetDanceCaptureGeometry geometry, DancePhase phase, double scale, bool button = true, bool second = false)
        {
            using var panel = Panel(phase, button);
            var image = new Bitmap(geometry.CaptureSize.Width, geometry.CaptureSize.Height, PixelFormat.Format24bppRgb);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(Color.Magenta);
            using var fill = new SolidBrush(Background);
            graphics.FillRectangle(fill, geometry.ClientBounds);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            var client = geometry.ClientBounds;
            var targetX = client.X + (client.Width - 1) / 2d;
            var targetY = client.Bottom - 87.5 * scale;
            void DrawBottom(double centerY) => graphics.DrawImage(panel,
                new RectangleF((float)(targetX + (512 - 640.5) * scale), (float)(centerY + (647 - 670.5) * scale),
                    (float)(256 * scale), (float)(100 * scale)), new RectangleF(0, 40, 256, 100), GraphicsUnit.Pixel);
            DrawBottom(targetY);
            if (second) DrawBottom(targetY - 120);
            graphics.DrawImage(panel, new RectangleF((float)(client.X + client.Width / 2d + (512 - 641) * scale),
                (float)(client.Y + (100 - 38) * scale), (float)(256 * scale), (float)(40 * scale)),
                new RectangleF(0, 0, 256, 40), GraphicsUnit.Pixel);
            return image;
        }

        private static Bitmap Panel(DancePhase phase, bool button)
        {
            var image = new Bitmap(256, 140, PixelFormat.Format24bppRgb);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(Background);
            graphics.FillRectangle(Brushes.Red, 115, 10, 24, 20);
            using var fill = new SolidBrush(Color.FromArgb(72, 8, 8));
            graphics.FillRectangle(fill, 16, 52, 226, 24);
            if (phase == DancePhase.Go)
            {
                graphics.FillRectangle(Brushes.Lime, 101, 57, 7, 14);
                graphics.FillRectangle(Brushes.Lime, 112, 57, 20, 4);
                graphics.FillRectangle(Brushes.Lime, 112, 67, 20, 4);
                graphics.FillRectangle(Brushes.Lime, 128, 57, 4, 14);
            }
            else if (phase == DancePhase.Watch)
                foreach (var x in new[] { 84, 103, 122, 141 }) graphics.FillRectangle(Brushes.Yellow, x, 57, 7, 14);
            else
            {
                graphics.FillRectangle(Brushes.Yellow, 60, 57, 16, 5);
                graphics.FillRectangle(Brushes.Yellow, 165, 66, 20, 5);
                graphics.FillRectangle(Brushes.Yellow, 190, 57, 7, 14);
            }
            if (button)
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.FillEllipse(Brushes.Wheat, 104, 79, 50, 50);
                graphics.FillEllipse(Brushes.Black, 106, 81, 46, 46);
                graphics.FillEllipse(Brushes.LightGray, 110, 85, 38, 38);
            }
            return image;
        }

        public void Dispose() { foreach (var reference in _references.Values) reference.Dispose(); }
    }
}
