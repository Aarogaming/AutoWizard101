namespace ProjectMaelstrom.Utilities;

/// <summary>A bounded supervisor handoff after all game input and capture stop.
/// Loading and menu navigation need time distinct from the gameplay budget.</summary>
internal static class PetDanceIdleConfirmation
{
    public const int TimeoutSeconds = 120;
    public const int OperationTimeoutSeconds = 310;

    public static int RemainingSeconds(double startedMs, double nowMs)
    {
        if (!double.IsFinite(startedMs) || !double.IsFinite(nowMs) || startedMs < 0 || nowMs < startedMs) return 0;
        var remaining = TimeoutSeconds * 1000d - (nowMs - startedMs);
        return remaining <= 0 ? 0 : (int)Math.Ceiling(remaining / 1000);
    }

    public static int ExitCode(OperationState state) => state == OperationState.Completed ? 0 : 1;
}
