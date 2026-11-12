using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PetDanceTeacherTests
{
    private static readonly Size Geometry = new(1280, 720);
    [Fact]
    public void CalibrationReviewRejectsMissingFramesWrongRegionsAndNonDirectionalLabels()
    {
        TeachingFrame[] frames = [new("frame-0000.png", 0, 1282, 760, ""), new("frame-0001.png", 50, 1282, 760, "")];
        var request = new DanceCalibrationRequest(1, 0, [610, 680, 64, 64], "Right");
        Assert.Equal(DanceCue.Right, PetDanceCalibrationReview.Validate(request, frames));
        Assert.Throws<InvalidDataException>(() => PetDanceCalibrationReview.Validate(request with { CueIndex = 3 }, frames));
        Assert.Throws<InvalidDataException>(() => PetDanceCalibrationReview.Validate(request with { NeutralIndex = 1 }, frames));
        Assert.Throws<InvalidDataException>(() => PetDanceCalibrationReview.Validate(request with { KnownCue = "Neutral" }, frames));
        Assert.Throws<InvalidDataException>(() => PetDanceCalibrationReview.Validate(request with { KnownCue = "2" }, frames));
        Assert.Throws<InvalidDataException>(() => PetDanceCalibrationReview.Validate(request with { Region = [1280, 700, 64, 64] }, frames));
    }

    private sealed class Round(int expected)
    {
        internal readonly PetDanceSequence Teacher = new(expected);
        internal double Time;
        internal DanceDecision Sample(DancePhase phase, DanceCue cue, int count = 3)
        {
            DanceDecision? last = null;
            for (var n = 0; n < count; n++) { Time += 50; last = Teacher.Observe(Time, Geometry, phase, cue); }
            return last!;
        }
        internal void Cue(DanceCue cue) { Sample(DancePhase.Watch, DanceCue.Neutral); Sample(DancePhase.Watch, cue, 5); }
        internal DanceDecision Go() { Sample(DancePhase.Watch, DanceCue.Neutral); return Sample(DancePhase.Go, DanceCue.Unknown, 1); }
    }

    [Fact]
    public void VariedSequencesCountHeldAndRepeatedArrowsOnlyOnce()
    {
        var random = new Random(9167);
        for (var test = 0; test < 300; test++)
        {
            var length = random.Next(3, 8); var round = new Round(length);
            var expected = Enumerable.Range(0, length).Select(_ => (DanceCue)random.Next(2, 6)).ToArray();
            foreach (var cue in expected) round.Cue(cue);
            var result = round.Go();
            Assert.False(result.Rejected, result.Reason);
            Assert.Equal(expected.Select(c => "dance-" + c.ToString().ToLowerInvariant()), result.Proposals);
            Assert.Empty(round.Sample(DancePhase.Go, DanceCue.Unknown).Proposals);
        }
    }

    [Theory]
    [InlineData("gap")]
    [InlineData("backwards")]
    [InlineData("duplicate-time")]
    [InlineData("unknown-cue")]
    [InlineData("unknown-phase")]
    [InlineData("geometry")]
    [InlineData("no-neutral")]
    [InlineData("extra")]
    public void BrokenEvidenceNeverProducesAnActionSequence(string fault)
    {
        var round = new Round(3); round.Cue(DanceCue.Up);
        switch (fault)
        {
            case "gap": round.Time += 200; round.Sample(DancePhase.Watch, DanceCue.Neutral); break;
            case "backwards": round.Teacher.Observe(round.Time - 1, Geometry, DancePhase.Watch, DanceCue.Neutral); break;
            case "duplicate-time": round.Teacher.Observe(round.Time, Geometry, DancePhase.Watch, DanceCue.Neutral); break;
            case "unknown-cue": round.Sample(DancePhase.Watch, DanceCue.Unknown); break;
            case "unknown-phase": round.Sample(DancePhase.Unknown, DanceCue.Neutral); break;
            case "geometry": round.Teacher.Observe(round.Time + 1, new(1024, 768), DancePhase.Watch, DanceCue.Neutral); break;
            case "no-neutral": round.Sample(DancePhase.Watch, DanceCue.Left); break;
            case "extra": round.Cue(DanceCue.Left); break;
        }
        round.Cue(DanceCue.Down); round.Cue(DanceCue.Right);
        var result = round.Go(); Assert.True(result.Rejected); Assert.Empty(result.Proposals);
    }

    [Fact]
    public void MissingStartAndPrematureGoAreRejected()
    {
        var round = new Round(3); round.Sample(DancePhase.Watch, DanceCue.Up);
        round.Cue(DanceCue.Down); round.Cue(DanceCue.Right); Assert.Empty(round.Go().Proposals);
        round = new Round(3); round.Cue(DanceCue.Up); Assert.Empty(round.Go().Proposals);
        Assert.Empty(new Round(3).Sample(DancePhase.Go, DanceCue.Unknown).Proposals);
    }

    [Fact]
    public void ConfirmedSuccessAdvancesLengthAndFailureDoesNot()
    {
        var round = new Round(3);
        for (var i = 0; i < 3; i++) round.Cue(DanceCue.Left);
        Assert.Equal(3, round.Go().Proposals.Length);
        round.Sample(DancePhase.Success, DanceCue.Unknown);
        for (var i = 0; i < 4; i++) round.Cue(DanceCue.Right);
        Assert.Equal(4, round.Go().Proposals.Length);
        round.Sample(DancePhase.Failure, DanceCue.Unknown);
        for (var i = 0; i < 4; i++) round.Cue(DanceCue.Up);
        Assert.Equal(4, round.Go().Proposals.Length);
    }

    [Fact]
    public void FailureBoundaryRecoversButReturningToWatchWithoutBoundaryDoesNot()
    {
        var round = new Round(3); round.Cue(DanceCue.Up); round.Sample(DancePhase.Unknown, DanceCue.Unknown);
        round.Sample(DancePhase.Watch, DanceCue.Neutral); Assert.Empty(round.Go().Proposals);
        round.Sample(DancePhase.Failure, DanceCue.Unknown);
        for (var i = 0; i < 3; i++) round.Cue(DanceCue.Up);
        Assert.Equal(3, round.Go().Proposals.Length);
        round.Sample(DancePhase.Watch, DanceCue.Neutral);
        Assert.Empty(round.Go().Proposals);
    }

    [Fact]
    public void InvalidSettingsAndUnknownLabelsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PetDanceSequence(2));
        Assert.Equal(DancePhase.Unknown, PetDanceSequence.ParsePhase("Go!"));
        Assert.True(new PetDanceSequence(3).Observe(double.NaN, Geometry, DancePhase.Go, DanceCue.Unknown).Rejected);
        var good = new DanceCalibration(1, 1280, 720, [50, 50, 100, 100], 1, "", "", "");
        PetDanceVision.Validate(good);
        foreach (var bad in new[] { good with { Region = [0, 0, 500, 40] }, good with { Scale = double.NaN }, good with { Region = [1270, 0, 40, 40] } })
            Assert.Throws<InvalidDataException>(() => PetDanceVision.Validate(bad));
    }

    [Fact]
    public void PinnedAssetsAndBoundedPngHeadersRejectUnreviewedData()
    {
        using var image = new Bitmap(20, 20); using var png = new MemoryStream(); image.Save(png, ImageFormat.Png);
        Assert.Throws<InvalidDataException>(() => PetDanceAssets.Verify(PetDanceAssets.Reviewed[0], png.ToArray()));
        var bytes = png.ToArray(); bytes[16] = 127;
        Assert.Throws<InvalidDataException>(() => PetDanceAssets.Decode(bytes));
        Assert.Equal(8, PetDanceAssets.Reviewed.Select(a => a.Sha256).Distinct().Count());
    }

    [Fact]
    public void TeacherPackCannotImportExecutionCapabilitiesOrAlterBindings()
    {
        var pack = JsonSerializer.SerializeToNode(new
        {
            schema_version = 2,
            game_id = "wizard101",
            status = "draft",
            teacher_bootstrap = new { teacher = "Wizard101_DanceBot", live_qualified = false },
            capabilities = Array.Empty<string>(),
            skills = Array.Empty<object>(),
            dependencies = Array.Empty<string>(),
            actions = new[] { "up", "down", "left", "right" }.Select(d => new { id = "dance-" + d, title = d, key = d.ToUpperInvariant(), duration_ms = 100 })
        })!;
        void Validate(JsonNode node) { using var document = JsonDocument.Parse(node.ToJsonString()); PetDanceAssets.ValidatePack(document.RootElement); }
        Validate(pack);
        var altered = pack.DeepClone(); altered["actions"]![0]!["key"] = "SPACE";
        Assert.Throws<InvalidDataException>(() => Validate(altered));
        altered = pack.DeepClone(); altered["capabilities"] = new JsonArray("input");
        Assert.Throws<InvalidDataException>(() => Validate(altered));
        altered = pack.DeepClone(); altered["teacher_bootstrap"]!["live_qualified"] = true;
        Assert.Throws<InvalidDataException>(() => Validate(altered));
        altered = pack.DeepClone(); altered["actions"]![0]!["command"] = "unreviewed";
        Assert.Throws<InvalidDataException>(() => Validate(altered));
    }

    [Fact]
    public void TeacherWorkerHasNoUnboundedQueueAndReleasesAfterFailure()
    {
        using (PetDanceWork.Acquire()) Assert.Throws<InvalidOperationException>(() => PetDanceWork.Acquire());
        using var next = PetDanceWork.Acquire();
    }

    [Fact]
    public void ImportRejectsChangedOrMissingAssetsWithoutCreatingAPack()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-dance-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "bad.zip");
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                using var image = new Bitmap(20, 20); using var stream = archive.CreateEntry(PetDanceAssets.Reviewed[0].File).Open(); image.Save(stream, ImageFormat.Png);
            }
            Assert.Throws<InvalidDataException>(() => PetDanceAssets.Import(path, root, CancellationToken.None));
            Assert.False(Directory.Exists(Path.Combine(root, "game-packs")));
            var missing = Path.Combine(root, "empty.zip"); using (var archive = ZipFile.Open(missing, ZipArchiveMode.Create)) { }
            Assert.Throws<InvalidDataException>(() => PetDanceAssets.Import(missing, root, CancellationToken.None));
            Assert.Throws<OperationCanceledException>(() => PetDanceAssets.Import(path, root, new CancellationToken(true)));
        }
        finally { Directory.Delete(root, true); }
    }
}
