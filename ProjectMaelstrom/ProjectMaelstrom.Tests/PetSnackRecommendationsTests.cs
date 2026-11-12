using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PetSnackRecommendationsTests
{
    private static PetTrainingPreferences Preferences(PetTrainingGoal goal = PetTrainingGoal.TrainingXp) => new(
        Snacks: PetSnackPreference.SuggestOnly, Goal: goal, MaxFeedsPerVisit: 2,
        MinimumSnackRemaining: 1, EnergyReserve: 10,
        AllowedSnackNames: ["Snack A", "Snack B", "Snack C"]);

    private static PetSnackRecommendationCandidate Snack(string key = "a", string name = "Snack A",
        int xp = 10, int agility = 6) => new(
        new(key, IdentityVerified: true, Protected: false, Quantity: 4, MinimumRemaining: 1),
        name, NameVerified: true, School: "Fire", SchoolVerified: true,
        Benefits: new Dictionary<PetTrainingGoal, PetSnackBenefitEvidence>
        {
            [PetTrainingGoal.TrainingXp] = new(xp, true),
            [PetTrainingGoal.Agility] = new(agility, true)
        });

    private static PetSnackRecommendationSnapshot Snapshot(params PetSnackRecommendationCandidate[] snacks) => new(
        new(Context: PetSnackContext.TrainingFeed, ContextVerified: true, PetProfileKey: "pet-1",
            PetVerified: true, OpportunityId: "game-1", OpportunityVerified: true,
            TrainingGameComplete: true, ObservationFresh: true, InteractionReady: true,
            Budget: new(2, 10, 0, 0)),
        ExpectedPetProfileKey: "pet-1", PetSchool: "Fire", PetSchoolVerified: true, ProgressStable: true,
        Progress: new Dictionary<PetTrainingGoal, PetSnackProgressEvidence>
        {
            [PetTrainingGoal.TrainingXp] = new(new(10, 30, 100), true),
            [PetTrainingGoal.Agility] = new(new(90, 100, 100), true)
        },
        Snacks: snacks.Length == 0 ? [Snack()] : snacks);

    private static PetSnackRecommendationReview Review(PetSnackRecommendationSnapshot? snapshot = null,
        PetTrainingPreferences? preferences = null) => PetSnackRecommendations.Review(snapshot ?? Snapshot(), preferences ?? Preferences());

    [Fact]
    public void NeverFeedIsDefaultAndAllResultsKeepPurchasesAndFeedingUnavailable()
    {
        var disabled = Review(preferences: new());
        Assert.Empty(disabled.Ranked);
        Assert.Equal("snacks-disabled", disabled.Reason);
        foreach (var result in new[] { disabled, Review(), Review(preferences: Preferences() with { Snacks = PetSnackPreference.SelectedSnacks }) })
        {
            Assert.True(result.CrownShopLocked);
            Assert.False(result.AutomaticFeedingAvailable);
        }
    }

    [Fact]
    public void UsesSelectedAttributeInsteadOfAssumingXpIsUseful()
    {
        var snapshot = Snapshot(Snack(xp: 99, agility: 1), Snack("b", "Snack B", xp: 1, agility: 10));
        Assert.Equal("a", Review(snapshot).Ranked[0].SnackProfileKey);
        var agility = Review(snapshot, Preferences(PetTrainingGoal.Agility));
        Assert.Equal("b", agility.Ranked[0].SnackProfileKey);
        Assert.Equal(PetTrainingGoal.Agility, agility.Ranked[0].Goal);
        Assert.Equal(10, agility.Ranked[0].UsefulBenefit);
    }

    [Fact]
    public void CapsAndRemainingTargetDetermineUsefulBenefitAndOvershoot()
    {
        var result = Review(Snapshot(Snack(agility: 30), Snack("b", "Snack B", agility: 10),
            Snack("c", "Snack C", agility: 9)), Preferences(PetTrainingGoal.Agility));
        Assert.Equal(new[] { "b", "a", "c" }, result.Ranked.Select(item => item.SnackProfileKey));
        Assert.Equal(10, result.Ranked[1].UsefulBenefit);
        Assert.Equal(20, result.Ranked[1].Overshoot);
        Assert.Equal(0, result.Ranked[0].Overshoot);
        Assert.Empty(Review(preferences: Preferences() with { GoalTarget = 101 }).Ranked);
        Assert.Empty(Review(preferences: Preferences() with { GoalTarget = 10 }).Ranked);
    }

    [Fact]
    public void UnknownGoalsCapsAndUnstableAgeNeverGenerateRecommendations()
    {
        var baseline = Snapshot();
        Assert.Empty(Review(baseline with { ProgressStable = false }).Ranked);
        Assert.Empty(Review(baseline with { Progress = null }).Ranked);
        foreach (var value in new[] { new PetSnackProgressEvidence(new(10, 30, 100), false),
                     new PetSnackProgressEvidence(new(10, 30, null), true),
                     new PetSnackProgressEvidence(new(100, 100, 100), true),
                     new PetSnackProgressEvidence(new(101, 100, 100), true) })
            Assert.Empty(Review(baseline with
            {
                Progress = new Dictionary<PetTrainingGoal, PetSnackProgressEvidence>
                { [PetTrainingGoal.TrainingXp] = value }
            }).Ranked);
    }

    [Fact]
    public void UnknownBenefitsAreWithheldEvenIfTheItemHasKnownXpElsewhere()
    {
        var item = Snack() with
        {
            Inventory = Snack().Inventory with { TrainingXpBenefit = 99 },
            Benefits = new Dictionary<PetTrainingGoal, PetSnackBenefitEvidence>
            { [PetTrainingGoal.Agility] = new(10, false) }
        };
        Assert.Empty(Review(Snapshot(item)).Ranked);
        Assert.Empty(Review(Snapshot(item), Preferences(PetTrainingGoal.Agility)).Ranked);
        Assert.Empty(Review(Snapshot(item with { Benefits = null })).Ranked);
    }

    [Fact]
    public void ProtectionAndReservesOverrideAllowlistsAndLargeRewards()
    {
        var result = Review(Snapshot(Snack(xp: 100), Snack("b", "Snack B", xp: 50) with
        { Inventory = Snack("b").Inventory with { Quantity = 1 } }, Snack("c", "Snack C", xp: 5)),
            Preferences() with { ProtectedSnackNames = ["snack a"] });
        Assert.Equal("c", Assert.Single(result.Ranked).SnackProfileKey);
        Assert.Contains(result.Rejections, item => item.ProfileKey == "a" && item.Reason == "protected-stock");
        Assert.Contains(result.Rejections, item => item.ProfileKey == "b" && item.Reason == "stock-reserve");
        Assert.Empty(Review(Snapshot(Snack() with { Inventory = Snack().Inventory with { Protected = true } })).Ranked);
        Assert.Empty(Review(Snapshot(Snack() with { Inventory = Snack().Inventory with { MinimumRemaining = null } })).Ranked);
        Assert.Empty(Review(preferences: Preferences() with { MinimumSnackRemaining = 4 }).Ranked);
    }

    [Fact]
    public void NamesRequireVerificationAndUnambiguousIdentity()
    {
        Assert.Empty(Review(preferences: Preferences() with { AllowedSnackNames = [] }).Ranked);
        Assert.Empty(Review(Snapshot(Snack() with { NameVerified = false })).Ranked);
        Assert.Empty(Review(Snapshot(Snack(), Snack("b", "Snack A"))).Ranked);
        Assert.Empty(Review(Snapshot(Snack(), Snack("a", "Snack B") with { NameVerified = false })).Ranked);
        Assert.NotEmpty(Review(Snapshot(Snack(name: " snack a "))).Ranked);
    }

    [Fact]
    public void SchoolContextNeverInventsPreferenceOrBonus()
    {
        var same = Assert.Single(Review().Ranked);
        Assert.True(same.SchoolMatchesPet);
        Assert.Equal(PetSnackRelationship.Unknown, same.Relationship);
        Assert.Equal(10, same.ExpectedBenefit);
        var different = Assert.Single(Review(Snapshot(Snack() with { School = "Ice" })).Ranked);
        Assert.False(different.SchoolMatchesPet);
        Assert.Equal(same.UsefulBenefit, different.UsefulBenefit);
        var unknown = Assert.Single(Review(Snapshot() with { PetSchoolVerified = false }).Ranked);
        Assert.Null(unknown.SchoolMatchesPet);
        Assert.Equal(same.ExpectedBenefit, unknown.ExpectedBenefit);
    }

    [Fact]
    public void VerifiedPreferenceBreaksTiesButDoesNotAddAnotherBonus()
    {
        var liked = Snack() with { Relationship = PetSnackRelationship.Liked, RelationshipVerified = true, RelationshipPetProfileKey = "pet-1" };
        var loved = Snack("b", "Snack B") with { Relationship = PetSnackRelationship.Loved, RelationshipVerified = true, RelationshipPetProfileKey = "pet-1" };
        var result = Review(Snapshot(liked, loved));
        Assert.Equal("b", result.Ranked[0].SnackProfileKey);
        Assert.Equal(10, result.Ranked[0].ExpectedBenefit);
        Assert.Equal("a", Review(Snapshot(liked, loved with { RelationshipPetProfileKey = "pet-2" })).Ranked[0].SnackProfileKey);
        Assert.Equal(PetSnackRelationship.Unknown, Assert.Single(Review(Snapshot(loved with { RelationshipVerified = false })).Ranked).Relationship);
    }

    [Fact]
    public void ReusesContextPetOpportunityAndBudgetGates()
    {
        var baseline = Snapshot();
        foreach (var feeding in new[]
        {
            baseline.Feeding with { Context = PetSnackContext.InventoryItemFeed },
            baseline.Feeding with { PetVerified = false },
            baseline.Feeding with { PetProfileKey = "pet-2" },
            baseline.Feeding with { TrainingGameComplete = false },
            baseline.Feeding with { HasUnresolvedConsumption = true },
            baseline.Feeding with { ObservationFresh = false },
            baseline.Feeding with { Budget = new(0, 10, 0, 0) },
            baseline.Feeding with { Budget = new(1, 9, 0, 0) },
            baseline.Feeding with { Budget = new(1, 10, null, 0) }
        }) Assert.Empty(Review(baseline with { Feeding = feeding }).Ranked);
        Assert.Empty(Review(preferences: Preferences() with { MaxFeedsPerVisit = 0 }).Ranked);
        Assert.Empty(Review(baseline with { ExpectedPetProfileKey = null }).Ranked);
        Assert.NotEmpty(Review(baseline with { Feeding = baseline.Feeding with { Budget = new(1, 10, 10, 0) } }).Ranked);
    }

    [Fact]
    public void RankingsAreDeterministicAndDoNotConsumeTheSnapshot()
    {
        var items = new[] { Snack("b", "Snack B"), Snack() };
        var snapshot = Snapshot(items);
        var result = Review(snapshot);
        Assert.Equal(new[] { "a", "b" }, result.Ranked.Select(item => item.SnackProfileKey));
        Assert.Equal(result.Ranked.Select(item => item.SnackProfileKey), Review(Snapshot(items.Reverse().ToArray())).Ranked.Select(item => item.SnackProfileKey));
        Assert.Equal(4, snapshot.Snacks![0].Inventory.Quantity);
        Assert.Equal(2, snapshot.Feeding.Budget!.FeedsRemaining);
        Assert.Equal(3, result.Ranked[0].RemainingStock);
    }
}

public class PetTrainingEnergyGateTests
{
    private static PetTrainingEnergySnapshot Known() => new(20, 4, 10, true, true, true);

    [Fact]
    public void UnknownOrStaleEnergyCostOrReserveAllowsObservationOnly()
    {
        foreach (var snapshot in new[] { new PetTrainingEnergySnapshot(), Known() with { Current = null },
                     Known() with { NextAttemptCost = null }, Known() with { Reserve = null },
                     Known() with { EnergyVerified = false }, Known() with { CostVerified = false },
                     Known() with { ObservationFresh = false }, Known() with { Current = -1 },
                     Known() with { NextAttemptCost = -1 }, Known() with { Reserve = -1 } })
        {
            var result = PetTrainingEnergyGate.Evaluate(snapshot);
            Assert.Equal(PetTrainingEnergyDecision.ObserveOnly, result.Decision);
            Assert.Equal(0, result.AllowedAttempts);
            Assert.Null(result.EnergyAfterAttempt);
            Assert.True(result.CrownShopLocked);
        }
    }

    [Fact]
    public void ReserveBoundaryAllowsExactlyOneAttemptAndNoPurchase()
    {
        var boundary = PetTrainingEnergyGate.Evaluate(Known() with { Current = 14 });
        Assert.Equal(PetTrainingEnergyDecision.OneAttempt, boundary.Decision);
        Assert.Equal(1, boundary.AllowedAttempts);
        Assert.Equal(10, boundary.EnergyAfterAttempt);
        Assert.True(boundary.CrownShopLocked);
        var low = PetTrainingEnergyGate.Evaluate(Known() with { Current = 13 });
        Assert.Equal(PetTrainingEnergyDecision.Stop, low.Decision);
        Assert.Equal(0, low.AllowedAttempts);
        Assert.True(low.CrownShopLocked);
        Assert.Equal(1, PetTrainingEnergyGate.Evaluate(Known() with { Current = int.MaxValue }).AllowedAttempts);
    }
}
