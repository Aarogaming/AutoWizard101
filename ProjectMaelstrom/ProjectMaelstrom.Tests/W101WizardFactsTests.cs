using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101WizardFactsTests
{
    [Fact]
    public void GoldAndLevelRequestsUseExistingOptionalResourceScan()
    {
        var card = new JobCardSpec("fixture", "fixture", "fixture", true, DataRequests: ["Gold.Current", "Wizard.Level"]);
        var state = new JobDeckState(new("fixture", "wizard101", [card]), 0, "Waiting", new());
        Assert.False(CardScanDemand.Build(state).Resources);
        var enabled = CardScanDemand.Build(state, resourcesEnabled: true);
        Assert.True(enabled.Resources);
        Assert.Equal(new[] { "gold.current", "wizard.level" }, enabled.Fields);
        Assert.Empty(enabled.Unavailable);
    }
    [Fact]
    public void OwnedStatsUseOpaqueCharacterIdentityWithoutReusingVisualProfileValues()
    {
        var reading = Reading() with
        {
            Resources = new("observed", Stamp, new(), new() { ["health"] = 1022, ["mana"] = 76 }),
            ResourceCandidate = JsonSerializer.SerializeToElement(new
            {
                status = "candidate",
                layout = "d410-property-registration-candidate",
                observedUtcTicks = Stamp,
                wizardIdentity = new { status = "observed", source = "client-object-registry-v1", characterKey = new string('c', 64), statsMatched = true, readCalls = 24, readBytes = 384 },
                namedFields = new[] {
                    new { name = "m_currentGold", quality = "candidate", value = 1136L, offset = 116, dataType = "int32" },
                    new { name = "m_referenceLevel", quality = "candidate", value = 10L, offset = 324, dataType = "int32" },
                    new { name = "m_schoolID", quality = "candidate", value = 78318724L, offset = 328, dataType = "uint32" }
                }
            })
        };
        var values = new Dictionary<string, JsonElement>();
        var subject = W101WizardFacts.AddObserved(values, reading, Stamp);
        Assert.NotNull(subject);
        Assert.Equal("Death", subject.School);
        Assert.Equal(Target.StartedUtcTicks, subject.Generation);
        Assert.Equal(1136, values["gold.current"].GetInt32());
        Assert.Null(W101WizardFacts.AddObserved(values, reading with { Loading = true }, Stamp));
        Assert.Empty(values);
        Assert.Null(W101WizardFacts.AddObserved(values, reading with { ResourceCandidate = Reading().ResourceCandidate }, Stamp));
        Assert.Empty(values);
    }
    private static readonly long Stamp = DateTime.UtcNow.Ticks;
    private static readonly WizwalkerTarget Target = new(1, 2, 3, "WizardGraphicalClient", "fixture.exe",
        "d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a");
    private static W101WizardSubject Subject => new(new string('a', 32), "Luke SkyWalker", "Death", 1, Stamp);
    private static W101WizardReference Reference => new(Target, Subject, Stamp, 1136, 10, new string('b', 64));
    private static W101UiTreeResult Reading(int level = 10) => new(1, "fixture", "fixture", "fixture", Target,
        "observed", "fixture", false, [], Stamp, Stamp, 1000, true, true, 2, 2, 0, [],
        ResourceCandidate: JsonSerializer.SerializeToElement(new
        {
            status = "candidate",
            layout = "d410-property-registration-candidate",
            observedUtcTicks = Stamp,
            namedFields = new[] {
                new { name = "m_currentGold", quality = "candidate", value = 1136, offset = 116, dataType = "int32" },
                new { name = "m_referenceLevel", quality = "candidate", value = level, offset = 324, dataType = "int32" }
            }
        }));

    [Fact]
    public void SameWizardCorroborationReachesTypedAliasesAndCardConditions()
    {
        var values = new Dictionary<string, JsonElement>();
        Assert.True(W101WizardFacts.Add(values, Reading(), Subject, Reference, Stamp));
        Assert.Equal(1136, values["gold.current"].GetInt32());
        Assert.Equal("DINT", JobAliasRegister.Type(JobFactCatalog.Find("Gold.Current")!));
        var facts = new JobFacts("wizard101", "target", new(Stamp, DateTimeKind.Utc), values, "fixture", Subject);
        var card = new JobCardSpec("fixture", "fixture", "fixture", Conditions: [new("Gold.Current", "gte", JsonSerializer.SerializeToElement(1000))]);
        Assert.Null(JobDeckRules.FactBlock(card, "wizard101", facts, new(Stamp, DateTimeKind.Utc), true));
        Assert.NotNull(JobDeckRules.FactBlock(card, "wizard101", facts with { WizardSubject = null }, new(Stamp, DateTimeKind.Utc), true));
        Assert.True(JobFactCatalog.Find("gold.current")!.ProducerAvailable);
    }

    [Fact]
    public void CharacterGenerationAndSchoolPreventSameProcessCrossWizardReuse()
    {
        var values = new Dictionary<string, JsonElement> { ["gold.current"] = JsonSerializer.SerializeToElement(999) };
        Assert.False(W101WizardFacts.Add(values, Reading(), Subject with { School = "Fire" }, Reference, Stamp));
        Assert.Empty(values);
        Assert.False(W101WizardFacts.Add(values, Reading(), Subject with { Generation = 2 }, Reference, Stamp));
        Assert.False(W101WizardFacts.Add(values, Reading(), null, Reference, Stamp));
        Assert.False(W101WizardFacts.Add(values, Reading() with { Target = Target with { StartedUtcTicks = 4 } }, Subject, Reference, Stamp));
    }

    [Fact]
    public void LevelUpDoesNotChangeIdentityButNeedsFreshMatchingLevelEvidence()
    {
        var values = new Dictionary<string, JsonElement>();
        Assert.False(W101WizardFacts.Add(values, Reading(11), Subject, Reference, Stamp));
        Assert.True(W101WizardFacts.Add(values, Reading(11), Subject, Reference with { Level = 11 }, Stamp));
        Assert.Equal(11, values["wizard.level"].GetInt32());
    }

    [Fact]
    public void LoadingUncleanStaleFutureAndUnknownCandidatesCannotPublish()
    {
        var values = new Dictionary<string, JsonElement>();
        foreach (var reading in new[] { Reading() with { Loading = true }, Reading() with { CleanupConfirmed = false },
            Reading() with { ResourceCandidate = null }, Reading() with { StatesObservedUtcTicks = Stamp + 1 } })
            Assert.False(W101WizardFacts.Add(values, reading, Subject, Reference, Stamp));
        Assert.False(W101WizardFacts.Add(values, Reading(), Subject, Reference, Stamp + TimeSpan.FromSeconds(3).Ticks));
        Assert.Empty(values);
    }
}
