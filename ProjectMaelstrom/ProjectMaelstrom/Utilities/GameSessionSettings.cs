namespace ProjectMaelstrom.Utilities;

internal enum GameSettingsStatus { Rejected, Pending, AwaitingDispatch, Uncertain, Conflict, Paused, Incomplete, Ready, Restored }
internal enum GameSettingChangeKind { Apply, Restore }
internal sealed record GameSettingRequirement(string Key, string DesiredValue);
internal sealed record GameSettingsObservation(long Revision, bool Verified, IReadOnlyDictionary<string, string?> Values);
internal sealed record GameSettingEntry(string Key, string OriginalValue, string DesiredValue,
    bool ApplyAttempted = false, bool Applied = false, bool RestoreAttempted = false, bool Restored = false);
internal sealed record GameSettingProposal(string Id, string Key, string BeforeValue, string AfterValue, GameSettingChangeKind Kind);
internal sealed record GameSettingsSession(string Id, IReadOnlyList<GameSettingEntry> Entries, long LastObservationRevision,
    bool EndRequested = false, bool InputAvailable = false, GameSettingProposal? Pending = null,
    long? DispatchedAfterRevision = null, string? Rejection = null);
internal sealed record GameSettingsUpdate(GameSettingsSession Session, GameSettingsStatus Status,
    string Reason, GameSettingProposal? Proposal = null);

/// <summary>
/// Immutable, input-free transaction model for reviewed visual game settings. It proposes UI work
/// but performs none. Verified observations and the trusted allowlist must come from the caller.
/// </summary>
internal static class GameSessionSettings
{
    private static readonly HashSet<string> VisualKeys = new(StringComparer.Ordinal)
        { "ui-scale", "resolution", "window-mode" };

    public static GameSettingsSession Capture(string sessionId, IReadOnlyList<GameSettingRequirement> requirements,
        IReadOnlySet<string> trustedAllowedKeys, GameSettingsObservation observation)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(trustedAllowedKeys);
        ArgumentNullException.ThrowIfNull(observation);
        GameSettingsSession Reject(string reason) => new(sessionId, Array.Empty<GameSettingEntry>(), 0, Rejection: reason);
        if (!ValueKnown(sessionId)) return Reject("invalid-session-id");
        if (!observation.Verified || observation.Revision <= 0 || observation.Values == null) return Reject("unknown-baseline");
        var entries = new List<GameSettingEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requirement in requirements)
        {
            if (requirement == null || !VisualKeys.Contains(requirement.Key) || !trustedAllowedKeys.Contains(requirement.Key))
                return Reject("setting-not-reviewed");
            if (!seen.Add(requirement.Key) || !ValueKnown(requirement.DesiredValue)) return Reject("invalid-requirement");
            if (!TryValue(observation, requirement.Key, out var original)) return Reject("unknown-baseline");
            entries.Add(new(requirement.Key, original, requirement.DesiredValue));
        }
        return new(sessionId, entries.AsReadOnly(), observation.Revision);
    }

    /// <summary>Cancellation/end requests restoration; an undispatched apply proposal is invalidated.</summary>
    public static GameSettingsSession RequestEnd(GameSettingsSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var cancelProposal = !session.EndRequested && session.Pending != null && session.DispatchedAfterRevision == null;
        return session with { EndRequested = true, InputAvailable = false, Pending = cancelProposal ? null : session.Pending };
    }

    /// <summary>Record intent immediately before dispatch. An attempt is uncertain until observed.</summary>
    public static GameSettingsSession RecordDispatch(GameSettingsSession session, string proposalId)
    {
        ArgumentNullException.ThrowIfNull(session);
        var pending = session.Pending;
        if (pending == null || session.Rejection != null || !session.InputAvailable || pending.Id != proposalId ||
            session.DispatchedAfterRevision != null || (session.EndRequested && pending.Kind == GameSettingChangeKind.Apply))
            throw new InvalidOperationException("Only a current, eligible, undispatched proposal can be dispatched.");
        var entries = session.Entries.Select(entry => entry.Key != pending.Key ? entry :
            pending.Kind == GameSettingChangeKind.Apply ? entry with { ApplyAttempted = true } : entry with { RestoreAttempted = true }).ToArray();
        return session with { Entries = Array.AsReadOnly(entries), DispatchedAfterRevision = session.LastObservationRevision };
    }

    public static GameSettingsUpdate Advance(GameSettingsSession session, GameSettingsObservation observation, bool desktopAvailable)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(observation);
        if (session.Rejection != null) return Result(session, GameSettingsStatus.Rejected, session.Rejection);
        session = session with { InputAvailable = false };
        if (!observation.Verified || observation.Values == null || observation.Revision <= session.LastObservationRevision)
            return Result(session, session.DispatchedAfterRevision != null ? GameSettingsStatus.Uncertain : GameSettingsStatus.Pending, "fresh-verified-observation-required");
        session = session with { LastObservationRevision = observation.Revision, InputAvailable = desktopAvailable };

        if (session.Pending is { } pending)
        {
            if (!TryValue(observation, pending.Key, out var current))
                return Block(session, GameSettingsStatus.Uncertain, "current-value-unknown");
            if (session.DispatchedAfterRevision == null)
            {
                if (pending.Kind == GameSettingChangeKind.Restore && Same(current, pending.AfterValue))
                {
                    session = session with
                    {
                        Entries = Array.AsReadOnly(session.Entries.Select(e => e.Key == pending.Key ? e with { Restored = true } : e).ToArray()),
                        Pending = null
                    };
                    return Result(session, session.Entries.All(e => !e.Applied || e.Restored) ?
                        GameSettingsStatus.Restored : GameSettingsStatus.Pending, "original-value-observed");
                }
                if (!Same(current, pending.BeforeValue)) return Block(session, GameSettingsStatus.Conflict, "value-changed-before-dispatch");
                return desktopAvailable ? Result(session, GameSettingsStatus.AwaitingDispatch, "proposal-already-issued") :
                    Block(session, GameSettingsStatus.Paused, "desktop-takeover");
            }
            if (!Same(current, pending.AfterValue) && !Same(current, pending.BeforeValue))
                return Block(session, GameSettingsStatus.Conflict, "external-setting-change");
            // An unchanged frame cannot distinguish a failed click from delayed UI work.
            // Retain its intent even during cancellation so a later change remains attributable.
            if (Same(current, pending.BeforeValue))
                return Block(session, GameSettingsStatus.Uncertain, "change-not-yet-observed-no-automatic-retry");

            var entries = session.Entries.Select(entry => entry.Key != pending.Key ? entry :
                pending.Kind == GameSettingChangeKind.Apply ? entry with { Applied = true } : entry with { Restored = true }).ToArray();
            session = session with { Entries = Array.AsReadOnly(entries), Pending = null, DispatchedAfterRevision = null };
        }

        var ordered = session.EndRequested ? session.Entries.Reverse() : session.Entries;
        foreach (var entry in ordered)
        {
            if (session.EndRequested && (!entry.Applied || entry.Restored)) continue;
            if (!TryValue(observation, entry.Key, out var current))
                return Block(session, GameSettingsStatus.Pending, "current-value-unknown");
            if (session.EndRequested)
            {
                if (Same(current, entry.OriginalValue))
                {
                    session = session with { Entries = Array.AsReadOnly(session.Entries.Select(e => e.Key == entry.Key ? e with { Restored = true } : e).ToArray()) };
                    continue;
                }
                if (!Same(current, entry.DesiredValue)) return Block(session, GameSettingsStatus.Conflict, "external-setting-change");
                if (entry.RestoreAttempted) return Block(session, GameSettingsStatus.Incomplete, "restore-not-observed-no-automatic-retry");
            }
            else
            {
                if (Same(entry.OriginalValue, entry.DesiredValue))
                {
                    if (!Same(current, entry.OriginalValue)) return Block(session, GameSettingsStatus.Conflict, "external-setting-change");
                    continue;
                }
                if (entry.Applied)
                {
                    if (Same(current, entry.DesiredValue)) continue;
                    return Block(session, Same(current, entry.OriginalValue) ? GameSettingsStatus.Incomplete : GameSettingsStatus.Conflict, "applied-setting-changed-externally");
                }
                if (!Same(current, entry.OriginalValue)) return Block(session, GameSettingsStatus.Conflict, "value-changed-before-dispatch");
                if (entry.ApplyAttempted) return Block(session, GameSettingsStatus.Incomplete, "apply-not-observed-no-automatic-retry");
            }
            if (!desktopAvailable) return Block(session, GameSettingsStatus.Paused, "desktop-takeover");
            var kind = session.EndRequested ? GameSettingChangeKind.Restore : GameSettingChangeKind.Apply;
            var proposal = new GameSettingProposal($"{session.Id}:{kind}:{entry.Key}", entry.Key, current,
                session.EndRequested ? entry.OriginalValue : entry.DesiredValue, kind);
            session = session with { Pending = proposal };
            return new(session, GameSettingsStatus.AwaitingDispatch, "one-setting-change-proposed", proposal);
        }
        return Result(session, session.EndRequested ? GameSettingsStatus.Restored : GameSettingsStatus.Ready,
            session.EndRequested ? "all-session-changes-restored" : "all-requirements-observed");
    }

    private static bool ValueKnown(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 120 && !value.Any(char.IsControl);
    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.Ordinal);
    private static bool TryValue(GameSettingsObservation observation, string key, out string value)
    {
        if (observation.Values.TryGetValue(key, out var found) && ValueKnown(found)) { value = found!; return true; }
        value = string.Empty; return false;
    }
    private static GameSettingsUpdate Result(GameSettingsSession session, GameSettingsStatus status, string reason) => new(session, status, reason);
    private static GameSettingsUpdate Block(GameSettingsSession session, GameSettingsStatus status, string reason) =>
        Result(session with { InputAvailable = false }, status, reason);
}
