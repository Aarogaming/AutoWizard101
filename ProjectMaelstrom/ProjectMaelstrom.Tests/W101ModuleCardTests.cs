using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101ModuleCardTests
{
    [Theory]
    [InlineData("w101.interact")]
    [InlineData("w101.menu")]
    [InlineData("w101.auto-idle")]
    public void DefaultHostCannotEnableLiveBindings(string id)
    {
        var routine = JobDeckRoutines.Create().Single(r => r.Info.Id == id);
        Assert.False(routine.Info.Available); Assert.True(routine.Info.WritesGame);
        var card = Card(routine);
        Assert.NotNull(routine.Eligibility!(card, null));
        Assert.Throws<InvalidOperationException>(() => routine.Prepare(card, null));
    }

    [Theory]
    [InlineData("w101.interact", "sigil")]
    [InlineData("w101.menu", "menu")]
    [InlineData("w101.auto-idle", "native-idle")]
    public void TrustedFactoryUsesExplicitKindAndReadinessWithoutImplicitInput(string id, string kind)
    {
        var calls = 0;
        string? blocker = "Current geometry is unqualified";
        var adapter = new W101ModuleCardAdapter((selected, card, facts) =>
        {
            Assert.Equal(kind, selected); calls++;
            return new(new(Guid.NewGuid().ToString("N"), "test.w101-module", "1", 5, false, "synthetic", new Dictionary<string, string>()),
                (_, _) => throw new InvalidOperationException("Preparation must not execute"));
        }, (_, _, _) => blocker);
        var routine = JobDeckRoutines.Create(w101Modules: adapter).Single(r => r.Info.Id == id);
        var card = Card(routine);
        Assert.True(routine.Info.Available);
        Assert.Equal(blocker, routine.Eligibility!(card, null));
        Assert.Throws<InvalidOperationException>(() => routine.Prepare(card, null));
        Assert.Equal(0, calls);
        blocker = null;
        routine.Prepare(card, null);
        Assert.Equal(1, calls); Assert.Null(GameInputLease.Current);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("61")]
    [InlineData("unbounded")]
    public void InvalidTimeoutRejected(string timeout)
    {
        var routine = JobDeckRoutines.Create().Single(r => r.Info.Id == "w101.menu");
        var card = Card(routine); card.Settings!["timeoutSeconds"] = timeout;
        Assert.Throws<InvalidDataException>(() => routine.Validate(card));
    }

    [Fact]
    public void ExistingIdleSettingsSurviveButMovementIsNotPromoted()
    {
        var adapter = new W101ModuleCardAdapter((_, _, _) => throw new Exception("Must not prepare"), (_, _, _) => null);
        var routine = JobDeckRoutines.Create(w101Modules: adapter).Single(r => r.Info.Id == "w101.auto-idle");
        var card = JobDeckRoutines.NewCard(routine.Info) with { InDeck = true, Settings = new() { ["method"] = "spin" } };
        routine.Validate(card);
        Assert.Contains("preparation-only", routine.Eligibility!(card, null));
        Assert.Throws<InvalidOperationException>(() => routine.Prepare(card, null));
    }

    [Fact]
    public void PromptAndMenuActionCannotBeArbitrary()
    {
        var prompt = JobDeckRoutines.Create().Single(r => r.Info.Id == "w101.interact");
        Assert.Throws<InvalidDataException>(() => prompt.Validate(JobDeckRoutines.NewCard(prompt.Info)));
        var menu = JobDeckRoutines.Create().Single(r => r.Info.Id == "w101.menu");
        var card = Card(menu); card.Settings!["action"] = "shop";
        Assert.Throws<InvalidDataException>(() => menu.Validate(card));
    }

    private static JobCardSpec Card(JobRoutine routine)
    {
        var card = JobDeckRoutines.NewCard(routine.Info) with { InDeck = true };
        if (routine.Info.Id == "w101.interact") card.Settings!["prompt"] = "Pet Dance";
        return card;
    }
}
