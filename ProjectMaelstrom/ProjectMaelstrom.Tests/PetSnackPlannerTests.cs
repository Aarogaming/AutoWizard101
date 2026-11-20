using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PetSnackPlannerTests
{
    private static PetSnackCandidate Snack(string key = "snack-a", int? xp = 10, int? happiness = 2) =>
        new(key, IdentityVerified: true, Protected: false, Quantity: 4, MinimumRemaining: 1,
            TrainingXpBenefit: xp, HappinessBenefit: happiness);

    private static PetSnackPolicy Policy() => new("pet-1", TrainingEnabled: true, HappinessEnabled: true,
        AllowedSnackProfileKeys: new[] { "snack-a", "snack-b", "snack-c" });

    private static PetSnackSnapshot Snapshot(PetSnackMode mode = PetSnackMode.TrainingXp) => new(
        Context: mode == PetSnackMode.TrainingXp ? PetSnackContext.TrainingFeed : PetSnackContext.HappinessFeed,
        ContextVerified: true, PetProfileKey: "pet-1", PetVerified: true,
        OpportunityId: "synthetic-opportunity-1", OpportunityVerified: true,
        TrainingGameComplete: mode == PetSnackMode.TrainingXp,
        ObservationFresh: true, InteractionReady: true,
        TrainingXp: new(10, 30, 100), Happiness: new(10, 30, 100),
        Budget: new(1, 5, 5, 0), Snacks: new[] { Snack() });

    private static PetSnackPlan Plan(PetSnackSnapshot snapshot, PetSnackPolicy? policy = null,
        PetSnackMode mode = PetSnackMode.TrainingXp) => PetSnackPlanner.Plan(mode, snapshot, policy ?? Policy());

    private static void NoAction(PetSnackPlan plan, string reason)
    {
        Assert.Null(plan.Suggestion);
        Assert.Equal(reason, plan.Reason);
    }

    [Fact]
    public void BothModesDefaultToDisabledAndEnableIndependently()
    {
        NoAction(Plan(Snapshot(), new()), "mode-disabled");
        NoAction(Plan(Snapshot(PetSnackMode.Happiness), new(), PetSnackMode.Happiness), "mode-disabled");
        NoAction(Plan(Snapshot(), Policy() with { TrainingEnabled = false }), "mode-disabled");
        NoAction(Plan(Snapshot(PetSnackMode.Happiness), Policy() with { HappinessEnabled = false }, PetSnackMode.Happiness), "mode-disabled");
        Assert.NotNull(Plan(Snapshot(), Policy() with { HappinessEnabled = false }).Suggestion);
    }

    [Fact]
    public void RejectsWrongAndUnverifiedFeedingContexts()
    {
        foreach (var context in new[] { PetSnackContext.Unknown, PetSnackContext.InventoryItemFeed, PetSnackContext.HappinessFeed })
            NoAction(Plan(Snapshot() with { Context = context }), "wrong-or-unverified-context");
        NoAction(Plan(Snapshot() with { ContextVerified = false }), "wrong-or-unverified-context");
        NoAction(Plan(Snapshot(), mode: PetSnackMode.Happiness), "wrong-or-unverified-context");
        NoAction(Plan(Snapshot(), mode: (PetSnackMode)999), "unknown-mode");
    }

    [Fact]
    public void RequiresTheExplicitVerifiedPet()
    {
        NoAction(Plan(Snapshot() with { PetVerified = false }), "wrong-or-unverified-pet");
        NoAction(Plan(Snapshot() with { PetProfileKey = "pet-2" }), "wrong-or-unverified-pet");
        NoAction(Plan(Snapshot(), Policy() with { PetProfileKey = null }), "wrong-or-unverified-pet");
        NoAction(Plan(Snapshot(), Policy() with { PetProfileKey = "PET-1" }), "wrong-or-unverified-pet");
    }

    [Fact]
    public void RequiresAnEligibleWholeGameOpportunityAndNoUnresolvedConsumption()
    {
        NoAction(Plan(Snapshot() with { OpportunityId = " " }), "unverified-opportunity");
        NoAction(Plan(Snapshot() with { OpportunityVerified = false }), "unverified-opportunity");
        NoAction(Plan(Snapshot() with { TrainingGameComplete = false }), "unverified-opportunity");
        NoAction(Plan(Snapshot() with { OpportunityAlreadyConsumed = true }), "opportunity-needs-reconciliation");
        NoAction(Plan(Snapshot() with { HasUnresolvedConsumption = true }), "opportunity-needs-reconciliation");
        Assert.NotNull(Plan(Snapshot(PetSnackMode.Happiness), mode: PetSnackMode.Happiness).Suggestion);
    }

    [Fact]
    public void FocusLossStaleEvidenceAndUserSkipProduceNoSuggestion()
    {
        NoAction(Plan(Snapshot() with { InteractionReady = false }), "stale-or-unready-context");
        NoAction(Plan(Snapshot() with { ObservationFresh = false }), "stale-or-unready-context");
        NoAction(Plan(Snapshot(), Policy() with { SkipOpportunity = true }), "user-skipped");
    }

    [Fact]
    public void UnknownInvalidCompleteAndCappedGoalsProduceNoSuggestion()
    {
        foreach (var goal in new PetSnackProgress?[] { null, new(null, 30, 100), new(10, null, 100),
                     new(10, 30, null), new(-1, 30, 100), new(10, 101, 100), new(101, 30, 100) })
            NoAction(Plan(Snapshot() with { TrainingXp = goal }), "unknown-or-invalid-goal");
        NoAction(Plan(Snapshot() with { TrainingXp = new(30, 30, 100) }), "goal-complete");
        NoAction(Plan(Snapshot() with { TrainingXp = new(31, 30, 100) }), "goal-complete");
        NoAction(Plan(Snapshot() with { TrainingXp = new(100, 100, 100) }), "goal-capped");
        NoAction(Plan(Snapshot(PetSnackMode.Happiness) with { Happiness = new(30, 30, 100) },
            mode: PetSnackMode.Happiness), "goal-complete");
    }

    [Fact]
    public void UnknownExhaustedOrInsufficientResourceBudgetsBlockTheProposal()
    {
        foreach (var budget in new PetSnackBudget?[] { null, new(), new(null, 5, 0, 0), new(0, 5, 0, 0),
                     new(-1, 5, 0, 0), new(1, null, 0, 0), new(1, 5, null, 0), new(1, 5, 0, null),
                     new(1, 5, 5, 1), new(1, -1, 0, 0), new(1, 5, -1, 0), new(1, 5, 0, -1) })
            NoAction(Plan(Snapshot() with { Budget = budget }), "unknown-or-exhausted-budget");
        Assert.NotNull(Plan(Snapshot() with { Budget = new(1, 6, 5, 1) }).Suggestion);
    }

    [Fact]
    public void ProtectedAndReservedStockNeverCompetesWithEligibleStock()
    {
        var plan = Plan(Snapshot() with
        {
            Snacks = new[] {
            Snack("snack-a", 100) with { Protected = true },
            Snack("snack-b", 100) with { Quantity = 5, MinimumRemaining = 5 },
            Snack("snack-c", 5) }
        });
        Assert.Equal("snack-c", plan.Suggestion?.SnackProfileKey);
        Assert.Contains(plan.Rejections, rejection => rejection.ProfileKey == "snack-a" && rejection.Reason == "protected-stock");
        Assert.Contains(plan.Rejections, rejection => rejection.ProfileKey == "snack-b" && rejection.Reason == "stock-reserve");
        Assert.NotNull(Plan(Snapshot() with { Snacks = new[] { Snack() with { Quantity = 2, MinimumRemaining = 1 } } }).Suggestion);
    }

    [Fact]
    public void UnknownStockIdentityAndMissingAllowlistFailClosed()
    {
        foreach (var snack in new[] { Snack() with { IdentityVerified = false }, Snack() with { Quantity = null },
                     Snack() with { Quantity = -1 }, Snack() with { MinimumRemaining = null },
                     Snack() with { MinimumRemaining = -1 }, Snack() with { ProfileKey = " " } })
            NoAction(Plan(Snapshot() with { Snacks = new[] { snack } }), "no-eligible-snack");
        NoAction(Plan(Snapshot(), Policy() with { AllowedSnackProfileKeys = null }), "no-eligible-snack");
        NoAction(Plan(Snapshot(), Policy() with { AllowedSnackProfileKeys = new[] { "SNACK-A" } }), "no-eligible-snack");
        NoAction(Plan(Snapshot() with { Snacks = null }), "no-eligible-snack");
    }

    [Fact]
    public void MissingOrNonpositiveBenefitIsNotInferredFromTheOtherMode()
    {
        foreach (int? benefit in new int?[] { null, 0, -1 })
        {
            NoAction(Plan(Snapshot() with { Snacks = new[] { Snack(xp: benefit, happiness: 99) } }), "no-eligible-snack");
            NoAction(Plan(Snapshot(PetSnackMode.Happiness) with { Snacks = new[] { Snack(xp: 99, happiness: benefit) } },
                mode: PetSnackMode.Happiness), "no-eligible-snack");
        }
    }

    [Fact]
    public void TrainingXpAndHappinessSelectFromTheirOwnObservedBenefits()
    {
        var snacks = new[] { Snack("snack-a", xp: 20, happiness: 1), Snack("snack-b", xp: 1, happiness: 20) };
        var training = Plan(Snapshot() with { Snacks = snacks });
        var happiness = Plan(Snapshot(PetSnackMode.Happiness) with { Snacks = snacks }, mode: PetSnackMode.Happiness);
        Assert.Equal("snack-a", training.Suggestion?.SnackProfileKey);
        Assert.Equal("snack-b", happiness.Suggestion?.SnackProfileKey);
        Assert.Equal(PetSnackMode.Happiness, happiness.Suggestion?.Mode);
    }

    [Fact]
    public void SelectionMaximizesGoalProgressThenMinimizesOvershoot()
    {
        var plan = Plan(Snapshot() with { Snacks = new[] { Snack("snack-a", 19), Snack("snack-b", 30), Snack("snack-c", 20) } });
        Assert.Equal("snack-c", plan.Suggestion?.SnackProfileKey);
        Assert.Equal(20, plan.Suggestion?.UsefulBenefit);
        Assert.Equal(0, plan.Suggestion?.Overshoot);
        Assert.Equal("most-goal-progress-then-least-overshoot", plan.Reason);
    }

    [Fact]
    public void TiesUseOrdinalIdentityIndependentOfInputOrder()
    {
        var snacks = new[] { Snack("snack-b", 20), Snack("snack-a", 20), Snack("snack-c", 20) with { Protected = true } };
        var first = Plan(Snapshot() with { Snacks = snacks });
        var second = Plan(Snapshot() with { Snacks = snacks.Reverse().ToArray() });
        Assert.Equal("snack-a", first.Suggestion?.SnackProfileKey);
        Assert.Equal(first.Suggestion, second.Suggestion);
        Assert.Equal(first.Rejections.ToArray(), second.Rejections.ToArray());
    }

    [Fact]
    public void ConflictingDuplicateIdentitiesCannotProduceAConsumptionSuggestion()
    {
        var plan = Plan(Snapshot() with { Snacks = new[] { Snack("snack-a", 1), Snack("snack-a", 100), Snack("snack-b", 5) } });
        Assert.Equal("snack-b", plan.Suggestion?.SnackProfileKey);
        Assert.Equal(2, plan.Rejections.Count);
        Assert.All(plan.Rejections, rejection => Assert.Equal("ambiguous-duplicate-identity", rejection.Reason));
    }

    [Fact]
    public void ProposalContainsOneItemAndDoesNotMutateOrAccountForConsumption()
    {
        var snapshot = Snapshot();
        var before = snapshot.Snacks!.ToArray();
        var first = Plan(snapshot);
        var second = Plan(snapshot);
        Assert.Equal(first.Suggestion, second.Suggestion);
        Assert.Equal(1, first.Suggestion?.Quantity);
        Assert.Equal(snapshot.OpportunityId, first.Suggestion?.OpportunityId);
        Assert.Equal(snapshot.PetProfileKey, first.Suggestion?.PetProfileKey);
        Assert.Equal(before, snapshot.Snacks!.ToArray());
        Assert.Equal(1, snapshot.Budget!.FeedsRemaining);
        Assert.Equal(4, snapshot.Snacks![0].Quantity);
    }
}
