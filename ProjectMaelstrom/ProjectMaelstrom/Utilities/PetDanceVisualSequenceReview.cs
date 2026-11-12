using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Replays saved direction and phase observations, not new image recognition.
/// It cannot change the recorded game outcome and never sends inputs.</summary>
internal static class PetDanceVisualSequenceReview
{
    public static void Run(string source, string output)
    {
        if (File.Exists(output) || Directory.Exists(output)) throw new IOException("Choose a new review output.");
        using var json = JsonDocument.Parse(GameExperiments.BoundedBytes(source, 24 * 1024 * 1024));
        var root = json.RootElement;
        var rows = root.GetProperty("events");
        if (rows.GetArrayLength() is < 1 or > 6000) throw new InvalidDataException("Review 1–6000 saved observations.");
        var tracker = new PetDanceVisualSequence();
        var useSound = root.TryGetProperty("audio_required", out var audioRequired) && audioRequired.GetBoolean() &&
            root.TryGetProperty("audio_snapshot_complete", out var audioComplete) && audioComplete.GetBoolean();
        var audioRows = useSound ? root.GetProperty("audio_events") : default;
        if (useSound && audioRows.GetArrayLength() > 5000) throw new InvalidDataException("Audio evidence budget exceeded.");
        var audio = useSound ? JsonSerializer.Deserialize<PetDanceAudioObservation[]>(audioRows.GetRawText()) ?? [] : [];
        var soundTracker = new PetDanceSoundSequence();
        var assessments = new List<object>();
        var accepted = 0;
        var selected = 0; var fallbacks = 0;
        foreach (var row in rows.EnumerateArray())
        {
            var time = row.GetProperty("qpc_ms").GetDouble();
            var sample = new PetDanceVisualSample(time,
                Enum.Parse<DancePhase>(row.GetProperty("phase").GetString()!),
                Enum.Parse<DanceCue>(row.GetProperty("cue").GetString()!),
                Enum.Parse<DanceCue>(row.GetProperty("fade_of").GetString()!),
                row.GetProperty("Opacity").GetDouble());
            var result = tracker.Observe(sample);
            var soundResult = useSound ? soundTracker.Observe(new(time, sample.Phase, sample.Cue), audio) : null;
            if (result == null) continue;
            if (result.Accepted) accepted++;
            var choice = PetDanceResponseSelection.Select(result.Cues, result.Accepted,
                soundResult?.Cues ?? [], soundResult?.Accepted == true);
            if (choice.Accepted)
            {
                tracker.NotifyResponseSelected(choice.Cues.Length);
                if (useSound) soundTracker.NotifyResponseSelected(choice.Cues.Length);
                selected++;
                if (choice.Source == "sound-fallback") fallbacks++;
            }
            assessments.Add(new { go_qpc_ms = time, result, sound_result = soundResult, selection = choice });
        }
        GameExperiments.WriteAtomic(output, new
        {
            kind = "saved-direction-appearance-review",
            source_sha256 = GameExperiments.Digest(source),
            assessments,
            accepted_sequences = accepted,
            selected_sequences = selected,
            sound_fallback_sequences = fallbacks,
            synchronized_sound_replay = useSound,
            inputs_sent = 0,
            live_qualified = false,
            note = "Uses stored classifications, not newly decoded images. When complete sound evidence exists, the same selector synchronizes feedback progression. Original successes and failures are unchanged."
        });
    }
}
