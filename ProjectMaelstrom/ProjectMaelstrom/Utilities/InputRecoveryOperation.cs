namespace ProjectMaelstrom.Utilities;

internal enum RecoveryFlowStage { Initial, Stopping, Blocked, Ready, Checking, Restored }
/// <summary>Message is fixed application wording, never raw errors, paths or warning contents.</summary>
internal sealed record RecoveryFlowView(RecoveryFlowStage Stage, string Message, bool CanRestore = false, string? EvidenceDirectory = null);
internal interface IInputRecoveryWorkflow : IDisposable
{
    RecoveryFlowView State { get; }
    Task<RecoveryFlowView> StopAsync(CancellationToken token);
    Task<RecoveryFlowView> CheckAsync(CancellationToken token);
    Task<RecoveryFlowView> RestoreAsync(CancellationToken token);
}
internal interface IInputRecoveryProbe
{
    InputRecoverySample Read(InputRecoveryChallenge challenge, bool workersStopped);
}

/// <summary>Explicit user-driven recovery. Preview never resets state; Restore obtains a
/// new challenge under the shared lock. Neither step starts a game or resumes work.</summary>
internal sealed class InputRecoveryOperation : IInputRecoveryWorkflow
{
    private readonly string _directory;
    private readonly IInputRecoveryProbe _probe;
    private readonly Func<long> _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<string, Func<InputRecoveryChallenge, CancellationToken, Task<InputRecoverySample>>, CancellationToken, Task<InputRecoveryResult>> _recover;
    private readonly SemaphoreSlim _serial = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private OperationScheduler.LiveRecoveryHold? _hold;
    private int _disposed;
    public RecoveryFlowView State { get; private set; } = new(RecoveryFlowStage.Initial,
        "Stop routines, then check that all controls have been released. Interrupted routines will stay stopped.");

    public InputRecoveryOperation() : this(GameInputLease.DirectoryPath, new WindowsInputRecoveryProbe()) { }
    internal InputRecoveryOperation(string directory, IInputRecoveryProbe probe,
        Func<string, Func<InputRecoveryChallenge, CancellationToken, Task<InputRecoverySample>>, CancellationToken, Task<InputRecoveryResult>>? recover = null,
        Func<long>? clock = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        _probe = probe; _clock = clock ?? (() => Environment.TickCount64);
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
        _recover = recover ?? GameInputRecovery.RunAsync;
    }

    public Task<RecoveryFlowView> StopAsync(CancellationToken token) => RunAsync(async cancellation =>
    {
        _hold ??= OperationScheduler.PauseLiveForRecovery(_directory);
        State = new(RecoveryFlowStage.Stopping, "Stopping live routines and waiting for their cleanup. Offline reviews can continue.");
        bool stopped = await _hold.StopAndWaitAsync(TimeSpan.FromSeconds(5), cancellation).ConfigureAwait(false);
        return State = stopped
            ? new(RecoveryFlowStage.Ready, "Routines have stopped. Choose Check cleanup, then leave the keyboard and mouse still briefly.")
            : new(RecoveryFlowStage.Blocked, "A routine is still cleaning up. Leave it stopped and try Stop routines again shortly.");
    }, token);

    public Task<RecoveryFlowView> CheckAsync(CancellationToken token) => RunAsync(async cancellation =>
    {
        if (_hold?.IsHeld != true || !_hold.IsDrained) return StopFirst();
        State = new(RecoveryFlowStage.Checking, "Checking the desktop and released controls. Keep Maelstrom active and leave the keyboard and mouse still briefly.");
        var challenge = new InputRecoveryChallenge(Guid.NewGuid().ToString("N"), _clock());
        var first = await ObserveAsync(challenge, cancellation).ConfigureAwait(false);
        if (CheckSample(first, challenge) is { } firstProblem) return State = firstProblem;
        await _delay(TimeSpan.FromSeconds(1), cancellation).ConfigureAwait(false);
        var second = await ObserveAsync(challenge, cancellation).ConfigureAwait(false);
        if (CheckSample(second, challenge) is { } secondProblem) return State = secondProblem;
        if (second.ObservedAtMilliseconds - first.ObservedAtMilliseconds < 1000 || second.ActivityRevision != first.ActivityRevision)
            return State = new(RecoveryFlowStage.Blocked, "Input changed during the check. Release all keys and mouse buttons, then check again.");
        return State = new(RecoveryFlowStage.Ready,
            "Cleanup checks passed. Choose Restore controls for one final check. No routine will restart automatically.", true);
    }, token);

    public Task<RecoveryFlowView> RestoreAsync(CancellationToken token) => RunAsync(async cancellation =>
    {
        if (!State.CanRestore || _hold?.IsHeld != true || !_hold.IsDrained) return StopFirst();
        State = new(RecoveryFlowStage.Checking, "Verifying cleanup again and saving the recovery record. Keep Maelstrom active and leave the keyboard and mouse still briefly.");
        var result = await _recover(_directory, ObserveAsync, cancellation).ConfigureAwait(false);
        // The transaction owns the commit boundary. Cancellation after a successful
        // commit must not turn an already-completed recovery into a false failure.
        if (!result.Recovered)
            return State = new(RecoveryFlowStage.Blocked, FriendlyFailure(result.Code), false, result.EvidenceDirectory);
        ReleaseHold();
        return State = new(RecoveryFlowStage.Restored,
            result.Code == "recovered-with-close-warning"
                ? "Controls were restored, but closing the recovery file could not be confirmed. Save your work and restart Maelstrom before starting a new routine. No routine was restarted."
                : "Recovery is complete. Start a new routine when you are ready; your control settings and interrupted runs are unchanged.", false, result.EvidenceDirectory);
    }, token);

    private Task<InputRecoverySample> ObserveAsync(InputRecoveryChallenge challenge, CancellationToken token) =>
        Task.Run(() => { token.ThrowIfCancellationRequested(); return _probe.Read(challenge, _hold?.IsHeld == true && _hold.IsDrained); }, token);

    private RecoveryFlowView? CheckSample(InputRecoverySample sample, InputRecoveryChallenge challenge)
    {
        if (!sample.WorkersStopped || _hold?.IsHeld != true || !_hold.IsDrained) return StopFirst();
        if (!sample.DesktopAvailable || !sample.InputStateKnown)
            return new(RecoveryFlowStage.Blocked, "Windows could not confirm the active desktop. Unlock your session, keep Maelstrom active, and try Check cleanup again.");
        if (!sample.InputsReleased)
            return new(RecoveryFlowStage.Blocked, "A key or mouse button is still held. Release it, or tap and release it if it feels stuck, then check again.");
        long now = _clock();
        if (sample.ChallengeId != challenge.Id || sample.ActivityRevision < 0 || sample.ObservedAtMilliseconds < challenge.IssuedAtMilliseconds ||
            sample.ObservedAtMilliseconds > now || now - sample.ObservedAtMilliseconds > 100)
            return new(RecoveryFlowStage.Blocked, "The control check was interrupted or became out of date. Please check again.");
        return null;
    }
    private RecoveryFlowView StopFirst() => State = new(RecoveryFlowStage.Blocked, "Choose Stop routines before checking or restoring controls.");
    private static string FriendlyFailure(string code) => code switch
    {
        "owner-or-recovery-changed" or "busy" or "owner-active" or "quarantined" => "Another worker may still own input, or the recovery state changed. Leave routines stopped and close other Maelstrom workers. If it stays blocked, save your work, restart Maelstrom, and retry recovery.",
        "cancelled" => "Recovery was cancelled. No routine was resumed. Choose Check cleanup when you are ready.",
        "timed-out" => "The cleanup check took too long. Keep Maelstrom active, release the controls, and check again. No routine was resumed.",
        "verification-or-storage-failed" => "Cleanup or its saved record could not be verified. Keep Maelstrom active, release the controls, and check again. If it keeps failing, check disk space and folder access.",
        "verification-failed" or "input-held" or "stale-sample" or "activity-changed" => "Cleanup could not be confirmed. Release all keys and mouse buttons, keep them still briefly, then check again.",
        "desktop-unavailable" => "Windows could not confirm the active desktop. Unlock your session, keep Maelstrom active, and check again.",
        "state-changed" => "The recovery state changed during the check. Leave routines stopped and check again.",
        _ => "Recovery could not be completed. Check available disk space and folder access, then stop routines and check again. No routine was resumed."
    };
    private async Task<RecoveryFlowView> RunAsync(Func<CancellationToken, Task<RecoveryFlowView>> action, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!await _serial.WaitAsync(0, token).ConfigureAwait(false)) return State;
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
            linked.Token.ThrowIfCancellationRequested();
            return await action(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        { return State = new(RecoveryFlowStage.Blocked, "The check was cancelled. Recovery was not completed and no routine was resumed."); }
        catch (Exception)
        { return State = new(RecoveryFlowStage.Blocked, "The recovery check could not finish. Leave routines stopped, check folder access and available disk space, then try again."); }
        finally
        {
            if (Volatile.Read(ref _disposed) != 0) ReleaseHold();
            _serial.Release();
        }
    }
    private void ReleaseHold() { _hold?.Dispose(); _hold = null; }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        if (_serial.Wait(0)) { try { ReleaseHold(); } finally { _serial.Release(); } }
    }
}
