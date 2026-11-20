using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDancePlayerLauncherTests
{
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "Maelstrom launch space ü " + Guid.NewGuid().ToString("N"));
        public string Executable => Path.Combine(Root, "Maelstrom.exe");
        public string Data => Path.Combine(Root, "User data");
        public PetDancePreset Preset { get; }
        public GameWindow Window { get; } = new((nint)12345, 42, 12345678, "WizardGraphicalClient", "Wizard101");
        public Files()
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Executable, "launch preparation never executes this fixture");
            Preset = new(Path.Combine(Root, "profile.json"), Path.Combine(Root, "audio-comparison.json"));
            File.WriteAllText(Preset.ProfilePath, "already validated by the preset loader");
            File.WriteAllText(Preset.AudioComparisonPath, "already validated by the preset loader");
        }
        public void Dispose() => Directory.Delete(Root, true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LaunchUsesCurrentNativeExecutableAndStructuredArgumentsWithoutAutoStart(bool audio)
    {
        using var files = new Files();
        var launch = PetDancePlayerLauncher.BuildStartInfo(files.Executable, files.Preset, files.Window, files.Data, audio);
        var info = launch.StartInfo;
        Assert.Equal(files.Executable, info.FileName);
        Assert.Equal(files.Root, info.WorkingDirectory);
        Assert.False(info.UseShellExecute);
        Assert.True(info.CreateNoWindow);
        Assert.Empty(info.Arguments);
        Assert.Equal(audio ? 9 : 8, info.ArgumentList.Count);
        Assert.Equal(audio ? "--pet-dance-sound-trial" : "--pet-dance-visual-trial", info.ArgumentList[0]);
        Assert.Equal("12345", info.ArgumentList[1]);
        Assert.Equal(files.Preset.ProfilePath, info.ArgumentList[2]);
        if (audio) Assert.Equal(files.Preset.AudioComparisonPath, info.ArgumentList[3]);
        else Assert.DoesNotContain(files.Preset.AudioComparisonPath, info.ArgumentList);
        Assert.Equal(launch.ReportDirectory, info.ArgumentList[^5]);
        Assert.Equal("--respect-control-setting", info.ArgumentList[^4]);
        Assert.Equal("--arena=WizardCity", info.ArgumentList[^3]);
        Assert.Equal("--return-native-idle", info.ArgumentList[^2]);
        Assert.Equal("--target-process=42:12345678", info.ArgumentList[^1]);
        var options = PetDanceTrialOptions.Parse(info.ArgumentList.Skip(audio ? 5 : 4));
        Assert.True(options.RespectControlSetting);
        Assert.True(options.ExpectedTarget!.Matches(files.Window));
        Assert.True(options.ReturnToNativeIdle);
        Assert.Equal(Path.Combine(files.Data, "reports", "pet-dance"), Path.GetDirectoryName(launch.ReportDirectory));
        Assert.False(Directory.Exists(files.Data));
        Assert.False(File.Exists(Path.Combine(launch.ReportDirectory, "start.request")));
    }

    [Fact]
    public void RoutineChoicesAreFrozenInArgumentsAndUnqualifiedAudioCannotLaunch()
    {
        using var files = new Files();
        var preferences = new PetTrainingPreferences(Arena: PetDanceArena.Mooshu);
        var launch = PetDancePlayerLauncher.BuildStartInfo(files.Executable, files.Preset, files.Window, files.Data, false, preferences);
        var parsed = PetDanceTrialOptions.Parse(launch.StartInfo.ArgumentList.Skip(4));
        Assert.True(parsed.ReturnToNativeIdle);
        Assert.Equal(PetDanceArena.Mooshu, parsed.Arena);
        Assert.Throws<InvalidOperationException>(() => PetDancePlayerLauncher.BuildStartInfo(files.Executable, files.Preset,
            files.Window, files.Data, true, preferences));
        Assert.False(Directory.Exists(files.Data));
    }

    [Fact]
    public void EachPreparedRunHasANewNonexistentReportDirectory()
    {
        using var files = new Files();
        var first = PetDancePlayerLauncher.BuildStartInfo(files.Executable, files.Preset, files.Window, files.Data, false);
        var second = PetDancePlayerLauncher.BuildStartInfo(files.Executable, files.Preset, files.Window, files.Data, false);
        Assert.NotEqual(first.ReportDirectory, second.ReportDirectory);
        Assert.False(Directory.Exists(first.ReportDirectory));
        Assert.False(Directory.Exists(second.ReportDirectory));
    }

    [Theory]
    [InlineData("wrong-game")]
    [InlineData("missing-handle")]
    [InlineData("missing-pid")]
    [InlineData("missing-start")]
    public void InvalidSelectedTargetsCannotPrepareAPlayer(string fault)
    {
        using var files = new Files();
        var target = fault switch
        {
            "wrong-game" => files.Window with { ProcessName = "AnotherGame" },
            "missing-handle" => files.Window with { Handle = 0 },
            "missing-pid" => files.Window with { ProcessId = 0 },
            _ => files.Window with { StartedUtcTicks = 0 }
        };
        Assert.Throws<InvalidOperationException>(() => PetDancePlayerLauncher.BuildStartInfo(files.Executable, files.Preset, target, files.Data, false));
    }

    [Fact]
    public void MissingPackagedFilesAndRelativeDataPathFailBeforeProcessLaunch()
    {
        using var files = new Files();
        Assert.Throws<ArgumentException>(() => PetDancePlayerLauncher.BuildStartInfo(files.Executable, files.Preset, files.Window, "relative-data", false));
        Assert.Throws<InvalidOperationException>(() => PetDancePlayerLauncher.BuildStartInfo(Path.Combine(files.Root, "absent.exe"), files.Preset, files.Window, files.Data, false));
        var missing = files.Preset with { ProfilePath = Path.Combine(files.Root, "absent.json") };
        Assert.Throws<InvalidOperationException>(() => PetDancePlayerLauncher.BuildStartInfo(files.Executable, missing, files.Window, files.Data, false));
        Assert.False(Directory.Exists(files.Data));
    }
}
