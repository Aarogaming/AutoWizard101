namespace ProjectMaelstrom.Utilities;

/// <summary>A failed optional audio stream stays disabled for the rest of this game.
/// It cannot invalidate independently qualified vision or revive stale audio cues.</summary>
internal sealed class PetDanceAudioAvailability(bool requested, bool visualSequenceEnabled)
{
    public bool Required { get; } = requested && !visualSequenceEnabled;
    public bool Available { get; private set; } = requested;
    public bool MayContinue => !Required || Available;

    public bool Observe(bool capturing)
    {
        if (!Available || capturing) return false;
        Available = false;
        return true;
    }

    public PetDanceSelectedResponse Select(PetDanceVisualSequenceResult? visual, PetDanceSoundSequenceResult? sound)
        => PetDanceResponseSelection.Select(visual?.Cues ?? [], visual?.Accepted == true,
            Available ? sound?.Cues ?? [] : [], Available && sound?.Accepted == true);
}
