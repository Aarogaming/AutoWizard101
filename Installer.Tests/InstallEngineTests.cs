using System.IO.Compression;
using System.Text.Json;
using Microsoft.Win32;
using Installer;

public class InstallEngineTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "MaelstromInstallerTest-" + Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(_sandbox, "application");
    private string Data => Path.Combine(_sandbox, "userdata");
    private string Helper => Path.Combine(_sandbox, "setup.exe");
    private string Audio => Path.Combine(Data, "video-recordings", "example", "game-audio.wav");
    private string AudioTiming => Path.Combine(Data, "video-recordings", "example", "audio-timing.json");
    private static readonly byte[] AudioSentinel = [82, 73, 70, 70, 0, 1, 255, 128, 0, 42];
    private const string AudioTimingSentinel = "{\"kind\":\"audio-timing-preservation-sentinel\",\"clock\":\"QPC100ns\"}";
    public InstallEngineTests()
    {
        Directory.CreateDirectory(Data);
        File.WriteAllText(Helper, "maintenance binary fixture");
        Directory.CreateDirectory(Path.Combine(Data, "game-packs"));
        Directory.CreateDirectory(Path.Combine(Data, "reports"));
        File.WriteAllText(Path.Combine(Data, "game-packs", "my-pack.json"), "user pack");
        File.WriteAllText(Path.Combine(Data, "reports", "my-report.json"), "user report");
        Directory.CreateDirectory(Path.GetDirectoryName(Audio)!);
        File.WriteAllBytes(Audio, AudioSentinel);
        File.WriteAllText(AudioTiming, AudioTimingSentinel);
    }

    private sealed class Registration : IInstallRegistration
    {
        public InstallRecord? Current;
        public bool FailNext;
        public void Apply(InstallRecord record)
        {
            if (FailNext) { FailNext = false; throw new IOException("registration failure"); }
            Current = record;
        }
        public void Remove() => Current = null;
    }

    private static MemoryStream Package(string content = "v1", string? extra = null)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in new[] { "Maelstrom.exe", "ProjectMaelstrom.dll", "runtime/python/python.exe", "tools/game_pack_workshop.py", "portable.mode" })
                using (var writer = new StreamWriter(archive.CreateEntry(name).Open())) writer.Write(content);
            if (extra != null) using (var writer = new StreamWriter(archive.CreateEntry(extra).Open())) writer.Write("extra");
        }
        stream.Position = 0;
        return stream;
    }

    private void AssertDataKept()
    {
        Assert.Equal("user pack", File.ReadAllText(Path.Combine(Data, "game-packs", "my-pack.json")));
        Assert.Equal("user report", File.ReadAllText(Path.Combine(Data, "reports", "my-report.json")));
        Assert.Equal(AudioSentinel, File.ReadAllBytes(Audio));
        Assert.Equal(AudioTimingSentinel, File.ReadAllText(AudioTiming));
    }

    [Fact]
    public void InstallUpdateUninstallReinstallPreserveDataAndUnknownFiles()
    {
        var shell = new Registration();
        var engine = new InstallEngine(Root, Data, shell);
        using var first = Package();
        var old = engine.Install(first, Helper, "1", true);
        AssertDataKept();
        Assert.False(File.Exists(Path.Combine(Root, old.CurrentDirectory, "portable.mode")));
        var unknown = Path.Combine(Root, old.CurrentDirectory, "my-notes.txt");
        File.WriteAllText(unknown, "keep me");
        using var second = Package("v2");
        var updated = engine.Install(second, Helper, "2", false);
        Assert.Equal("2", shell.Current!.Version);
        Assert.False(File.Exists(Path.Combine(Root, old.CurrentDirectory, "Maelstrom.exe")));
        Assert.Equal("v2", File.ReadAllText(Path.Combine(Root, updated.CurrentDirectory, "Maelstrom.exe")));
        AssertDataKept();
        Assert.Empty(engine.Uninstall());
        Assert.Null(shell.Current);
        Assert.False(engine.ReadInstalled()!.Installed);
        Assert.Equal("keep me", File.ReadAllText(unknown));
        AssertDataKept();
        using var third = Package("v3");
        Assert.True(engine.Install(third, Helper, "3", false).Installed);
        AssertDataKept();
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("runtime/../../outside.txt")]
    [InlineData("Data/game-packs/pack.json")]
    [InlineData("Data/video-recordings/example/game-audio.wav")]
    [InlineData("Data/video-recordings/example/audio-timing.json")]
    [InlineData("tools/con.txt")]
    [InlineData("MAELSTROM.EXE")]
    public void BadArchiveCannotReplaceExistingVersion(string extra)
    {
        var shell = new Registration();
        var engine = new InstallEngine(Root, Data, shell);
        using var first = Package();
        var old = engine.Install(first, Helper, "1", false);
        using var bad = Package("bad", extra);
        Assert.ThrowsAny<IOException>(() => engine.Install(bad, Helper, "2", false));
        Assert.Equal(old.CurrentDirectory, engine.ReadInstalled()!.CurrentDirectory);
        Assert.Equal("v1", File.ReadAllText(Path.Combine(Root, old.CurrentDirectory, "Maelstrom.exe")));
        AssertDataKept();
    }

    [Fact]
    public void RegistrationFailureRollsBackVersionAndShortcuts()
    {
        var shell = new Registration();
        var engine = new InstallEngine(Root, Data, shell);
        using var first = Package();
        var old = engine.Install(first, Helper, "1", false);
        shell.FailNext = true;
        using var second = Package("v2");
        Assert.Throws<IOException>(() => engine.Install(second, Helper, "2", true));
        Assert.Equal(old.CurrentDirectory, shell.Current!.CurrentDirectory);
        Assert.Equal(old.CurrentDirectory, engine.ReadInstalled()!.CurrentDirectory);
        Assert.Single(Directory.GetDirectories(Path.Combine(Root, "versions")));
        AssertDataKept();
    }

    [Fact]
    public void LockedApplicationFileLeavesUninstallRetryable()
    {
        var shell = new Registration();
        var engine = new InstallEngine(Root, Data, shell);
        using var package = Package();
        var installed = engine.Install(package, Helper, "1", false);
        using (var locked = new FileStream(Path.Combine(Root, installed.CurrentDirectory, "Maelstrom.exe"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Throws<IOException>(() => engine.Uninstall());
            Assert.NotNull(shell.Current);
            Assert.True(engine.ReadInstalled()!.Installed);
        }
        Assert.Empty(engine.Uninstall());
        AssertDataKept();
    }

    [Fact]
    public void ModifiedAppFileIsPreservedAndTamperedLedgerCannotEscape()
    {
        var engine = new InstallEngine(Root, Data, new Registration());
        using var package = Package();
        var record = engine.Install(package, Helper, "1", false);
        var modified = Path.Combine(Root, record.CurrentDirectory, "tools/game_pack_workshop.py");
        File.WriteAllText(modified, "user edited script");
        record.Files["../userdata/game-packs/my-pack.json"] = InstallEngine.Hash(Path.Combine(Data, "game-packs/my-pack.json"));
        File.WriteAllText(Path.Combine(Root, InstallEngine.ReceiptName), JsonSerializer.Serialize(record));
        Assert.Throws<IOException>(() => engine.Uninstall());
        record.Files.Remove("../userdata/game-packs/my-pack.json");
        File.WriteAllText(Path.Combine(Root, InstallEngine.ReceiptName), JsonSerializer.Serialize(record));
        Assert.Single(engine.Uninstall());
        Assert.Equal("user edited script", File.ReadAllText(modified));
        AssertDataKept();
    }

    [Fact]
    public void OverlappingDataAndUnknownInstallFolderAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new InstallEngine(Root, Path.Combine(Root, "Data"), new Registration()));
        Directory.CreateDirectory(Root);
        File.WriteAllText(Path.Combine(Root, "unrelated.txt"), "keep");
        using var package = Package();
        Assert.Throws<IOException>(() => new InstallEngine(Root, Data, new Registration()).Install(package, Helper, "1", false));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(Root, "unrelated.txt")));
    }

    [Fact]
    public void PortableImportDoesNotMergeOrOverwriteExistingVersions()
    {
        var source = Path.Combine(_sandbox, "portable", "Data");
        var version = Path.Combine(source, "game-packs", "my-game", "1.0");
        Directory.CreateDirectory(version);
        Directory.CreateDirectory(Path.Combine(source, "reports"));
        File.WriteAllText(Path.Combine(version, "pack.json"), "portable pack");
        File.WriteAllText(Path.Combine(source, "reports", "demo.json"), "portable report");
        var first = PortableDataImport.Copy(source, Data);
        Assert.Equal(1, first.PackVersions);
        Assert.Equal(1, first.Reports);
        File.WriteAllText(Path.Combine(version, "pack.json"), "changed source");
        var second = PortableDataImport.Copy(source, Data);
        Assert.Equal(2, second.Skipped);
        Assert.Equal("portable pack", File.ReadAllText(Path.Combine(Data, "game-packs", "my-game", "1.0", "pack.json")));
        Assert.Equal("changed source", File.ReadAllText(Path.Combine(version, "pack.json")));
        AssertDataKept();
    }

    [Fact]
    public void WindowsShortcutAndRegistryLifecycleUsesIsolatedLocations()
    {
        var key = @"Software\MaelstromInstallerTests\" + Guid.NewGuid().ToString("N");
        var start = Path.Combine(_sandbox, "Start Menu", "Maelstrom.lnk");
        var desktop = Path.Combine(_sandbox, "Desktop", "Maelstrom.lnk");
        try
        {
            var registration = new WindowsRegistration(Root, key, start, desktop);
            var engine = new InstallEngine(Root, Data, registration);
            using var first = Package();
            engine.Install(first, Helper, "1", true);
            Assert.True(File.Exists(start));
            Assert.True(File.Exists(desktop));
            using (var installed = Registry.CurrentUser.OpenSubKey(key)) Assert.Equal("1", installed!.GetValue("DisplayVersion"));
            using var second = Package("v2");
            engine.Install(second, Helper, "2", false);
            Assert.False(File.Exists(desktop));
            using (var installed = Registry.CurrentUser.OpenSubKey(key)) Assert.Equal("2", installed!.GetValue("DisplayVersion"));
            engine.Uninstall();
            Assert.False(File.Exists(start));
            Assert.Null(Registry.CurrentUser.OpenSubKey(key));
            AssertDataKept();
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(key, false); }
    }

    public void Dispose()
    {
        // Only the uniquely allocated test directory is removed, after path checking.
        var full = Path.GetFullPath(_sandbox);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), full, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("MaelstromInstallerTest-", Path.GetFileName(full));
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }
}
