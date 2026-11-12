namespace ProjectMaelstrom.Utilities;

/// <summary>One optional home session per user across installed and portable copies. OS releases on exit.</summary>
internal static class HomeMovementLease
{
    public static GameInputLease Acquire(string? directory = null) => GameInputLease.Acquire("w101.auto-idle", directory);
}
