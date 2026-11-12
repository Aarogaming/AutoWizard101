using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class WorkshopHostTests
{
    [Fact]
    public void PortableRuntimeUsesPackageAndDataFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "maelstrom-host-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "runtime", "python"));
            Directory.CreateDirectory(Path.Combine(root, "tools"));
            File.WriteAllText(Path.Combine(root, "portable.mode"), "");
            File.WriteAllText(Path.Combine(root, "runtime", "python", "python.exe"), "");
            File.WriteAllText(Path.Combine(root, "tools", "game_pack_workshop.py"), "");
            var info = WorkshopHost.BuildStartInfo(root);
            Assert.Equal(Path.Combine(root, "Data"), info.WorkingDirectory);
            Assert.Equal(Path.Combine(root, "runtime", "python", "python.exe"), info.FileName);
            Assert.Contains(Path.Combine(root, "Data", "game-packs"), info.ArgumentList);
            Assert.False(info.UseShellExecute);
            Assert.True(info.CreateNoWindow);
            File.Delete(info.FileName);
            Assert.Throws<InvalidOperationException>(() => WorkshopHost.BuildStartInfo(root, root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void InstalledDataIsOutsideApplicationDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Maelstrom"), WorkshopHost.DataRoot(root));
    }
}
