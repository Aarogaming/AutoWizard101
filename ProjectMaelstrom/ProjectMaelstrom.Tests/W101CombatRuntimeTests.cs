using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101CombatRuntimeTests
{
    private static W101CombatUiContext Context() => new(1, "structural-unqualified", "raw-layout-unqualified", false, false, true, true,
        [new(0, null, "PlanningPhase", "planning-container", null, true, true),
         new(1, 0, "Hand", "hand-container", null, true, true),
         new(2, 1, "Card", "hand-descendant", [1,2,3,4], true, true),
         new(3, 0, "CombatantControl", "combatant-container", null, true, true)],
        new(1, "deimos-development-df2283f", "source-layout-only", false, null, null, null,
            [new(2, 123, 456, "FireCat", "Damage", 234, 75, 0, false, false, true, false, null,
                [new(1, 100, 0, 234, 1, 0, 0, false)])],
            [new(3, "987", "654", 1, false, 234, 50, 100, false,
                new() { ["generic"]=1, ["power"]=0, ["balance"]=0, ["death"]=0, ["fire"]=0, ["ice"]=0, ["life"]=0, ["myth"]=0, ["storm"]=0, ["shadow"]=0 }, true, null)], [], 3000, 90));
    private static W101UiTreeResult Reading(W101CombatUiContext context, long now) => new(1, "run", W101UiTreeOperation.Version, "source",
        new(1, 2, 3, "WizardGraphicalClient", "unused", "digest"), "observed", "bounded-ui-watch-finished", false,
        new(), now, now, 100, true, true, 1, 1, 0, new(), CombatContext: context);

    [Fact]
    public void SourceCandidatesCannotEnableInputOrAssertSelection()
    {
        var context = Context(); var now = DateTime.UtcNow.Ticks;
        Assert.True(W101CombatRuntime.ValidCandidates(context));
        var assessment = W101CombatRuntime.Assess(Reading(context, now), now);
        Assert.False(assessment.Ready); Assert.Equal(1, assessment.CardCandidates); Assert.Equal(1, assessment.ParticipantCandidates);
        Assert.Contains("game-input-response", assessment.Missing);
        Assert.Contains("selected-card-confirmation", assessment.Missing);
    }

    [Theory]
    [InlineData("qualification")]
    [InlineData("card-binding")]
    [InlineData("selection")]
    [InlineData("duplicates")]
    [InlineData("owner")]
    [InlineData("pips")]
    [InlineData("hidden-health")]
    [InlineData("field-budget")]
    public void ChangedOrInventedCandidateFactsAreRejected(string fault)
    {
        var context = Context(); var fields = context.FieldCandidates!;
        fields = fault switch
        {
            "qualification" => fields with { MetadataQualified = true },
            "card-binding" => fields with { Cards = [fields.Cards[0] with { ControlIndex = 3 }] },
            "selection" => fields with { Cards = [fields.Cards[0] with { Selected = "true" }] },
            "duplicates" => fields with { Cards = [fields.Cards[0], fields.Cards[0]] },
            "owner" => fields with { Participants = [fields.Participants[0] with { OwnerId = "000987" }] },
            "pips" => fields with { Participants = [fields.Participants[0] with { Pips = new() }] },
            "hidden-health" => fields with { Participants = [fields.Participants[0] with { HealthHidden = true }] },
            _ => fields with { BytesRead = 65537 }
        };
        Assert.False(W101CombatRuntime.ValidCandidates(context with { FieldCandidates = fields }));
    }

    [Fact]
    public void DuelCorrelationIsNotVictoryAndMustContainObservedOwners()
    {
        var context = Context() with
        {
            DuelCandidates = new(1, "deimos-development-df2283f", "source-layout-only", false,
            "matched", null, "12", "victory", 6, 3, ["987"], null, null, 1000, 20)
        };
        Assert.True(W101CombatRuntime.ValidCandidates(context));
        Assert.False(W101CombatRuntime.Assess(Reading(context, DateTime.UtcNow.Ticks), DateTime.UtcNow.Ticks).Ready);
        Assert.False(W101CombatRuntime.ValidCandidates(context with { DuelCandidates = context.DuelCandidates with { WinnerTeamId = 1 } }));
        Assert.False(W101CombatRuntime.ValidCandidates(context with { DuelCandidates = context.DuelCandidates with { ParticipantOwnerIds = ["another"] } }));
    }

    [Fact]
    public void StaleOrUncleanObservationHasNoUsableCandidates()
    {
        var now = DateTime.UtcNow.Ticks;
        var reading = Reading(Context(), now);
        foreach (var invalid in new[] { reading with { CleanupConfirmed = false }, reading with { HookRestored = false },
            reading with { StatesObservedUtcTicks = now - TimeSpan.FromSeconds(6).Ticks }, reading with { Loading = true } })
        {
            var result = W101CombatRuntime.Assess(invalid, now);
            Assert.False(result.Ready); Assert.Equal(0, result.CardCandidates);
        }
    }

    [Fact]
    public void CandidateContractRoundTripsWithoutTurningUnknownIntoZero()
    {
        var context = JsonSerializer.Deserialize<W101CombatUiContext>(JsonSerializer.Serialize(Context()))!;
        Assert.True(W101CombatRuntime.ValidCandidates(context));
        Assert.Null(context.FieldCandidates!.Round); Assert.Null(context.FieldCandidates.Phase);
    }
}
