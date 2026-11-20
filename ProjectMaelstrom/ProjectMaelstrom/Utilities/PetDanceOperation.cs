namespace ProjectMaelstrom.Utilities;

/// <summary>Calibrated recorded teacher, scheduled through the common bounded lifecycle.</summary>
internal static class PetDanceOperation
{
    public const string Id = "w101.pet-dance-review";
    public const string Version = "pet-dance-visual-v1";
    public static OperationTicket Submit(OperationScheduler scheduler, string pack, TeachingRecording recording,
        VisualStateRecognizer? phases, int expected, string dataRoot, IProgress<string>? progress = null)
    {
        if (expected is < 3 or > 7) throw new ArgumentOutOfRangeException(nameof(expected));
        if (!recording.Complete) throw new InvalidOperationException("Finish recording before submitting a teacher routine.");
        var pins = new Dictionary<string, string>();
        foreach (var path in new[] { pack, Path.Combine(Path.GetDirectoryName(pack)!, "pet-dance-calibration.json"), Path.Combine(recording.DirectoryPath, "recording.json") })
        { var full = Path.GetFullPath(path); pins[full] = GameExperiments.Digest(full); }
        var frozenPhases = phases?.Snapshot();
        var request = new OperationRequest(Guid.NewGuid().ToString("N"), Id, Version, 600, false, "recording", pins);
        return scheduler.Submit(request, async (context, token) =>
        {
            frozenPhases?.SaveSnapshot(Path.Combine(context.Directory, "phase-rubric.json"));
            GameExperiments.WriteAtomic(Path.Combine(context.Directory, "teacher-request.json"), new
            { pack = Path.GetFullPath(pack), recording = recording.DirectoryPath, expected, phase_pack_sha256 = frozenPhases?.PackHash, mode = "recorded-teacher", inputs_sent = 0 });
            var result = await PetDanceStudy.RunAsync(pack, recording, frozenPhases, expected, dataRoot, progress, token).ConfigureAwait(false);
            return OperationOutcome.Completed($"Reviewed {result.Frames} frames; saved {result.Proposals} sequence proposals. No game inputs sent.", result.Report, result.Pack);
        });
    }
}
