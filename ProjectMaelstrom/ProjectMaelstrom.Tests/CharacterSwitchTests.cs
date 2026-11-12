using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class CharacterSwitchTests
{
    [Theory]
    [InlineData("character-inspect")]
    [InlineData("character-roster")]
    [InlineData("character-enter")]
    [InlineData("character-verify")]
    [InlineData("character-home")]
    public void CharacterCliRequiresOwnerAndExplicitActionOptIn(string command)
    {
        var root = Path.GetTempPath();
        string[] args = ["--cli", command, "--output", Path.Combine(root, "new.json"), "--workspace", root,
            "--window", "42", "--process-id", "123", "--started-utc-ticks", "100", "--enable-client-observation", "--enable-ui-tree"];
        if (command != "character-verify") args = [.. args, "--selection", Path.Combine(root, "wizard.json")];
        Assert.Throws<ArgumentException>(() => MaelstromCli.Parse(args, root));
        Assert.Equal(command, MaelstromCli.Parse([.. args, "--enable-module-actions"], root).Command);
    }

    [Fact]
    public void WizardProfileRoundTripsWithoutInventingAHouse()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var profile = new WizardProfile(1, Guid.NewGuid().ToString("N"), "Luke SkyWalker", "Fire", 106);
        try
        {
            WizardProfileStore.Save(directory, profile);
            var loaded = WizardProfileStore.Load(Path.Combine(directory, profile.Id + ".json"));
            Assert.Equal(profile, loaded);
            Assert.Null(loaded.ActiveHouseId);
            var house = new WizardHouseProfile("home", new(profile.Name, profile.School, 106), "home");
            WizardProfileStore.Save(directory, profile with { ActiveHouseId = "home", Houses = [house] });
            var refreshed = WizardProfileStore.RefreshLevel(directory, profile with { LastKnownLevel = 107 });
            Assert.Equal(107, refreshed.LastKnownLevel);
            Assert.Equal("home", refreshed.ActiveHouseId);
            Assert.Single(refreshed.Houses!);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void StatsRequireNameAndCurrentLevelInHeader()
    {
        var profile = new WizardProfile(1, Guid.NewGuid().ToString("N"), "Luke SkyWalker", "Fire", 106);
        PetDanceMenuWord[] words = [new("LUKE", new(500, 100, 40, 20)), new("SKYWALKER", new(550, 100, 100, 20)), new("(LEVEL 106)", new(400, 180, 100, 20))];
        Assert.True(W101CharacterOperation.StatsMatch(new(new(1282, 759), words), profile));
        Assert.False(W101CharacterOperation.StatsMatch(new(new(1282, 759), words), profile with { LastKnownLevel = 10 }));
        Assert.False(W101CharacterOperation.StatsMatch(new(new(1282, 759), words[1..]), profile));
    }
    [Fact]
    public void SameNameDeathWizardDoesNotMatchFireTarget()
    {
        var profile = new WizardProfile(1, Guid.NewGuid().ToString("N"), "Luke SkyWalker", "Fire", 105);
        Assert.False(W101CharacterOperation.Matches(new("Luke SkyWalker", "Level 10 (Initiate Necromancer)", "Dorm Room"), profile));
        Assert.True(W101CharacterOperation.Matches(new("Luke SkyWalker", "Level 105 (Exalted Pyromancer)", "House"), profile));
        Assert.True(W101CharacterOperation.Matches(new("Luke SkyWalker", "Level 106 (Exalted Pyromancer)", "House"), profile));
        Assert.Equal(106, W101CharacterOperation.RefreshObservedLevel(new("Luke SkyWalker", "Level 106 (Exalted Pyromancer)", "House"), profile).LastKnownLevel);
        Assert.False(W101CharacterOperation.Matches(new("Other Wizard", "Level 105 (Exalted Pyromancer)", "House"), profile));
    }
    [Fact]
    public void QuitRequiresOptionsContextAndOneControl()
    {
        PetDanceMenuWord[] words = [new("QUIT", new(700, 600, 45, 20)), new("IDLE", new(600, 600, 40, 20)), new("GRAPHICS", new(500, 100, 80, 20))];
        Assert.NotNull(W101CharacterOperation.QuitControl(new(new(1280, 720), words)));
        Assert.Null(W101CharacterOperation.QuitControl(new(new(1280, 720), words[..1])));
        Assert.Null(W101CharacterOperation.QuitControl(new(new(1280, 720), [.. words, new("QUIT", new(900, 600, 40, 20))])));
    }

    [Fact]
    public void ProfilesCannotBorrowAnotherWizardsHouse()
    {
        var profile = new WizardProfile(1, Guid.NewGuid().ToString("N"), "Luke SkyWalker", "Fire", 105);
        WizardProfileStore.Validate(profile);
        Assert.Throws<InvalidDataException>(() => WizardProfileStore.Validate(profile with { ActiveHouseId = "unknown" }));
        Assert.Throws<InvalidDataException>(() => WizardProfileStore.Validate(profile with { Houses = [new("h", Wizard, "dorm")] }));
    }

    private static readonly WizardIdentity Wizard = new("Test Wizard", "Fire", 50);
    private static CharacterSwitch Create() => new(new("house-profile", Wizard, "house-instance"));

    [Fact]
    public void RequiresLoadingTwoIdentityReadingsAndIdle()
    {
        var run = Create();
        Assert.Equal("quit-to-selection", run.Observe(new(1, true, "menu")));
        Assert.Equal("select-configured-wizard", run.Observe(new(2, true, "selection")));
        Assert.True(run.PreviousContextInvalidated);
        Assert.Null(run.ActiveProfile);
        Assert.Equal("play-selected-wizard", run.Observe(new(3, true, "selection", Wizard)));
        Assert.Equal("wait", run.Observe(new(4, true, "loading")));
        Assert.Equal("wait", run.Observe(new(5, true, "world", Wizard)));
        Assert.Null(run.ActiveProfile);
        Assert.Equal("return-to-idle", run.Observe(new(6, true, "world", Wizard)));
        Assert.Equal(Wizard, run.ActiveProfile!.Wizard);
        Assert.Equal("complete", run.Observe(new(7, true, "world", Wizard, true)));
    }

    [Fact]
    public void RepeatedOrStaleReadingsCannotVerifyIdentity()
    {
        var run = Create();
        run.Observe(new(1, true, "selection"));
        run.Observe(new(2, true, "loading"));
        run.Observe(new(3, true, "world", Wizard));
        run.Observe(new(3, true, "world", Wizard));
        run.Observe(new(4, false, "world", Wizard));
        Assert.Null(run.ActiveProfile);
    }

    [Theory]
    [InlineData("login")]
    [InlineData("disconnected")]
    [InlineData("takeover")]
    public void StopsOnUnavailableContext(string scene)
    {
        var run = Create();
        Assert.Equal("stopped", run.Observe(new(1, true, scene)));
        Assert.Null(run.ActiveProfile);
    }

    [Fact]
    public void WrongWizardNeverLoadsHouse()
    {
        var run = Create();
        run.Observe(new(1, true, "selection"));
        run.Observe(new(2, true, "loading"));
        Assert.Equal("stopped", run.Observe(new(3, true, "world", Wizard with { School = "Ice" })));
        Assert.Null(run.ActiveProfile);
    }

    [Fact]
    public void CardRemainsUnavailableUntilRuntimeQualified()
    {
        var card = Assert.Single(JobDeckRoutines.Create(), r => r.Info.Id == "w101.character-switch");
        Assert.False(card.Info.Available);
        Assert.Throws<InvalidOperationException>(() => card.Prepare(JobDeckRoutines.NewCard(card.Info), null));
    }
}
