namespace ProjectMaelstrom.Utilities;

internal enum W101InteractionMethod { VisibleUi, OfflineResearch, ProcessMemoryObservation, ClientInternals, LiveGameTraffic, UnreviewedCode }

/// <summary>The Wizard101 workspace uses visible screens and ordinary player input.
/// Generic control permission, source ownership and manifest claims cannot relax this boundary.
/// This is Maelstrom admission policy, not an operating-system sandbox for external programs.</summary>
internal static class W101InteractionPolicy
{
    private static volatile bool _processObservationAllowed = false;

    /// <summary>
    /// Explicit exception toggle for W101 workspace.
    /// When true, read-only process memory observation is permitted as an additive telemetry stream.
    /// Defaults to false (default-to-safe baseline).
    /// </summary>
    public static bool IsProcessObservationAllowed
    {
        get => _processObservationAllowed;
        set => _processObservationAllowed = value;
    }

    public static string? BlockReason(W101InteractionMethod method) => method switch
    {
        W101InteractionMethod.VisibleUi or W101InteractionMethod.OfflineResearch => null,
        W101InteractionMethod.ProcessMemoryObservation => IsProcessObservationAllowed
            ? null
            : "Wizard101 UI-only mode: process-memory observation is not enabled. It requires separate evidence and a scope decision; use screen observation meanwhile.",
        W101InteractionMethod.ClientInternals => "Wizard101 UI-only mode: client hooks, injection and memory modification are not enabled. Use visible screens and ordinary game controls.",
        W101InteractionMethod.LiveGameTraffic => "Wizard101 UI-only mode: live game traffic access is not enabled. Saved files remain available for offline research.",
        _ => "Wizard101 UI-only mode: unreviewed legacy programs and plugin assemblies cannot run. Use a native screen-based routine or retain this source for offline review."
    };

    // This map belongs to trusted application code, never to an imported manifest.
    // Saved-capture decoding is offline research; it does not authorize live capture.
    public static W101InteractionMethod ToolMethod(string tool) => tool.ToLowerInvariant() switch
    {
        "companion" or "moonlight" or "wizdiff" => W101InteractionMethod.OfflineResearch,
        "wiztype" => W101InteractionMethod.ProcessMemoryObservation,
        "deimos" or "wizwalker" or "wizsdk" => W101InteractionMethod.ClientInternals,
        _ => W101InteractionMethod.UnreviewedCode
    };

    public static string? ToolBlockReason(string tool) => BlockReason(ToolMethod(tool));
    public static void Require(W101InteractionMethod method)
    {
        if (BlockReason(method) is { } reason) throw new InvalidOperationException(reason);
    }
    public static void RequireTool(string tool) => Require(ToolMethod(tool));
}
