using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Bridges the bounded facts session to the existing W101 UI observer.
///
/// Uses the supplied OperationScheduler — never a second scheduler or lease — and
/// the reviewed pinned observer path. Each cycle is one bounded observation; its
/// restored result is mapped into facts. No input, no action, no auto-retarget.</summary>
internal static class W101FactsCycle
{
    internal const int CycleSeconds = 5;

    internal static Func<CancellationToken, Task<W101UiTreeResult?>> Create(
        OperationScheduler scheduler, WizwalkerTarget target, WizwalkerLaunch launch,
        bool includePoseContext = false) => async token =>
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        var session = new WizwalkerSession(target, Enabled: true);
        var ticket = W101UiTreeOperation.Submit(scheduler, session, launch,
            uiTreeEnabled: true, durationSeconds: CycleSeconds,
            includePoseContext: includePoseContext);
        using var cancellation = token.Register(ticket.Cancel);
        var receipt = await ticket.Completion.ConfigureAwait(false);
        var resultPath = receipt.Artifacts.FirstOrDefault(p =>
            Path.GetFileName(p) == "ui-result.json");
        if (receipt.State != OperationState.Completed || resultPath is null)
            throw new InvalidOperationException("Facts observation stopped: " + receipt.Message);
        var result = JsonSerializer.Deserialize<W101UiTreeResult>(
            GameExperiments.BoundedBytes(resultPath, W101UiTreeOperation.MaximumResultBytes),
            WizwalkerOperation.Json)
            ?? throw new InvalidDataException("Facts observation returned no result.");
        if (!result.CleanupConfirmed || !result.HookRestored)
            throw new InvalidOperationException("Facts observation hook restoration is unconfirmed.");
        return result;
    };
}
