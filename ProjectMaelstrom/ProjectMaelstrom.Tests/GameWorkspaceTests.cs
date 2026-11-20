using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class GameWorkspaceTests
{
    [Fact]
    public void ReusedWindowOrProcessCannotMatchOriginalIdentity()
    {
        var original = new GameWindow(100, 42, 1234, "Wizard", "Game");
        Assert.True(GameWindows.SameIdentity(original, original with { Title = "New title" }));
        Assert.False(GameWindows.SameIdentity(original, original with { Handle = 101 }));
        Assert.False(GameWindows.SameIdentity(original, original with { ProcessId = 43 }));
        Assert.False(GameWindows.SameIdentity(original, original with { StartedUtcTicks = 1235 }));
        Assert.False(GameWindows.SameIdentity(original, null));
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, -1)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void InvalidCaptureGeometryIsRejected(int width, int height) => Assert.Throws<InvalidOperationException>(() => WindowObserver.CheckSize(width, height));

    [Fact]
    public void WorkspaceRoundTripDoesNotPersistLiveConnection()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-workspace-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Null(GameWorkspaceStore.Load(root));
            var profile = new GameWorkspaceProfile("Wizard101 home", "WizardGraphicalClient");
            GameWorkspaceStore.Save(root, profile);
            Assert.Equal(profile, GameWorkspaceStore.Load(root));
            var path = Path.Combine(root, "workspaces", "last-workspace.json");
            var json = File.ReadAllText(path);
            Assert.DoesNotContain("Handle", json);
            Assert.DoesNotContain("ProcessId", json);
            Assert.Throws<InvalidDataException>(() => GameWorkspaceStore.Save(root, profile with { Name = " " }));
            Assert.Equal(profile, GameWorkspaceStore.Load(root));
            File.WriteAllText(path, "{\"Name\":\"Game\",\"ProcessName\":null}");
            Assert.Throws<InvalidDataException>(() => GameWorkspaceStore.Load(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
