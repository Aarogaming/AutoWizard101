using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class TestGameWorkspaceStoreTests
{
    [Fact]
    public void SelectedFolderSurvivesAppVersionChangesAndInvalidPathsAreRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-test-folder-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = Path.Combine(root, "workspace");
            var data = Path.Combine(root, "data");
            var game = LocalTestGames.DodgePath(workspace);
            var godot = LocalTestGames.GodotPath(workspace);
            Directory.CreateDirectory(Path.GetDirectoryName(game)!);
            Directory.CreateDirectory(Path.GetDirectoryName(godot)!);
            File.WriteAllText(game, "test");
            File.WriteAllText(godot, "test");
            TestGameWorkspaceStore.Save(data, workspace);
            Assert.Equal(workspace, TestGameWorkspaceStore.Load(data));
            Assert.Throws<InvalidOperationException>(() => TestGameWorkspaceStore.Save(data, Path.Combine(root, "missing")));
            Assert.Equal(workspace, TestGameWorkspaceStore.Load(data));
            File.Delete(game);
            Assert.Null(TestGameWorkspaceStore.Load(data));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
