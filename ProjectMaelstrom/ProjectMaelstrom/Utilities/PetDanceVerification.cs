using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Reproducible local integration check using supplied assets and explicitly synthetic recordings.</summary>
internal static class PetDanceVerification
{
    private static readonly Rectangle CueRegion = new(36, 38, 128, 128);
    private static readonly Rectangle PhaseRegion = new(0, 0, 200, 24);
    private static readonly (string Name, Color Color)[] Phases = [("dance-ready", Color.Blue), ("dance-watch", Color.Yellow), ("dance-go", Color.Lime), ("dance-success", Color.Magenta), ("dance-failure", Color.Red)];

    public static async Task RunAsync(string archive, string output, CancellationToken token)
    {
        if (Directory.Exists(output)) throw new IOException("Use a new verification directory.");
        Directory.CreateDirectory(output);
        var watch = Stopwatch.StartNew(); var cpu = Process.GetCurrentProcess().TotalProcessorTime;
        var pack = PetDanceAssets.Import(archive, output, token); var assets = PetDanceAssets.Read(pack);
        Bitmap Frame(string phase, int? arrow = null, double scale = .75)
        {
            var bitmap = new Bitmap(200, 170);
            using var graphics = Graphics.FromImage(bitmap); graphics.Clear(Color.FromArgb(24, 32, 48));
            using var brush = new SolidBrush(Phases.First(p => p.Name == phase).Color); graphics.FillRectangle(brush, PhaseRegion);
            if (arrow.HasValue)
            {
                using var image = PetDanceAssets.Decode(assets[arrow.Value]);
                var width = (int)Math.Round(image.Width * scale); var height = (int)Math.Round(image.Height * scale);
                graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                graphics.DrawImage(image, new Rectangle(CueRegion.X + (CueRegion.Width - width) / 2, CueRegion.Y + (CueRegion.Height - height) / 2, width, height));
            }
            return bitmap;
        }
        var calibration = TeachingRecording.Start(output, "Synthetic Pet Dance calibration (not gameplay)", "fixture");
        using (var empty = Frame("dance-watch")) calibration.AddImportedFrame(empty, 0);
        using (var arrow = Frame("dance-watch", 0)) calibration.AddImportedFrame(arrow, 50);
        calibration.Finish();
        var calibrated = PetDanceVision.Calibrate(pack, calibration, 1, 0, CueRegion, DanceCue.Down, token);
        var visualChecks = new List<object>(); int correct = 0, wrong = 0, unknown = 0;
        using (var vision = new PetDanceVision(pack))
        {
            for (var index = 0; index < 8; index++)
            {
                using var frame = Frame("dance-watch", index); var prediction = vision.Read(frame);
                var expected = Enum.Parse<DanceCue>(PetDanceAssets.Reviewed[index].Label, true);
                if (prediction.Cue == expected) correct++; else if (prediction.Cue == DanceCue.Unknown) unknown++; else wrong++;
                visualChecks.Add(new { asset = PetDanceAssets.Reviewed[index].File, expected = expected.ToString(), predicted = prediction.Cue.ToString(), prediction.Score, prediction.Margin });
            }
            using var neutral = Frame("dance-watch"); var neutralPrediction = vision.Read(neutral);
            if (neutralPrediction.Cue != DanceCue.Neutral) throw new InvalidDataException("Neutral reference was not recognized.");
            using var resized = new Bitmap(210, 170);
            if (vision.Read(resized).Cue != DanceCue.Unknown) throw new InvalidDataException("Changed geometry was accepted.");
            using var clutter = Frame("dance-watch");
            using (var drawing = Graphics.FromImage(clutter)) drawing.FillRectangle(Brushes.White, CueRegion);
            if (vision.Read(clutter).Cue != DanceCue.Unknown) throw new InvalidDataException("Unseen clutter was accepted as neutral or an arrow.");
        }
        GameExperiments.WriteAtomic(Path.Combine(output, "visual-checks.json"), visualChecks);
        if (wrong > 0 || correct != 8) throw new InvalidDataException($"Synthetic arrow check: {correct} correct, {wrong} wrong, {unknown} uncertain. See visual-checks.json.");
        var scales = new List<object>(); int scaleCorrect = 0, scaleUnknown = 0, scaleWrong = 0;
        foreach (var sourceScale in new[] { .55, .75, 1.0, 1.25, 1.5 })
        {
            using var known = Frame("dance-watch", 0, sourceScale); using var crop = known.Clone(CueRegion, PixelFormat.Format24bppRgb);
            var learned = PetDanceMatcher.Calibrate(assets, crop, DanceCue.Down, token);
            using var matcher = new PetDanceMatcher(assets, learned);
            for (var index = 0; index < 8; index++)
            {
                using var frame = Frame("dance-watch", index, sourceScale); using var region = frame.Clone(CueRegion, PixelFormat.Format24bppRgb);
                var prediction = matcher.Match(region); var expected = Enum.Parse<DanceCue>(PetDanceAssets.Reviewed[index].Label, true);
                if (prediction.Cue == expected) scaleCorrect++; else if (prediction.Cue == DanceCue.Unknown) scaleUnknown++; else scaleWrong++;
                scales.Add(new { source_scale = sourceScale, calibrated_scale = learned, asset = PetDanceAssets.Reviewed[index].File, expected = expected.ToString(), predicted = prediction.Cue.ToString(), prediction.Score, prediction.Margin });
            }
        }
        GameExperiments.WriteAtomic(Path.Combine(output, "scale-checks.json"), scales);
        if (scaleWrong != 0 || scaleCorrect < 36) throw new InvalidDataException("Multiscale recognition regression. Inspect scale-checks.json.");
        var phaseExamples = Phases.Select(phase =>
        {
            using var frame = Frame(phase.Name); using var crop = frame.Clone(PhaseRegion, PixelFormat.Format24bppRgb);
            return new VisualExample(phase.Name, Convert.ToHexString(SHA256.HashData(VisualStateRecognizer.Features(crop))), frame.Width, frame.Height, VisualStateRecognizer.Features(crop), PhaseRegion);
        }).ToArray();
        var phaseBytes = JsonSerializer.SerializeToUtf8Bytes(phaseExamples);
        File.WriteAllBytes(Path.Combine(output, "synthetic-phase-rubric.json"), phaseBytes);
        var phases = new VisualStateRecognizer("Synthetic colored phase rubric (not trained on W101)", Convert.ToHexString(SHA256.HashData(phaseBytes)), phaseExamples);
        var recording = TeachingRecording.Start(output, "Synthetic Pet Dance varied sequence replay (not gameplay)", "fixture");
        var random = new Random(1193); double timestamp = 0;
        void Add(string phase, int? arrow, int count)
        {
            using var frame = Frame(phase, arrow);
            for (var n = 0; n < count; n++) { recording.AddImportedFrame(frame, timestamp); timestamp += 50; }
        }
        var expectedSequences = new List<string[]>();
        for (var round = 0; round < 3; round++)
        {
            var arrows = Enumerable.Range(0, round + 3).Select(_ => random.Next(4)).ToArray();
            expectedSequences.Add(arrows.Select(index => "dance-" + PetDanceAssets.Reviewed[index].Label).ToArray());
            Add(round == 0 ? "dance-ready" : "dance-success", null, 3);
            foreach (var arrow in arrows) { Add("dance-watch", null, 4); Add("dance-watch", arrow, 6); }
            Add("dance-watch", null, 4); Add("dance-go", null, 4);
        }
        recording.Finish(); var labelsBefore = File.ReadAllBytes(Path.Combine(recording.DirectoryPath, "recording.json"));
        await using var scheduler = new OperationScheduler(output);
        var ticket = PetDanceOperation.Submit(scheduler, pack, recording, phases, 3, output);
        using var cancellation = token.Register(ticket.Cancel);
        var receipt = await ticket.Completion.ConfigureAwait(false);
        if (receipt.State != OperationState.Completed) throw new InvalidOperationException("Scheduled teacher failed: " + receipt.Message);
        using var teacherReport = JsonDocument.Parse(File.ReadAllBytes(receipt.Artifacts[0]));
        var data = teacherReport.RootElement;
        var result = new DanceStudyResult(receipt.Artifacts[1], receipt.Artifacts[0], data.GetProperty("frames").GetInt32(),
            data.GetProperty("sequence_proposals").GetInt32(), data.GetProperty("rejected_frames").GetInt32(), data.GetProperty("analysis_ms").GetDouble());
        var actualSequences = ReadProposals(result.Pack);
        if (actualSequences.Count != expectedSequences.Count || actualSequences.Where((value, index) => !value.SequenceEqual(expectedSequences[index])).Any())
            throw new InvalidDataException("Teacher proposals did not match the synthetic random sequences. Inspect the saved replay.");
        if (!File.ReadAllBytes(Path.Combine(recording.DirectoryPath, "recording.json")).SequenceEqual(labelsBefore)) throw new IOException("Teacher modified source recording.");
        var sparse = TeachingRecording.Start(output, "Synthetic slow stills must not produce input proposals", "fixture");
        foreach (var frame in recording.Frames)
        {
            using var image = recording.ReadFrame(sparse.Frames.Count);
            sparse.AddImportedFrame(image, frame.TimeMs * 20);
        }
        sparse.Finish(); var sparseResult = await PetDanceStudy.RunAsync(pack, sparse, phases, 3, output, null, token);
        if (sparseResult.Proposals != 0) throw new InvalidDataException("Sparse recording incorrectly produced a proposal.");
        GameExperiments.WriteAtomic(Path.Combine(output, "verification.json"), new
        {
            status = "passed",
            source = "local archive images on synthetic scenes; not live W101 gameplay",
            archive = Path.GetFileName(archive),
            calibration_scale = calibrated.Scale,
            correct_arrows = correct,
            incorrect_arrows = wrong,
            uncertain_arrows = unknown,
            scale_cases = 40,
            scale_correct = scaleCorrect,
            scale_uncertain = scaleUnknown,
            scale_incorrect = scaleWrong,
            varied_rounds = result.Proposals,
            expected_sequences = expectedSequences,
            actual_sequences = actualSequences,
            sparse_proposals = sparseResult.Proposals,
            result.Frames,
            result.AnalysisMs,
            analysis_ms_per_frame = result.AnalysisMs / result.Frames,
            elapsed_seconds = watch.Elapsed.TotalSeconds,
            process_cpu_seconds = (Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalSeconds,
            source_labels_unchanged = true,
            inputs_sent = 0,
            scheduler_job = receipt.Request.Id,
            scheduler_state = receipt.State.ToString(),
            shared_operation = receipt.Request.Operation,
            gpu_model = false,
            opencv_threads = 1,
            result.Pack,
            result.Report,
            live_qualified = false,
            remaining = "Current-game arrow and Watch/Go calibration; live cue cadence, action delivery and reward validation."
        });
    }
    private static List<string[]> ReadProposals(string pack)
    {
        var rows = new List<string[]>();
        foreach (var line in File.ReadLines(Path.Combine(Path.GetDirectoryName(pack)!, "teacher", "events.jsonl")))
        {
            using var row = JsonDocument.Parse(line);
            if (row.RootElement.GetProperty("kind").GetString() == "action_proposal")
                rows.Add(row.RootElement.GetProperty("payload").GetProperty("action_ids").EnumerateArray().Select(value => value.GetString()!).ToArray());
        }
        return rows;
    }
}
