using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class GameWorkspaceCatalogTests
{
    [Theory]
    [InlineData("Godot_v4.7.2-stable_win64", "Dodge the Creeps", true)]
    [InlineData("Godot_v4.7.2-stable_win64", "Dodge the Creeps (DEBUG)", true)]
    [InlineData("dodge_the_creeps", "Dodge the Creeps", true)]
    [InlineData("WizardGraphicalClient", "Wizard101", false)]
    [InlineData("Godot_v4.7.2-stable_win64", "Another Game", false)]
    [InlineData("Godot_v4.7.2-stable_win64", "Dodge the Creeps - Godot Engine", false)]
    [InlineData("msedge", "Dodge the Creeps", false)]
    [InlineData("notepad", "Dodge the Creeps", false)]
    public void DodgeSelectionDoesNotFallBackToWizardOrAnEditor(string process, string title, bool expected)
    {
        Assert.Equal(expected, GameWorkspaceCatalog.IsDodgeWindow(new(1, 10, 20, process, title)));
    }

    [Theory]
    [InlineData("{\"game_id\":\"godot-dodge-the-creeps\"}", true)]
    [InlineData("{\"game_id\":\"wizard101\"}", false)]
    [InlineData("{\"game_id\":\"godot-dodge-the-creeps\",\"game_id\":\"wizard101\"}", false)]
    [InlineData("{\"game_id\":123}", false)]
    [InlineData("{}", false)]
    [InlineData("[]", false)]
    public void PackRoutingRequiresExactlyOneDodgeGameIdentifier(string json, bool accepted)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            if (accepted) Assert.Equal(Path.GetFullPath(path), GameWorkspaceCatalog.RequireDodgePack(path));
            else Assert.Throws<InvalidDataException>(() => GameWorkspaceCatalog.RequireDodgePack(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void PackRoutingBoundsFileSizeWithoutLaunchingAnything()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var stream = File.OpenWrite(path)) stream.SetLength(1024 * 1024 + 1);
            Assert.Throws<InvalidDataException>(() => GameWorkspaceCatalog.RequireDodgePack(path));
        }
        finally { File.Delete(path); }
    }
}
