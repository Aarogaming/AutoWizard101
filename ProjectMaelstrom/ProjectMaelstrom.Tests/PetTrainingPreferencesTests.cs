using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetTrainingPreferencesTests
{
    private sealed class Files : IDisposable
    {
        public string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Maelstrom pet preferences " + Guid.NewGuid().ToString("N"));
        public string Path => System.IO.Path.Combine(Root, PetTrainingPreferences.FileName);
        public Files() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, true);
    }

    private static PetTrainingPreferences Enabled() => new(Snacks: PetSnackPreference.SelectedSnacks,
        MaxFeedsPerVisit: 3, MinimumSnackRemaining: 2, EnergyReserve: 5, GoalTarget: 100,
        AllowedSnackNames: ["Apple", "Cake"], ProtectedSnackNames: ["Cake"]);

    private static PetSnackSnapshot Snapshot() => new(PetSnackContext.TrainingFeed,
        ContextVerified: true, PetProfileKey: "pet-verified", PetVerified: true,
        OpportunityId: "game-1", OpportunityVerified: true, TrainingGameComplete: true,
        ObservationFresh: true, InteractionReady: true,
        TrainingXp: new(50, 100, 250), Budget: new(3, 10, 0, 0),
        Snacks: [new("apple-key", true, false, 5, 0, 5), new("cake-key", true, false, 10, 0, 50)]);

    private static readonly IReadOnlyDictionary<string, string> Bindings = new Dictionary<string, string>
    { ["Apple"] = "apple-key", ["Cake"] = "cake-key" };

    [Fact]
    public void MissingSettingsDefaultToNoFeedingAndNativeIdle()
    {
        using var files = new Files();
        var settings = PetTrainingPreferences.Load(files.Root);
        Assert.Equal(PetSnackPreference.NeverFeed, settings.Snacks);
        Assert.Equal(PetDanceArena.WizardCity, settings.Arena);
        Assert.True(settings.ReturnToNativeIdle);
        Assert.Equal(0, settings.MaxFeedsPerVisit);
        Assert.False(settings.AutomatedFeedingAvailable);
        Assert.False(File.Exists(files.Path));
        Assert.Null(settings.PrepareSnackReview(Snapshot(), Bindings).Suggestion);
    }

    [Fact]
    public void ChoicesRoundTripUnderUserDataWithoutChangingReports()
    {
        using var files = new Files();
        var sentinel = System.IO.Path.Combine(files.Root, "recorded-game.txt");
        File.WriteAllText(sentinel, "preserve recording");
        var settings = Enabled() with
        {
            Arena = PetDanceArena.Dragonspyre,
            ReturnToNativeIdle = false,
            Goal = PetTrainingGoal.Agility,
            AllowedSnackNames = [" Apple ", "apple", "Cake"]
        };
        PetTrainingPreferences.Save(files.Root, settings);
        var loaded = PetTrainingPreferences.Load(files.Root);
        Assert.Equal(settings.Arena, loaded.Arena);
        Assert.False(loaded.ReturnToNativeIdle);
        Assert.Equal(settings.Goal, loaded.Goal);
        Assert.Equal(settings.MaxFeedsPerVisit, loaded.MaxFeedsPerVisit);
        Assert.Equal(settings.GoalTarget, loaded.GoalTarget);
        Assert.Equal(new[] { "Apple", "Cake" }, loaded.AllowedSnackNames);
        Assert.Equal("preserve recording", File.ReadAllText(sentinel));
        Assert.Empty(Directory.GetFiles(files.Root, "*.tmp"));
        Assert.Contains("\"SelectedSnacks\"", File.ReadAllText(files.Path));
        Assert.DoesNotContain("AutomatedFeedingAvailable", File.ReadAllText(files.Path));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"Snacks\":\"SelectedSnacks\"}")]
    [InlineData("{\"SchemaVersion\":2,\"Snacks\":\"SelectedSnacks\"}")]
    [InlineData("{\"SchemaVersion\":1,\"Snacks\":\"UseEverything\"}")]
    [InlineData("{\"SchemaVersion\":1,\"Snacks\":2}")]
    [InlineData("{\"SchemaVersion\":1,\"Snacks\":\"SelectedSnacks\",\"MaxFeedsPerVisit\":-1}")]
    [InlineData("{\"SchemaVersion\":1,\"Snacks\":\"SelectedSnacks\",\"MaxFeedsPerVisit\":101}")]
    [InlineData("{\"SchemaVersion\":1,\"Snacks\":\"SelectedSnacks\",\"AllowedSnackNames\":[\"  \"]}")]
    [InlineData("{\"SchemaVersion\":1,\"Snacks\":\"SelectedSnacks\",\"ProtectedSnackNames\":[null]}")]
    [InlineData("{\"SchemaVersion\":1,\"Snacks\":\"SelectedSnacks\",\"EnergyReserve\":-1}")]
    [InlineData("{\"SchemaVersion\":1,\"Snacks\":\"NeverFeed\",\"Snacks\":\"SelectedSnacks\"}")]
    [InlineData("{\"SchemaVersion\":1,\"UnsafeAutomaticFeeding\":true}")]
    public void InvalidFilesFallBackWithoutOverwritingTheirEvidence(string text)
    {
        using var files = new Files();
        File.WriteAllText(files.Path, text);
        var settings = PetTrainingPreferences.Load(files.Root);
        Assert.Equal(PetSnackPreference.NeverFeed, settings.Snacks);
        Assert.Equal(0, settings.MaxFeedsPerVisit);
        Assert.Equal(text, File.ReadAllText(files.Path));
    }

    [Fact]
    public void OversizedFilesAreNotReadAsActivePreferences()
    {
        using var files = new Files();
        File.WriteAllText(files.Path, new string(' ', PetTrainingPreferences.MaximumFileBytes + 1));
        Assert.Equal(PetSnackPreference.NeverFeed, PetTrainingPreferences.Load(files.Root).Snacks);
    }

    [Fact]
    public void InvalidSaveCannotOverwriteExistingPreferences()
    {
        using var files = new Files();
        PetTrainingPreferences.Save(files.Root, new());
        var before = File.ReadAllBytes(files.Path);
        Assert.Throws<InvalidDataException>(() => PetTrainingPreferences.Save(files.Root, Enabled() with { AllowedSnackNames = ["bad\nname"] }));
        Assert.Equal(before, File.ReadAllBytes(files.Path));
        Assert.Throws<ArgumentException>(() => PetTrainingPreferences.Save("relative-root", new()));
    }

    [Fact]
    public void ProtectedSnackOverridesTheAllowListAndPreservesObservedStock()
    {
        var snapshot = Snapshot();
        var plan = Enabled().PrepareSnackReview(snapshot, Bindings);
        Assert.Equal("apple-key", plan.Suggestion!.SnackProfileKey);
        Assert.Contains(plan.Rejections, rejection => rejection.ProfileKey == "cake-key");
        Assert.False(snapshot.Snacks![0].Protected);
        Assert.Equal(0, snapshot.Snacks[0].MinimumRemaining);
        Assert.Equal(3, snapshot.Budget!.FeedsRemaining);
        Assert.False(Enabled().AutomatedFeedingAvailable);
    }

    [Fact]
    public void ANameWithoutVerifiedIdentityCannotBecomeASnackProposal()
    {
        var plan = Enabled().PrepareSnackReview(Snapshot(), new Dictionary<string, string>());
        Assert.Null(plan.Suggestion);
        var unverified = Snapshot() with { PetVerified = false };
        Assert.Null(Enabled().PrepareSnackReview(unverified, Bindings).Suggestion);
        var missingQuantity = Snapshot() with { Snacks = [new("apple-key", true, false, null, 0, 5)] };
        Assert.Null(Enabled().PrepareSnackReview(missingQuantity, Bindings).Suggestion);
    }

    [Theory]
    [InlineData("consumed")]
    [InlineData("unresolved")]
    [InlineData("stale")]
    [InlineData("budget-spent")]
    [InlineData("unknown-reserve")]
    [InlineData("stock-reserve")]
    [InlineData("protected-observation")]
    public void SettingsNeverRestoreAuthorityLostInObservedEvidence(string fault)
    {
        var snapshot = fault switch
        {
            "consumed" => Snapshot() with { OpportunityAlreadyConsumed = true },
            "unresolved" => Snapshot() with { HasUnresolvedConsumption = true },
            "stale" => Snapshot() with { ObservationFresh = false },
            "budget-spent" => Snapshot() with { Budget = new(0, 10, 0, 0) },
            "unknown-reserve" => Snapshot() with { Budget = new(3, 10, null, 0) },
            "stock-reserve" => Snapshot() with { Snacks = [new("apple-key", true, false, 2, 0, 5)] },
            _ => Snapshot() with { Snacks = [new("apple-key", true, true, 5, 0, 5)] }
        };
        Assert.Null(Enabled().PrepareSnackReview(snapshot, Bindings).Suggestion);
    }

    [Fact]
    public void SuggestOnlyRemainsAReadOnlyProposalAndCannotEnableConsumption()
    {
        var settings = Enabled() with { Snacks = PetSnackPreference.SuggestOnly };
        Assert.NotNull(settings.PrepareSnackReview(Snapshot(), Bindings).Suggestion);
        Assert.False(settings.AutomatedFeedingAvailable);
        Assert.Contains("Feeding is not enabled", settings.SnackStatus);
    }

    [Fact]
    public void StatObjectivesArePreservedWithoutPretendingXpRanksTheDesiredStat()
    {
        var settings = Enabled() with { Goal = PetTrainingGoal.Strength };
        var plan = settings.PrepareSnackReview(Snapshot(), Bindings);
        Assert.Null(plan.Suggestion);
        Assert.Equal("stat-goal-needs-verified-benefits", plan.Reason);
    }

    [Fact]
    public void ConflictingNameBindingsAreNotResolvedByGuessing()
    {
        var bindings = new Dictionary<string, string> { ["Apple"] = "one", [" apple "] = "two" };
        Assert.Equal("ambiguous-snack-name-bindings", Enabled().PrepareSnackReview(Snapshot(), bindings).Reason);
    }
}
