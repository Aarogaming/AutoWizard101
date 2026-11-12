namespace ProjectMaelstrom.Utilities;

internal enum PetSnackMode { TrainingXp, Happiness }
internal enum PetSnackContext { Unknown, TrainingFeed, HappinessFeed, InventoryItemFeed }

/// <summary>Observed values in one consistent unit/age; null is unknown, never zero.</summary>
internal sealed record PetSnackProgress(int? Current = null, int? Target = null, int? Cap = null);

/// <summary>Remaining allowances supplied by the caller, not persistent accounting.</summary>
internal sealed record PetSnackBudget(int? FeedsRemaining = 0, int? ResourceRemaining = null,
    int? ResourceReserve = null, int? ResourceCost = null);

internal sealed record PetSnackCandidate(string ProfileKey, bool IdentityVerified = false,
    bool Protected = true, int? Quantity = null, int? MinimumRemaining = null,
    int? TrainingXpBenefit = null, int? HappinessBenefit = null);

internal sealed record PetSnackPolicy(string? PetProfileKey = null, bool TrainingEnabled = false,
    bool HappinessEnabled = false, IReadOnlyCollection<string>? AllowedSnackProfileKeys = null,
    bool SkipOpportunity = false);

internal sealed record PetSnackSnapshot(PetSnackContext Context = PetSnackContext.Unknown,
    bool ContextVerified = false, string? PetProfileKey = null, bool PetVerified = false,
    string? OpportunityId = null, bool OpportunityVerified = false, bool TrainingGameComplete = false,
    bool OpportunityAlreadyConsumed = false, bool HasUnresolvedConsumption = false,
    bool ObservationFresh = false, bool InteractionReady = false,
    PetSnackProgress? TrainingXp = null, PetSnackProgress? Happiness = null,
    PetSnackBudget? Budget = null, IReadOnlyList<PetSnackCandidate>? Snacks = null);

internal sealed record PetSnackRejection(string ProfileKey, string Reason);
internal sealed record PetSnackSuggestion(PetSnackMode Mode, string PetProfileKey, string OpportunityId,
    string SnackProfileKey, int Quantity, int ExpectedBenefit, int UsefulBenefit, int Overshoot);
internal sealed record PetSnackPlan(PetSnackSuggestion? Suggestion, string Reason,
    IReadOnlyList<PetSnackRejection> Rejections);

/// <summary>
/// Pure, read-only one-snack proposal. All identities, rewards and eligibility are caller-supplied
/// evidence; a proposal is neither input authorization nor a verified consumption result.
/// </summary>
internal static class PetSnackPlanner
{
    public static PetSnackPlan Plan(PetSnackMode mode, PetSnackSnapshot snapshot, PetSnackPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(policy);
        if (mode is not (PetSnackMode.TrainingXp or PetSnackMode.Happiness)) return NoAction("unknown-mode");
        if (policy.SkipOpportunity) return NoAction("user-skipped");
        if (mode == PetSnackMode.TrainingXp ? !policy.TrainingEnabled : !policy.HappinessEnabled)
            return NoAction("mode-disabled");

        var expectedContext = mode == PetSnackMode.TrainingXp ? PetSnackContext.TrainingFeed : PetSnackContext.HappinessFeed;
        if (!snapshot.ContextVerified || snapshot.Context != expectedContext) return NoAction("wrong-or-unverified-context");
        if (!snapshot.PetVerified || string.IsNullOrWhiteSpace(policy.PetProfileKey) ||
            !string.Equals(policy.PetProfileKey, snapshot.PetProfileKey, StringComparison.Ordinal))
            return NoAction("wrong-or-unverified-pet");
        if (!snapshot.OpportunityVerified || string.IsNullOrWhiteSpace(snapshot.OpportunityId) ||
            (mode == PetSnackMode.TrainingXp && !snapshot.TrainingGameComplete))
            return NoAction("unverified-opportunity");
        if (snapshot.OpportunityAlreadyConsumed || snapshot.HasUnresolvedConsumption)
            return NoAction("opportunity-needs-reconciliation");
        if (!snapshot.ObservationFresh || !snapshot.InteractionReady) return NoAction("stale-or-unready-context");

        var progress = mode == PetSnackMode.TrainingXp ? snapshot.TrainingXp : snapshot.Happiness;
        if (progress?.Current is not int current || progress.Target is not int target || progress.Cap is not int cap ||
            current < 0 || target < 0 || cap < 0 || current > cap || target > cap)
            return NoAction("unknown-or-invalid-goal");
        if (current >= cap) return NoAction("goal-capped");
        if (current >= target) return NoAction("goal-complete");

        var budget = snapshot.Budget;
        if (budget?.FeedsRemaining is not int feeds || feeds <= 0 ||
            budget.ResourceRemaining is not int resource || budget.ResourceReserve is not int reserve ||
            budget.ResourceCost is not int cost || resource < 0 || reserve < 0 || cost < 0 || resource - cost < reserve)
            return NoAction("unknown-or-exhausted-budget");

        var allowed = new HashSet<string>(policy.AllowedSnackProfileKeys ?? [], StringComparer.Ordinal);
        var snacks = snapshot.Snacks ?? [];
        var duplicateKeys = snacks.Where(s => s != null).GroupBy(s => s.ProfileKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var rejections = new List<PetSnackRejection>();
        var suggestions = new List<PetSnackSuggestion>();
        var remaining = target - current;
        foreach (var snack in snacks)
        {
            string? reason = null;
            var benefit = mode == PetSnackMode.TrainingXp ? snack?.TrainingXpBenefit : snack?.HappinessBenefit;
            if (snack == null || string.IsNullOrWhiteSpace(snack.ProfileKey) || !snack.IdentityVerified)
                reason = "unknown-identity";
            else if (duplicateKeys.Contains(snack.ProfileKey)) reason = "ambiguous-duplicate-identity";
            else if (!allowed.Contains(snack.ProfileKey)) reason = "not-allowed";
            else if (snack.Protected) reason = "protected-stock";
            else if (snack.Quantity is not int quantity || snack.MinimumRemaining is not int minimum || quantity < 0 || minimum < 0)
                reason = "unknown-or-invalid-stock";
            else if (quantity <= minimum) reason = "stock-reserve";
            else if (benefit is not > 0) reason = "unknown-or-no-benefit";

            if (reason != null)
            {
                rejections.Add(new(snack?.ProfileKey ?? string.Empty, reason));
                continue;
            }
            suggestions.Add(new(mode, policy.PetProfileKey!, snapshot.OpportunityId!, snack!.ProfileKey,
                1, benefit!.Value, Math.Min(remaining, benefit.Value), Math.Max(0, benefit.Value - remaining)));
        }

        var selected = suggestions.OrderByDescending(s => s.UsefulBenefit).ThenBy(s => s.Overshoot)
            .ThenBy(s => s.SnackProfileKey, StringComparer.Ordinal).FirstOrDefault();
        return new(selected, selected == null ? "no-eligible-snack" : "most-goal-progress-then-least-overshoot",
            rejections.OrderBy(r => r.ProfileKey, StringComparer.Ordinal).ThenBy(r => r.Reason, StringComparer.Ordinal).ToArray());
    }

    private static PetSnackPlan NoAction(string reason) => new(null, reason, Array.Empty<PetSnackRejection>());
}
