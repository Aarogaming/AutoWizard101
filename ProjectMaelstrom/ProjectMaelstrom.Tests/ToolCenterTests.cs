using System.Diagnostics;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class ToolCenterTests
{
    [Theory]
    [InlineData("companion", true, false, false, false, false, null)]
    [InlineData("dance", true, false, false, true, false, "UI-only mode")]
    [InlineData("dance", true, true, true, false, false, "UI-only mode")]
    [InlineData("deimos", true, true, true, true, true, "UI-only mode")]
    [InlineData("deimos", true, true, true, true, false, "UI-only mode")]
    [InlineData("wiztype", true, false, false, false, false, "process-memory observation is not enabled")]
    [InlineData("wizsdk", true, true, true, true, false, "supporting library")]
    [InlineData("companion", false, false, false, false, false, "needs setup")]
    public void LaunchChecksExplainWhatThePlayerNeeds(string tool, bool installed, bool live, bool experimental, bool gameOpen, bool busy, string? expected)
    {
        var reason = ToolIntegrationService.LaunchBlockReason(tool, installed, live, experimental, gameOpen, busy);
        if (expected == null) Assert.Null(reason);
        else Assert.Contains(expected, reason!);
    }

    [Fact]
    public void ArgumentsWithSpacesAreKeptSeparateAndShellIsDisabled()
    {
        var info = ToolIntegrationService.BuildStartInfo("C:/Tools With Spaces/python.exe", "C:/Work With Spaces", "C:/Work With Spaces/hub.py",
            new[] { "compare-types", "C:/Old Dump.json", "C:/New Dump.json", "--output", "C:/Output Report.json" });
        Assert.False(info.UseShellExecute);
        Assert.Equal("C:/Old Dump.json", info.ArgumentList[2]);
        Assert.Equal("C:/Output Report.json", info.ArgumentList[5]);
        Assert.False(info.Environment.ContainsKey("PYTHONPATH"));
    }

    [Fact]
    public void WorkspaceDiscoveryWalksParentsAndRejectsAnEmptyFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "tools-root-" + Guid.NewGuid().ToString("N"));
        var tools = Path.Combine(root, "tools");
        var child = Path.Combine(root, "build", "app");
        Directory.CreateDirectory(tools);
        Directory.CreateDirectory(child);
        try
        {
            Assert.Null(ToolIntegrationService.FindWorkspace(root));
            File.WriteAllText(Path.Combine(tools, "wizard101_hub.py"), "# fixture");
            Assert.Equal(root, ToolIntegrationService.FindWorkspace(child));
        }
        finally
        {
            File.Delete(Path.Combine(tools, "wizard101_hub.py"));
            Directory.Delete(child);
            Directory.Delete(Path.GetDirectoryName(child)!);
            Directory.Delete(tools);
            Directory.Delete(root);
        }
    }

    [Fact]
    public async Task CommandReadsBothStreamsAndPreservesFailureReason()
    {
        var helper = Path.Combine(AppContext.BaseDirectory, "Maelstrom.TestProcess.dll");
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { helper, "--stdout", new string('A', 16000), "--stderr", "Helpful failure", "--exit", "7" }) info.ArgumentList.Add(argument);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ToolIntegrationService.RunOnceAsync(info));
        Assert.Contains("Helpful failure", exception.Message);
    }

    [Fact]
    public async Task CancellationStopsOwnedHelper()
    {
        var helper = Path.Combine(AppContext.BaseDirectory, "Maelstrom.TestProcess.dll");
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { helper, "--sleepMs", "30000" }) info.ArgumentList.Add(argument);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ToolIntegrationService.RunOnceAsync(info, cancellation.Token));
    }

    [Fact]
    public void ClosingProcessGroupStopsLauncherAndDescendant()
    {
        var directory = Path.Combine(Path.GetTempPath(), "owned-tool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var pidFile = Path.Combine(directory, "child.pid");
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "Maelstrom.TestProcess.dll"), "--spawnChildPidFile", pidFile, "--sleepMs", "10000" }) info.ArgumentList.Add(argument);
        using var job = new OwnedProcessJob();
        using var launcher = Process.Start(info)!;
        try
        {
            job.Attach(launcher);
            Assert.True(SpinWait.SpinUntil(() => File.Exists(pidFile) && new FileInfo(pidFile).Length > 0, 5000));
            using var child = Process.GetProcessById(int.Parse(File.ReadAllText(pidFile)));
            job.Dispose();
            Assert.True(launcher.WaitForExit(5000));
            Assert.True(child.WaitForExit(5000));
        }
        finally
        {
            job.Dispose();
            if (!launcher.HasExited) launcher.Kill(entireProcessTree: true);
            if (File.Exists(pidFile)) File.Delete(pidFile);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void InstalledStatusDoesNotClaimGameCompatibility()
    {
        var readiness = new ToolReadiness { SourcePresent = true, EntryPresent = true };
        Assert.True(readiness.Installed);
        Assert.Contains("unverified", readiness.DisplayStatus);
        readiness.MissingPackages = new[] { "missing" };
        Assert.False(readiness.Installed);
        Assert.Equal("Needs setup", readiness.DisplayStatus);
    }
}
