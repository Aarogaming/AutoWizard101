using ProjectMaelstrom.Models;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class W101InteractionPolicyTests
{
    [Fact]
    public void VisibleControlsAndOfflineResearchRemainAdmissible()
    {
        foreach (var method in new[] { W101InteractionMethod.VisibleUi, W101InteractionMethod.OfflineResearch })
        {
            Assert.Null(W101InteractionPolicy.BlockReason(method));
            W101InteractionPolicy.Require(method);
        }
    }

    [Fact]
    public void ProcessObservationIsDistinctAndCurrentlyNotEnabled()
    {
        W101InteractionPolicy.IsProcessObservationAllowed = false;
        Assert.Equal(W101InteractionMethod.ProcessMemoryObservation, W101InteractionPolicy.ToolMethod("wiztype"));
        Assert.Equal(W101InteractionMethod.ClientInternals, W101InteractionPolicy.ToolMethod("deimos"));
        Assert.Contains("process-memory observation is not enabled", W101InteractionPolicy.ToolBlockReason("wiztype")!);
        Assert.NotNull(W101InteractionPolicy.BlockReason(W101InteractionMethod.LiveGameTraffic));
    }

    [Fact]
    public void ProcessObservationCanBePermittedByExplicitException()
    {
        try
        {
            W101InteractionPolicy.IsProcessObservationAllowed = true;
            Assert.Null(W101InteractionPolicy.BlockReason(W101InteractionMethod.ProcessMemoryObservation));
            Assert.Null(W101InteractionPolicy.ToolBlockReason("wiztype"));

            // Other methods remain strictly blocked
            Assert.NotNull(W101InteractionPolicy.BlockReason(W101InteractionMethod.ClientInternals));
            Assert.NotNull(W101InteractionPolicy.ToolBlockReason("deimos"));
            Assert.NotNull(W101InteractionPolicy.BlockReason(W101InteractionMethod.LiveGameTraffic));
            Assert.NotNull(W101InteractionPolicy.BlockReason(W101InteractionMethod.UnreviewedCode));
        }
        finally
        {
            W101InteractionPolicy.IsProcessObservationAllowed = false;
        }
    }

    [Theory]
    [InlineData("deimos")]
    [InlineData("wiztype")]
    [InlineData("dance")]
    [InlineData("claims-to-be-ui-only")]
    public async Task LaunchAdmissionRejectsBeforeRequiringWorkspaceOrStartingProcesses(string tool)
    {
        using var service = new ToolIntegrationService("missing-workspace");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchAsync(tool, new ToolReadiness { SourcePresent = true }));
        Assert.Contains("UI-only mode", error.Message);
        Assert.False(service.HasOpenTools);
    }

    [Fact]
    public async Task GuidedProbeAndRunRejectBeforeSetupFilesOrBackendAccess()
    {
        using var service = new ToolIntegrationService("missing-workspace");
        var profile = new ActivityProfile { Activity = "questing" };
        var probe = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckActivityAsync(profile, probe: true));
        Assert.Contains("UI-only mode", probe.Message);
        var run = await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchGuidedAsync(profile, new ToolReadiness(), null));
        Assert.Contains("UI-only mode", run.Message);
        Assert.Null(service.GuidedActivity);
    }

    [Fact]
    public void ManifestClaimsDoNotAuthorizePluginAssemblyCode()
    {
        var live = new ExecutionPolicySnapshot { AllowLiveAutomation = true, Profile = ExecutionProfile.ExperimentalSimulation };
        var claimedOffline = new PluginInfo { AssemblyPath = "unreviewed.dll", Capabilities = [PluginCapability.OverlayWidgets], Status = PluginStatus.Allowed };
        var result = PluginGate.Evaluate(claimedOffline, live);
        Assert.Equal(PluginStatus.Blocked, result.status);
        Assert.Contains("UI-only mode", result.reason);
        var manifestOnly = new PluginInfo { Capabilities = [PluginCapability.OverlayWidgets], Status = PluginStatus.Allowed };
        Assert.Equal(PluginStatus.Allowed, PluginGate.Evaluate(manifestOnly, live).status);
    }

    [Fact]
    public void LegacyScriptAdmissionCannotBeOverriddenByManifestStatus()
    {
        var service = ScriptLibraryService.Instance;
        var savedDryRun = service.DryRun;
        try
        {
            service.DryRun = false;
            var script = new ScriptDefinition("missing-manifest", new ScriptManifest { Name = "Native claim", Status = "native", EntryPoint = "missing.exe" }, "missing-root");
            Assert.Contains("UI-only mode", Assert.Throws<InvalidOperationException>(() => service.ScheduleScript(script)).Message);
            Assert.Contains("UI-only mode", Assert.Throws<InvalidOperationException>(() => service.StartScript(script)).Message);
        }
        finally { service.DryRun = savedDryRun; }
    }
}
