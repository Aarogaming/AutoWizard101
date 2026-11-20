namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceSelectedResponse(string Source, string[] Cues, bool Accepted,
    bool ModalitiesDisagree, string Reason);

/// <summary>Selects a complete qualified visual sequence, using independent sound
/// only when vision has no complete sequence. This never creates missing arrows or
/// grants input authority; the live dispatcher still checks the target and clock.</summary>
internal static class PetDanceResponseSelection
{
    public static PetDanceSelectedResponse Select(string[] visualCues, bool visualAccepted,
        string[] soundCues, bool soundAccepted)
    {
        ArgumentNullException.ThrowIfNull(visualCues);
        ArgumentNullException.ThrowIfNull(soundCues);
        var visual = visualAccepted && Valid(visualCues);
        var sound = soundAccepted && Valid(soundCues);
        var disagreement = visual && sound && !visualCues.SequenceEqual(soundCues);
        if (visual)
            return new("visual", [.. visualCues], true, disagreement,
                disagreement ? "Complete visual sequence selected; independent sound disagrees and is logged for review." :
                sound ? "Complete visual sequence independently agrees with sound." : "Complete visual sequence selected.");
        if (sound)
            return new("sound-fallback", [.. soundCues], true, false,
                "Visual sequence incomplete or uncertain; complete independent sound sequence selected.");
        return new("none", [], false, false, "Neither channel supplied a complete qualified sequence.");
    }

    private static bool Valid(string[] cues) => cues.Length is >= 3 and <= 7 &&
        cues.All(cue => cue is "up" or "down" or "left" or "right");
}
