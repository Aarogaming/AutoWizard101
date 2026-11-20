namespace ProjectMaelstrom.Utilities;

/// <summary>Explicit windowless recovery on the active accessible desktop.
/// Uses verified asynchronous input state; never activates a window or sends input.
/// The ordinary recovery transaction still owns stop, verification and commit.</summary>
internal static class CliInputRecovery
{
    internal static async Task<RecoveryFlowView> RunAsync(CancellationToken token)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(TimeSpan.FromSeconds(25));
        token = budget.Token;
        try
        {
            var baseline = W101DialogueInput.Activity();
            await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
            if (baseline != W101DialogueInput.Activity() || DesktopActivityMonitor.AnyKeyHeld())
                return new(RecoveryFlowStage.Blocked, "Desktop activity detected; recovery was not started.");
            using var workflow = new InputRecoveryOperation(GameInputLease.DirectoryPath,
                new WindowsInputRecoveryProbe(requireOwnForeground: false));
            await workflow.StopAsync(token).ConfigureAwait(false);
            var checkedState = await workflow.CheckAsync(token).ConfigureAwait(false);
            return checkedState.CanRestore
                ? await workflow.RestoreAsync(token).ConfigureAwait(false)
                : checkedState;
        }
        catch (OperationCanceledException)
        { return new(RecoveryFlowStage.Blocked, "Cleanup verification cancelled or timed out. Controls remain blocked."); }
        catch (Exception)
        { return new(RecoveryFlowStage.Blocked, "Cleanup verification did not complete. Controls remain blocked."); }
    }
}
