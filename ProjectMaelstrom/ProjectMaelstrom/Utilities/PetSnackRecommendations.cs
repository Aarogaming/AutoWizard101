namespace ProjectMaelstrom.Utilities;

internal enum PetSnackRelationship { Unknown, Ate, Liked, Loved }
internal sealed record PetSnackProgressEvidence(PetSnackProgress? Values = null, bool Verified = false);
internal sealed record PetSnackBenefitEvidence(int? Amount = null, bool Verified = false);

/// <summary>Benefits are observed totals for this pet/context, including any
/// verified preference/event effect. School never supplies an inferred bonus.</summary>
internal sealed record PetSnackRecommendationCandidate(PetSnackCandidate Inventory,
    string? Name = null, bool NameVerified = false, string? School = null,
    bool SchoolVerified = false,
    IReadOnlyDictionary<PetTrainingGoal, PetSnackBenefitEvidence>? Benefits = null,
    PetSnackRelationship Relationship = PetSnackRelationship.Unknown,
    bool RelationshipVerified = false, string? RelationshipPetProfileKey = null);

/// <summary>All verification flags are caller evidence, not recognition performed
/// here. ProgressStable must be false during an unresolved age/stat-cap change.
/// Feeding.Budget uses Energy units; its cost is the proposed feed's cost, not
/// an already-paid game entry cost. Next-game Energy is checked separately.</summary>
internal sealed record PetSnackRecommendationSnapshot(PetSnackSnapshot Feeding,
    string? ExpectedPetProfileKey = null, string? PetSchool = null,
    bool PetSchoolVerified = false, bool ProgressStable = false,
    IReadOnlyDictionary<PetTrainingGoal, PetSnackProgressEvidence>? Progress = null,
    IReadOnlyList<PetSnackRecommendationCandidate>? Snacks = null);

internal sealed record PetSnackRecommendation(string SnackProfileKey, string Name,
    string PetProfileKey, string OpportunityId, PetTrainingGoal Goal,
    int ExpectedBenefit, int UsefulBenefit, int Overshoot, int RemainingStock,
    bool? SchoolMatchesPet, PetSnackRelationship Relationship, IReadOnlyList<string> Reasons);

internal sealed record PetSnackRecommendationReview(IReadOnlyList<PetSnackRecommendation> Ranked,
    string Reason, IReadOnlyList<PetSnackRejection> Rejections)
{
    public bool CrownShopLocked => true;
    public bool AutomaticFeedingAvailable => false;
}

/// <summary>Pure read-only ranking of one-snack alternatives. Never reserves
/// stock, advances a budget, feeds, purchases or changes game state.</summary>
internal static class PetSnackRecommendations
{
    public static PetSnackRecommendationReview Review(PetSnackRecommendationSnapshot snapshot,
        PetTrainingPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Feeding);
        var settings = PetTrainingPreferences.Normalize(preferences);
        if (settings.Snacks == PetSnackPreference.NeverFeed) return Empty("snacks-disabled");
        if (!snapshot.ProgressStable) return Empty("progress-needs-reconciliation");
        if (snapshot.Progress == null || !snapshot.Progress.TryGetValue(settings.Goal, out var evidence) ||
            evidence is not { Verified: true, Values: not null })
            return Empty("unknown-or-unverified-goal");

        var goal = settings.GoalTarget.HasValue ? evidence.Values with { Target = settings.GoalTarget } : evidence.Values;
        var allowedNames = new HashSet<string>(settings.AllowedSnackNames!, StringComparer.OrdinalIgnoreCase);
        var protectedNames = new HashSet<string>(settings.ProtectedSnackNames!, StringComparer.OrdinalIgnoreCase);
        var candidates = snapshot.Snacks ?? [];
        var duplicateKeys = candidates.Where(candidate => candidate?.Inventory != null)
            .GroupBy(candidate => candidate.Inventory.ProfileKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var ambiguousNames = candidates.Where(candidate => ValidName(candidate) && candidate.Inventory != null)
            .GroupBy(candidate => candidate.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(candidate => candidate.Inventory.ProfileKey).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rejected = new List<PetSnackRejection>();
        var projected = new List<PetSnackCandidate>();
        var mapped = new List<PetSnackRecommendationCandidate>();
        foreach (var candidate in candidates)
        {
            var key = candidate?.Inventory?.ProfileKey ?? string.Empty;
            if (duplicateKeys.Contains(key))
            { rejected.Add(new(key, "ambiguous-duplicate-identity")); continue; }
            if (!ValidName(candidate) || candidate!.Inventory == null)
            { rejected.Add(new(key, "unknown-or-unverified-name")); continue; }
            var name = candidate.Name!.Trim();
            if (ambiguousNames.Contains(name))
            { rejected.Add(new(key, "ambiguous-snack-name")); continue; }
            if (!allowedNames.Contains(name))
            { rejected.Add(new(key, "not-allowed")); continue; }
            PetSnackBenefitEvidence? benefit = null;
            if (candidate.Benefits != null) candidate.Benefits.TryGetValue(settings.Goal, out benefit);
            // Reuse the scalar planner's admission and inventory rules. This is
            // an internal projection of the selected stat, not a claim of XP.
            projected.Add(candidate.Inventory with
            {
                Protected = candidate.Inventory.Protected || protectedNames.Contains(name),
                MinimumRemaining = candidate.Inventory.MinimumRemaining is int stockReserve
                    ? Math.Max(stockReserve, settings.MinimumSnackRemaining) : null,
                TrainingXpBenefit = benefit is { Verified: true, Amount: >= 0 } ? benefit.Amount : null
            });
            mapped.Add(candidate);
        }

        var budget = snapshot.Feeding.Budget is { } observed ? observed with
        {
            FeedsRemaining = observed.FeedsRemaining is int count ? Math.Min(count, settings.MaxFeedsPerVisit) : null,
            ResourceReserve = observed.ResourceReserve is int energyReserve ? Math.Max(energyReserve, settings.EnergyReserve) : null
        } : null;
        var plan = PetSnackPlanner.Plan(PetSnackMode.TrainingXp,
            snapshot.Feeding with { TrainingXp = goal, Budget = budget, Snacks = projected },
            new(snapshot.ExpectedPetProfileKey, TrainingEnabled: true,
                AllowedSnackProfileKeys: projected.Select(snack => snack.ProfileKey).ToArray()));
        rejected.AddRange(plan.Rejections);
        var orderedRejections = rejected.OrderBy(item => item.ProfileKey, StringComparer.Ordinal)
            .ThenBy(item => item.Reason, StringComparer.Ordinal).ToArray();
        if (plan.Suggestion == null) return new([], plan.Reason, orderedRejections);

        var rejectedKeys = plan.Rejections.Select(item => item.ProfileKey).ToHashSet(StringComparer.Ordinal);
        var remaining = goal.Target!.Value - goal.Current!.Value;
        var ranking = new List<PetSnackRecommendation>();
        for (var index = 0; index < projected.Count; index++)
        {
            var snack = projected[index];
            if (rejectedKeys.Contains(snack.ProfileKey)) continue;
            var metadata = mapped[index];
            var benefit = snack.TrainingXpBenefit!.Value;
            var useful = Math.Min(remaining, benefit);
            var overshoot = Math.Max(0, benefit - remaining);
            bool? schoolMatches = snapshot.PetSchoolVerified && !string.IsNullOrWhiteSpace(snapshot.PetSchool) &&
                metadata.SchoolVerified && !string.IsNullOrWhiteSpace(metadata.School)
                ? string.Equals(snapshot.PetSchool.Trim(), metadata.School.Trim(), StringComparison.OrdinalIgnoreCase) : null;
            var relationship = metadata.RelationshipVerified && Enum.IsDefined(metadata.Relationship) &&
                string.Equals(metadata.RelationshipPetProfileKey, snapshot.ExpectedPetProfileKey, StringComparison.Ordinal)
                ? metadata.Relationship : PetSnackRelationship.Unknown;
            var reasons = new List<string>
            {
                "verified-benefit-for-selected-goal", "within-stock-and-budget-limits",
                schoolMatches switch { true => "same-school-does-not-imply-preference", false => "different-school-does-not-exclude-benefit", null => "school-context-unverified" },
                relationship == PetSnackRelationship.Unknown ? "preference-unverified-no-bonus-inferred" : "preference-verified-for-selected-pet",
                overshoot == 0 ? "no-goal-overshoot" : "benefit-exceeds-remaining-goal"
            };
            ranking.Add(new(snack.ProfileKey, metadata.Name!.Trim(), snapshot.ExpectedPetProfileKey!,
                snapshot.Feeding.OpportunityId!, settings.Goal, benefit, useful, overshoot,
                snack.Quantity!.Value - 1, schoolMatches, relationship, reasons));
        }
        return new(ranking.OrderByDescending(item => item.UsefulBenefit).ThenBy(item => item.Overshoot)
            .ThenByDescending(item => item.Relationship).ThenBy(item => item.SnackProfileKey, StringComparer.Ordinal).ToArray(),
            "goal-progress-then-least-overshoot-then-verified-preference", orderedRejections);
    }

    private static bool ValidName(PetSnackRecommendationCandidate? candidate) =>
        candidate is { NameVerified: true } && !string.IsNullOrWhiteSpace(candidate.Name) &&
        candidate.Name.Length <= 128 && !candidate.Name.Any(char.IsControl);

    private static PetSnackRecommendationReview Empty(string reason) => new([], reason, []);
}

internal enum PetTrainingEnergyDecision { ObserveOnly, Stop, OneAttempt }

internal sealed record PetTrainingEnergySnapshot(int? Current = null, int? NextAttemptCost = null,
    int? Reserve = null, bool EnergyVerified = false, bool CostVerified = false,
    bool ObservationFresh = false);

internal sealed record PetTrainingEnergyPlan(PetTrainingEnergyDecision Decision, string Reason,
    int? EnergyAfterAttempt = null)
{
    public int AllowedAttempts => Decision == PetTrainingEnergyDecision.OneAttempt ? 1 : 0;
    public bool CrownShopLocked => true;
}

/// <summary>One-attempt admission using observed yellow-orb Energy and a verified
/// current game cost. Every later attempt requires a fresh observation; there is
/// no refill, purchase, retry or input operation in this gate.</summary>
internal static class PetTrainingEnergyGate
{
    public static PetTrainingEnergyPlan Evaluate(PetTrainingEnergySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.EnergyVerified || !snapshot.CostVerified || !snapshot.ObservationFresh ||
            snapshot.Current is not int current || snapshot.NextAttemptCost is not int cost ||
            snapshot.Reserve is not int reserve || current < 0 || cost < 0 || reserve < 0)
            return new(PetTrainingEnergyDecision.ObserveOnly, "energy-cost-or-reserve-unverified");
        if (current - cost < reserve)
            return new(PetTrainingEnergyDecision.Stop, "energy-reserve-would-be-crossed");
        return new(PetTrainingEnergyDecision.OneAttempt, "one-attempt-within-energy-reserve", current - cost);
    }
}
