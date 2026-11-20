using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101QuestIntentTests
{
    [Theory]
    [InlineData("Go To Fireglobe Theatre in Firecat Alley", "Destination", "Fireglobe Theatre", "Firecat Alley", "navigate", true)]
    [InlineData("Go To The Grand Arena in Krokosphinx", "Destination", "The Grand Arena", "Krokosphinx", "navigate", true)]
    [InlineData("Explore Dark Cave in Wizard City", "Explore", "Dark Cave", "Wizard City", "navigate", false)]
    [InlineData("Talk To Private Quinn in Firecat Alley", "Talk", "Private Quinn", "Firecat Alley", "interact", false)]
    [InlineData("Defeat Lost Souls in Unicorn Way", "Defeat", "Lost Souls", "Unicorn Way", "combat", false)]
    [InlineData("Collect Crates in Triton Avenue", "Collect", "Crates", "Triton Avenue", "collect", false)]
    public void VerbFamiliesProducePortableDataWithoutQuestSpecificBranches(string objective,
        string action, string target, string area, string executionKey, bool arrivalOnly)
    {
        var result = W101QuestIntent.Parse(objective);
        Assert.True(result.IsRecognized); Assert.Null(result.UnsupportedReason);
        Assert.Equal(action, result.Action.ToString()); Assert.Equal(target, result.Target); Assert.Equal(area, result.Area);
        Assert.Equal(executionKey, result.ExecutionKey); Assert.Equal(arrivalOnly, result.ArrivalOnly);
        Assert.Equal(objective, result.RawObjective);
    }

    [Theory]
    [InlineData("  gO\tTO   Fireglobe Theatre  IN  Firecat Alley  ", "Fireglobe Theatre", "Firecat Alley")]
    [InlineData("go to fireglobe theatre in FIRECAT ALLEY", "fireglobe theatre", "FIRECAT ALLEY")]
    [InlineData("Go\u00a0To René's Gate in North-West Ward", "René's Gate", "North-West Ward")]
    [InlineData("Go To Tower 2 in St. Agnes’ Court", "Tower 2", "St. Agnes’ Court")]
    public void NormalizesGrammarAndHorizontalWhitespaceButPreservesProperNamesAndRawText(
        string raw, string target, string area)
    {
        var result = W101QuestIntent.Parse(raw);
        Assert.True(result.IsRecognized); Assert.Equal(target, result.Target); Assert.Equal(area, result.Area);
        Assert.Equal(raw, result.RawObjective);
    }

    [Theory]
    [InlineData(null, "missing-objective")]
    [InlineData("  ", "missing-objective")]
    [InlineData("Buy Snacks in Wizard City", "unsupported-verb")]
    [InlineData("Find Fireglobe Theatre in Firecat Alley", "unsupported-verb")]
    [InlineData("Please Go To Fireglobe Theatre in Firecat Alley", "unsupported-verb")]
    [InlineData("Go To Fireglobe Theatre", "missing-target-or-area")]
    [InlineData("Go To Fireglobe Theatre in", "missing-target-or-area")]
    [InlineData("Go To in Firecat Alley", "missing-target-or-area")]
    [InlineData("Go To Gate in Ruins in Wizard City", "ambiguous-area")]
    [InlineData("Go To Theatre in Firecat Alley; buy snacks", "unsupported-name-or-compound-objective")]
    [InlineData("Go To Theatre and then talk in Firecat Alley", "unsupported-name-or-compound-objective")]
    [InlineData("Go To Theatre in Firecat Alley or Olde Town", "unsupported-name-or-compound-objective")]
    [InlineData("Go To Theatre in Firecat Alley\nTalk To Quinn", "invalid-objective-text")]
    [InlineData("Go To Theatre in Firecat\0Alley", "invalid-objective-text")]
    [InlineData("Go To Theatre in Firecat\u2028Alley", "invalid-objective-text")]
    [InlineData("<center>Go To Theatre in Firecat Alley</center>", "unsupported-verb")]
    public void UnknownOrAmbiguousTextRetainsEvidenceWithoutAnExecutionKey(string? raw, string reason)
    {
        var result = W101QuestIntent.Parse(raw);
        Assert.False(result.IsRecognized); Assert.Equal(W101QuestIntentAction.Unknown, result.Action);
        Assert.Null(result.Target); Assert.Null(result.Area); Assert.Null(result.ExecutionKey);
        Assert.Empty(result.ExecutionNeeds); Assert.Null(result.Enemy); Assert.Null(result.Item);
        Assert.False(result.ArrivalOnly); Assert.Equal(reason, result.UnsupportedReason); Assert.Equal(raw, result.RawObjective);
    }

    [Theory]
    [InlineData("Defeat 3 Lost Souls in Unicorn Way")]
    [InlineData("Defeat Lost Souls (0 of 3) in Unicorn Way")]
    [InlineData("Defeat Lost Souls in Unicorn Way 0/3")]
    [InlineData("Collect 0/3 Crates in Triton Avenue")]
    [InlineData("Collect three Crates in Triton Avenue")]
    [InlineData("Defeat all Lost Souls in Unicorn Way")]
    public void CountsAndQuantifiersStayUnsupportedInsteadOfBecomingGuessedNamesOrQuotas(string objective)
    {
        var result = W101QuestIntent.Parse(objective);
        Assert.False(result.IsRecognized); Assert.Equal("unsupported-count", result.UnsupportedReason);
        Assert.Equal(objective, result.RawObjective); Assert.Null(result.ExecutionKey);
    }

    [Fact]
    public void ObjectiveBudgetIncludesRawWhitespaceAndDoesNotRetainOversizedInput()
    {
        const string prefix = "Go To ", suffix = " in Area";
        var exact = prefix + new string('a', W101QuestIntent.MaximumObjectiveLength - prefix.Length - suffix.Length) + suffix;
        Assert.True(W101QuestIntent.Parse(exact).IsRecognized);
        var oversized = W101QuestIntent.Parse(exact + " ");
        Assert.False(oversized.IsRecognized); Assert.Equal("objective-too-long", oversized.UnsupportedReason);
        Assert.Null(oversized.RawObjective); Assert.Null(oversized.ExecutionKey);
    }

    [Fact]
    public void CapturedQuinnOfferObjectiveBecomesArrivalOnlyDestinationData()
    {
        using var saved = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "TestData", "QuestConversation", "quinn-offer.json")));
        var final = saved.RootElement.GetProperty("events").EnumerateArray().Last(entry =>
            entry.GetProperty("kind").GetString() == "observation");
        var raw = final.GetProperty("context").GetProperty("objectiveText").GetString();
        var result = W101QuestIntent.Parse(raw);
        Assert.Equal("Go To Fireglobe Theatre in Firecat Alley", raw);
        Assert.Equal(W101QuestIntentAction.Destination, result.Action); Assert.True(result.ArrivalOnly);
        Assert.Equal("Fireglobe Theatre", result.Target); Assert.Equal("Firecat Alley", result.Area);
        Assert.Equal("navigate", result.ExecutionKey);
    }

    [Theory]
    [InlineData("Defeat Any Fire Elf and Collect Arrows in Firecat Alley. (0 of 3)", "Any Fire Elf", "Arrows", "Firecat Alley", 0, 3)]
    [InlineData("  defeat\tAny Fire Elf AND COLLECT Arrows IN Firecat Alley (0 OF 3). ", "Any Fire Elf", "Arrows", "Firecat Alley", 0, 3)]
    [InlineData("Defeat Clockwork Golems and Collect Brass Gears in Test District. (2 of 4)", "Clockwork Golems", "Brass Gears", "Test District", 2, 4)]
    [InlineData("Defeat Ice Creatures and Collect Frost Shards in North-West Ward", "Ice Creatures", "Frost Shards", "North-West Ward", null, null)]
    public void GenericDefeatAndCollectKeepsEnemyItemAreaAndExactProgress(string raw, string enemy,
        string item, string area, int? current, int? required)
    {
        var result = W101QuestIntent.Parse(raw);
        Assert.True(result.IsRecognized); Assert.Equal(W101QuestIntentAction.DefeatAndCollect, result.Action);
        Assert.Equal(enemy, result.Enemy); Assert.Equal(item, result.Item); Assert.Equal(area, result.Area);
        Assert.Equal(current, result.CurrentCount); Assert.Equal(required, result.RequiredCount);
        Assert.Equal("combat-collect", result.ExecutionKey);
        Assert.Equal(new[] { "navigate", "combat", "collect" }, result.ExecutionNeeds);
        Assert.False(result.ArrivalOnly); Assert.Equal(raw, result.RawObjective);
    }

    [Theory]
    [InlineData("Defeat Any Lost Soul in Unicorn Way. (1 of 2)", "Any Lost Soul", null)]
    [InlineData("Collect Crates in Triton Avenue (1 of 2)", null, "Crates")]
    public void ExactProgressSuffixAlsoAppliesToSingleDefeatAndCollectFamilies(string raw, string? enemy, string? item)
    {
        var result = W101QuestIntent.Parse(raw);
        Assert.True(result.IsRecognized); Assert.Equal(enemy, result.Enemy); Assert.Equal(item, result.Item);
        Assert.Equal(1, result.CurrentCount); Assert.Equal(2, result.RequiredCount);
    }

    [Theory]
    [InlineData("(3 of 2)")]
    [InlineData("(-1 of 3)")]
    [InlineData("(0 of 0)")]
    [InlineData("(0 of 10001)")]
    [InlineData("(0/3)")]
    [InlineData("(zero of three)")]
    [InlineData("(0 of 3) (1 of 3)")]
    [InlineData("(0 of 3) extra")]
    public void AmbiguousOrInvalidProgressDoesNotProduceACombatPlan(string suffix)
    {
        var raw = "Defeat Any Enemy and Collect Items in Test Area. " + suffix;
        var result = W101QuestIntent.Parse(raw);
        Assert.False(result.IsRecognized); Assert.Null(result.ExecutionKey); Assert.Empty(result.ExecutionNeeds);
        Assert.Null(result.CurrentCount); Assert.Null(result.RequiredCount); Assert.Equal(raw, result.RawObjective);
    }

    [Theory]
    [InlineData("Defeat and Collect Arrows in Firecat Alley")]
    [InlineData("Defeat Fire Elves and Collect in Firecat Alley")]
    [InlineData("Defeat Fire Elves and Collect Arrows and Collect Coins in Firecat Alley")]
    [InlineData("Defeat Fire Elves or Collect Arrows in Firecat Alley")]
    [InlineData("Go To Theatre in Firecat Alley (0 of 3)")]
    public void IncompleteOrDifferentCompoundGrammarStaysUnknown(string raw)
    {
        var result = W101QuestIntent.Parse(raw);
        Assert.False(result.IsRecognized); Assert.Null(result.ExecutionKey); Assert.Equal(raw, result.RawObjective);
    }
}
