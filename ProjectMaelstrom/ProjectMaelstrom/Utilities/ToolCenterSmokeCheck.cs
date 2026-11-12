using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Local end-to-end check: tool inventory, boss import, and a headless Companion lifecycle.</summary>
internal static class ToolCenterSmokeCheck
{
    public static async Task RunAsync(string output)
    {
        using var service = new ToolIntegrationService();
        var states = await service.CheckAsync().ConfigureAwait(false);
        var companion = states.Single(state => state.Tool == "companion");
        var bosses = await service.UpdateBossLibraryAsync().ConfigureAwait(false);
        var oldQt = Environment.GetEnvironmentVariable("QT_QPA_PLATFORM");
        var started = false;
        var stopped = false;
        try
        {
            Environment.SetEnvironmentVariable("QT_QPA_PLATFORM", "offscreen");
            await service.LaunchAsync("companion", companion).ConfigureAwait(false);
            for (var attempt = 0; attempt < 80; attempt++)
            {
                await Task.Delay(100).ConfigureAwait(false);
                if (service.Messages.Contains("App started.")) { started = service.IsRunning("companion"); break; }
                if (!service.IsRunning("companion")) break;
            }
            service.Stop("companion");
            for (var attempt = 0; attempt < 50 && service.IsRunning("companion"); attempt++)
                await Task.Delay(100).ConfigureAwait(false);
            stopped = !service.IsRunning("companion");
        }
        finally { Environment.SetEnvironmentVariable("QT_QPA_PLATFORM", oldQt); }
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            ToolsChecked = states.Count,
            Installed = states.Count(state => state.Installed),
            BossesImported = bosses,
            CompanionStarted = started,
            CompanionStopped = stopped,
            GameAutomationRun = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (!started || !stopped || bosses == 0) throw new InvalidOperationException("Tools lifecycle check failed. See the saved report.");
    }
}
