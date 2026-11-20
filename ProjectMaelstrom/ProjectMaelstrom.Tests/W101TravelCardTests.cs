using System.Drawing;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101TravelCardTests
{
    [Theory]
    [InlineData("Zafaria", 60, 8, true)]
    [InlineData("Wizard City", 0, 1, true)]
    [InlineData("", 60, 8, false)]
    [InlineData("Zafaria; quit", 60, 8, false)]
    [InlineData("Zafaria", 121, 8, false)]
    [InlineData("Zafaria", 60, 9, false)]
    public void CardBounds(string world, int seconds, int pages, bool valid)
    {
        var options = new W101TravelOptions(world, true, seconds, pages);
        if (valid) options.Validate(); else Assert.Throws<InvalidDataException>(options.Validate);
    }
    [Theory]
    [InlineData(true, 0, "ready")]
    [InlineData(false, 0, "wait")]
    [InlineData(false, 60, "expired")]
    [InlineData(null, 0, "unknown")]
    public void HomeDoesNotTreatUnknownOrCooldownAsPermission(bool? enabled, int elapsed, string decision) =>
        Assert.Equal(decision, W101TravelCard.HomeDecision(enabled, elapsed, 60));

    private static W101TravelCard.Node[] Rows(float markerY = 195) =>
    [new("wbtnZafaria", 1, new(10, 100, 100, 30)), new("wbtnAvalon", 1, new(10, 180, 100, 30)),
     new("selectedWorldCheckMark", 1, new(-20, markerY, 20, 20))];
    [Fact]
    public void CooldownCaptureIsOptionalAndRequiresHomeReturn()
    {
        Assert.False(new W101TravelOptions("Zafaria").CaptureHomeCooldown);
        new W101TravelOptions("Zafaria", CaptureHomeCooldown: true).Validate();
        Assert.Throws<InvalidDataException>(() => new W101TravelOptions("Zafaria", ReturnHome: false, CaptureHomeCooldown: true).Validate());
        var card = JobDeckRoutines.NewCard(JobDeckRoutines.Create().Single(r => r.Info.Id == W101TravelCard.Id).Info);
        card.Settings!["captureHomeCooldown"] = "true";
        Assert.True(W101TravelCard.Options(card).CaptureHomeCooldown);
        card.Settings!["captureHomeCooldown"] = "invalid";
        Assert.Throws<InvalidDataException>(() => W101TravelCard.Options(card));
    }
    [Fact]
    public void SelectedMarkerMustIdentifyUniqueWorldRow()
    {
        Assert.True(W101TravelCard.Selected(Rows(), "Avalon"));
        Assert.False(W101TravelCard.Selected(Rows(), "Zafaria"));
        Assert.False(W101TravelCard.Selected(Rows(145), "Avalon"));
        Assert.False(W101TravelCard.Selected(Rows().Where(n => n.Name != "selectedWorldCheckMark"), "Avalon"));
        Assert.False(W101TravelCard.Selected([.. Rows(), Rows()[1]], "Avalon"));
    }
    [Fact]
    public void PageMappingUsesFrameLabelsAndRejectsMissingGeometry()
    {
        var rows = Rows().Append(new W101TravelCard.Node("leftButton", 1, new(0, 260, 20, 20))).ToArray();
        var text = new PetDanceMenuText(new(1000, 800), [new("ZAFARIA", new(650, 230, 100, 30)), new("AVALON", new(650, 390, 100, 30))]);
        var mapped = W101TravelCard.PageButton(rows, text, "leftButton");
        Assert.NotNull(mapped); Assert.Equal(40, mapped.Value.Width);
        Assert.Null(W101TravelCard.PageButton(rows, text with { Words = [] }, "leftButton"));
        Assert.Null(W101TravelCard.PageButton(rows, text, "rightButton"));
    }
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void HomeTimerOverridesEnabledButton(bool enabled, bool timerVisible, bool expected)
    {
        var path = Path.GetTempFileName();
        try
        {
            var tree = new
            {
                kind = "tree",
                nodes = new object[] {
                new { index=1, parent=(int?)null, name="compassAndTeleporterButtons", visible=true, enabled=true },
                new { index=2, parent=(int?)1, name="GoHomeButton", visible=true, enabled },
                new { index=3, parent=(int?)2, name="txtGoHomeTimer", visible=timerVisible, enabled=true } }
            };
            var line = JsonSerializer.Serialize(tree);
            File.WriteAllText(path, line + "\n" + line + "\n");
            Assert.Equal(expected, W101TravelCard.HomeAvailable(path));
            File.WriteAllText(path, line + "\n"); Assert.Null(W101TravelCard.HomeAvailable(path));
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void ReturnMustMatchFiniteHouseOrigin()
    {
        Assert.True(W101TravelCard.NearOrigin(new(1, 2, 3), new(1, 2, 3)));
        Assert.False(W101TravelCard.NearOrigin(new(1, 2, 3), new(1, 2000, 3)));
        Assert.False(W101TravelCard.NearOrigin(new(1, 2, 3), new(double.NaN, 2, 3)));
    }
    [Fact]
    public void CardUsesExistingRegistryAndNeedsAnAdapter()
    {
        var unbound = JobDeckRoutines.Create().Single(r => r.Info.Id == W101TravelCard.Id);
        Assert.False(unbound.Info.Available);
        var card = JobDeckRoutines.NewCard(unbound.Info);
        Assert.True(W101TravelCard.Options(card).ReturnHome);
        Assert.Throws<InvalidOperationException>(() => unbound.Prepare(card, null));
        Assert.Throws<InvalidDataException>(() => W101TravelCard.Options(card with { RoutineId = "w101.questing" }));
    }
}
