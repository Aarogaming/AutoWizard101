using ProjectMaelstrom.Models;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class ActivityProfileTests
{
    [Fact]
    public async Task CancelledLaunchDoesNotStartAHiddenTool()
    {
        using var service = new ToolIntegrationService();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.LaunchAsync("companion", new ToolReadiness { SourcePresent = true }, cancellationToken: cancellation.Token));
        Assert.False(service.HasOpenTools);
    }
    [Theory]
    [InlineData("questing", "deimos")]
    [InlineData("farming", "deimos")]
    [InlineData("pet_dance", "dance")]
    public void ActivityMapsToItsImplementedBackend(string activity, string tool)
    {
        var profile = new ActivityProfile { Activity = activity };
        profile.Validate();
        Assert.Equal(tool, profile.Tool);
        Assert.Contains("Time limit", profile.Summary());
    }

    [Fact]
    public void UnsupportedMinigameAndSnackCoordinatesAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new ActivityProfile { Activity = "potion" }.Validate());
        var dance = new ActivityProfile { Activity = "pet_dance", Resolution = "800x600", Snacks = new[] { true, false, false, false, false } };
        Assert.Throws<InvalidOperationException>(dance.Validate);
        dance.Snacks = new bool[5];
        dance.Validate();
        dance.Locations = new bool[5];
        Assert.Throws<InvalidOperationException>(dance.Validate);
    }

    [Fact]
    public void SavedSetupPreservesChoicesWithoutSavingPreparationConfirmations()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var profile = new ActivityProfile { Activity = "farming", MaxMinutes = 12, CombatStyle = "aoe", BuyPotions = false, TeamUp = true };
            profile.Save(path);
            var loaded = ActivityProfile.Load(path);
            Assert.Equal(12, loaded.MaxMinutes);
            Assert.Equal("aoe", loaded.CombatStyle);
            Assert.True(loaded.TeamUp);
            Assert.False(loaded.BuyPotions);
            Assert.DoesNotContain("signedIn", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }
}
