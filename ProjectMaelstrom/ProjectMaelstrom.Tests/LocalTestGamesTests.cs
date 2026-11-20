using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class LocalTestGamesTests
{
    [Fact]
    public void LocalLaunchesUseExplicitPathsAndDoNotUseAShell()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom test games " + Guid.NewGuid().ToString("N"));
        try
        {
            var reflex = LocalTestGames.ReflexPath(root);
            var project = LocalTestGames.DodgePath(root);
            var godot = LocalTestGames.GodotPath(root);
            var edge = Path.Combine(root, "edge.exe");
            foreach (var path in new[] { reflex, project, godot, edge })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "test");
            }
            var reflexStart = LocalTestGames.ReflexStartInfo(root, edge);
            Assert.False(reflexStart.UseShellExecute);
            Assert.Equal(edge, reflexStart.FileName);
            Assert.Equal("--app=" + new Uri(reflex).AbsoluteUri, reflexStart.ArgumentList[0]);
            Assert.Contains("--user-data-dir=", reflexStart.ArgumentList[1]);
            var dodgeStart = LocalTestGames.DodgeStartInfo(root);
            Assert.False(dodgeStart.UseShellExecute);
            Assert.Equal(godot, dodgeStart.FileName);
            Assert.Equal(new[] { "--path", Path.GetDirectoryName(project)! }, dodgeStart.ArgumentList);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void MissingTestGamesAreReportedBeforeLaunching()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.Throws<FileNotFoundException>(() => LocalTestGames.ReflexStartInfo(missing, "missing-edge"));
        Assert.Throws<FileNotFoundException>(() => LocalTestGames.DodgeStartInfo(missing));
    }
}
