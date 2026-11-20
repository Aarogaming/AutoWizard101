using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record W101CombatEffectCandidate(int EffectType, int Parameter, int Disposition, uint SchoolId,
    int Target, int Rounds, int ParameterPerRound, bool Cloaked);
internal sealed record W101CombatCardCandidate(int ControlIndex, uint TemplateId, uint SpellId, string TemplateName,
    string SpellType, uint SchoolId, int Accuracy, uint EnchantmentId, bool Treasure,
    bool GrayedCandidate, bool CastableCandidate, bool? CheckedCandidate, string? Selected,
    W101CombatEffectCandidate[] Effects);
internal sealed record W101CombatParticipantCandidate(int ControlIndex, string OwnerId, string TemplateId,
    int TeamId, bool IsPlayer, uint SchoolId, int? Health, int? MaxHealth, bool HealthHidden,
    Dictionary<string, int> Pips, bool SchoolPipsKnown, string? Relation);
internal sealed record W101CombatFieldRejection(int ControlIndex, string Kind, string Reason);
internal sealed record W101CombatFieldCandidates(int SchemaVersion, string LayoutId, string Qualification,
    bool? MetadataQualified, string? Phase, int? Round, string? SelectedCard,
    W101CombatCardCandidate[] Cards, W101CombatParticipantCandidate[] Participants,
    W101CombatFieldRejection[] Rejected, int BytesRead, int ReadCalls);
internal sealed record W101CombatReadiness(int SchemaVersion, bool Ready, int CardCandidates,
    int ParticipantCandidates, int RejectedCandidates, string[] Missing, string Note);
internal sealed record W101CombatDuelCandidates(int SchemaVersion, string LayoutId, string Qualification,
    bool? MetadataQualified, string Status, string? Reason, string? DuelId, string? Phase, int? PhaseCode,
    int? Round, string[] ParticipantOwnerIds, string? Outcome, int? WinnerTeamId, int BytesRead, int ReadCalls);

/// <summary>Boundary between optional source-layout observations and the turn player.
/// Candidate fields remain evidence for qualification, never an implicit cast permission.
/// The pinned source decoder is deliberately unable to assert its own live qualification.</summary>
internal static class W101CombatRuntime
{
    internal const string FieldReaderSha256 = "a405acdadd9b983a512e853155d832e6a1126439dcd783cffe393992b2b97c74";
    internal const string DuelReaderSha256 = "632a826d1aee0757a01dbe442d61b3045eecc0f274d5a94650d6f0048c060165";
    private static readonly string[] PipNames = ["generic", "power", "balance", "death", "fire", "ice", "life", "myth", "storm", "shadow"];

    internal static bool ValidCandidates(W101CombatUiContext context)
    {
        var fields = context.FieldCandidates;
        if (fields is null) return context.DuelCandidates is null;
        if (fields.SchemaVersion != 1 || fields.LayoutId != "deimos-development-df2283f" ||
            fields.Qualification != "source-layout-only" || fields.MetadataQualified != false || fields.Phase != null ||
            fields.Round != null || fields.SelectedCard != null || fields.BytesRead is < 0 or > 65536 ||
            fields.ReadCalls is < 0 or > 768 || fields.Cards is not { Length: <= 16 } ||
            fields.Participants is not { Length: <= 8 } || fields.Rejected is not { Length: <= 24 }) return false;
        var ids = new HashSet<int>();
        bool Bound(int index, string kind) => ids.Add(index) && context.Controls.Count(c => c.Index == index && c.Kind == kind) == 1;
        foreach (var card in fields.Cards)
        {
            if (card is null || !Bound(card.ControlIndex, "hand-descendant") || card.TemplateId == 0 ||
                !Text(card.TemplateName) || !Text(card.SpellType) || card.Accuracy is < 0 or > 100 || card.Selected != null ||
                card.CastableCandidate && (card.GrayedCandidate || context.Controls.Single(c => c.Index == card.ControlIndex).Enabled != true) ||
                card.Effects is not { Length: <= 8 }) return false;
            if (card.Effects.Any(e => e is null || e.EffectType is < 0 or > 1024 || e.Target is < 0 or > 17 ||
                e.Parameter is < -10000000 or > 10000000 || e.Rounds is < 0 or > 10000)) return false;
        }
        var owners = new HashSet<string>(StringComparer.Ordinal);
        foreach (var participant in fields.Participants)
        {
            if (participant is null || !Bound(participant.ControlIndex, "combatant-container") ||
                !Identifier(participant.OwnerId, false) || !owners.Add(participant.OwnerId) || !Identifier(participant.TemplateId, true) ||
                participant.TeamId is < 0 or > 1024 || participant.Relation != null || !participant.SchoolPipsKnown ||
                participant.Pips is not { Count: 10 } || !PipNames.All(participant.Pips.ContainsKey) ||
                participant.Pips.Any(p => p.Value is < 0 or > 7) || participant.Pips["shadow"] > 2 ||
                participant.Pips.Where(p => p.Key != "shadow").Sum(p => p.Value) > 7) return false;
            if (participant.HealthHidden ? participant.Health != null || participant.MaxHealth != null :
                participant.Health is null or < 0 || participant.MaxHealth is null or <= 0 or > 10000000 || participant.Health > participant.MaxHealth)
                return false;
        }
        return ValidDuel(context.DuelCandidates, owners) && fields.Rejected.All(r => r is not null && Text(r.Reason) &&
            r.Kind is "card" or "participant" && Bound(r.ControlIndex, r.Kind == "card" ? "hand-descendant" : "combatant-container"));
    }

    private static bool ValidDuel(W101CombatDuelCandidates? duel, HashSet<string> owners)
    {
        if (duel is null) return true;
        if (duel.SchemaVersion != 1 || duel.LayoutId != "deimos-development-df2283f" || duel.Qualification != "source-layout-only" ||
            duel.MetadataQualified != false || duel.Outcome != null || duel.WinnerTeamId != null || duel.BytesRead is < 0 or > 16384 ||
            duel.ReadCalls is < 0 or > 256 || duel.ParticipantOwnerIds is not { Length: <= 8 } || owners.Count == 0) return false;
        if (duel.Status == "unknown") return Text(duel.Reason) && duel.DuelId is null && duel.Phase is null &&
            duel.PhaseCode is null && duel.Round is null && duel.ParticipantOwnerIds.Length == 0;
        string[] phases = ["starting", "pre-planning", "planning", "pre-execution", "execution", "resolution", "victory", "ended"];
        return duel.Status == "matched" && duel.Reason is null && Identifier(duel.DuelId, false) && duel.PhaseCode is >= 0 and <= 7 &&
            duel.Phase == phases[duel.PhaseCode.Value] && duel.Round is >= 0 and <= 10000 &&
            duel.ParticipantOwnerIds.All(p => Identifier(p, false)) && duel.ParticipantOwnerIds.Distinct().Count() == duel.ParticipantOwnerIds.Length &&
            owners.IsSubsetOf(duel.ParticipantOwnerIds);
    }

    internal static W101CombatReadiness Assess(W101UiTreeResult? reading, long now)
    {
        var missing = new List<string>();
        var context = reading?.CombatContext;
        if (reading is null || !reading.CleanupConfirmed || !reading.HookRestored || reading.Status != "observed" ||
            !W101UiTreeOperation.ValidCombatContext(reading, true, now) || context is null)
            return new(1, false, 0, 0, 0, ["fresh-combat-observation"], "No current, restored combat observation is available.");
        if (context.PlanningVisible != true || context.HandVisible != true) missing.Add("visible-planning-hand");
        var fields = context.FieldCandidates;
        if (fields is null) missing.Add("combat-field-observation");
        else
        {
            if (fields.Cards.Length == 0) missing.Add("recognized-hand-cards");
            if (fields.Participants.Length == 0) missing.Add("recognized-combatants");
            if (fields.Rejected.Length != 0) missing.Add("complete-supported-combat-fields");
        }
        // These are independent live gates, not booleans accepted from a pack or API caller.
        missing.AddRange(["live-field-qualification", "encounter-and-turn-identity", "self-and-enemy-identity",
            "selected-card-confirmation", "client-pixel-geometry", "game-input-response", "battle-outcome-evidence"]);
        return new(1, false, fields?.Cards.Length ?? 0, fields?.Participants.Length ?? 0, fields?.Rejected.Length ?? 0,
            missing.ToArray(), "Source-layout candidates only. The battle runner is tested offline; this observation does not enable game input.");
    }

    private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && !value.Any(char.IsControl);
    private static bool Identifier(string? value, bool zero) => value is { Length: > 0 and <= 20 } &&
        ulong.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) &&
        (zero || number > 0) && number.ToString(System.Globalization.CultureInfo.InvariantCulture) == value;
}
