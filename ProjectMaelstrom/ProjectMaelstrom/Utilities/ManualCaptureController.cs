namespace ProjectMaelstrom.Utilities;

/// <summary>Operating mode for a manual-capture session. NORMAL is the product default;
/// SAFE is an explicit, per-session caution mode for step-by-step diagnostics.</summary>
internal enum ManualCaptureMode { Normal, Safe }

/// <summary>State of the manual-capture dispatch gate. ExplicitlyPaused and Stopped are
/// latches: no timer, quiet period or countdown can leave them.</summary>
internal enum ManualCaptureState
{
    Idle, Running, ImplicitlyPaused, ResumePending, ExplicitlyPaused, Stopping, Stopped
}

/// <summary>What caused the most recent implicit pause. Diagnostics only; never an
/// authorization to resume.</summary>
internal enum ManualPauseTrigger { None, Loading, FocusLost, MouseActivity, VerificationLost }

/// <summary>Observable, immutable view of the dispatch gate for the overlay and receipts.</summary>
internal sealed record ManualCaptureSnapshot(
    ManualCaptureMode Mode,
    ManualCaptureState State,
    ManualPauseTrigger Trigger,
    bool MayDispatch,
    bool ExplicitLatch,
    double QuietMs,
    double CountdownMs,
    string Reason);

/// <summary>Pure dispatch gate for headless manual-capture sessions.
///
/// NORMAL: loading, focus loss or mouse activity close the gate; it reopens only after
/// sustained quiet, a visible pre-resume countdown, and fresh verification — never while
/// an explicit pause/stop latch, recovery hold, or uncertain cleanup is present.
/// SAFE: implicit pauses never reopen automatically; an explicit resume is required.
///
/// The controller owns no window, no input lease and no timer. A host supplies time and
/// the verification delegates, so every transition is deterministic and offline-testable.</summary>
internal sealed class ManualCaptureController
{
    internal const double QuietRequiredMs = 3000;
    internal const double ResumeCountdownMs = 3000;

    private readonly ManualCaptureMode _mode;
    private readonly Func<bool> _verified;
    private readonly Func<bool> _recoveryRequired;
    private ManualCaptureState _state = ManualCaptureState.Idle;
    private ManualPauseTrigger _trigger = ManualPauseTrigger.None;
    private double _pausedAt;
    private double _countdownAt = -1;
    private bool _explicitLatch;
    private string _reason = "idle";

    internal ManualCaptureController(ManualCaptureMode mode, Func<bool> verified, Func<bool> recoveryRequired)
    {
        _mode = mode;
        _verified = verified ?? throw new ArgumentNullException(nameof(verified));
        _recoveryRequired = recoveryRequired ?? throw new ArgumentNullException(nameof(recoveryRequired));
    }

    internal ManualCaptureSnapshot Snapshot(double nowMs) => new(
        _mode, _state, _trigger,
        MayDispatch(nowMs), _explicitLatch,
        _state is ManualCaptureState.ImplicitlyPaused or ManualCaptureState.ResumePending
            ? Math.Max(0, nowMs - _pausedAt) : 0,
        _countdownAt >= 0 ? Math.Max(0, nowMs - _countdownAt) : 0,
        _reason);

    /// <summary>The only authorization to dispatch. Time-dependent: callers must pass the
    /// current clock and re-check immediately before each action.</summary>
    internal bool MayDispatch(double nowMs)
    {
        if (_state != ManualCaptureState.Running || _explicitLatch) return false;
        return _verified() && !_recoveryRequired();
    }

    internal void Start(double nowMs)
    {
        if (_explicitLatch || _state is ManualCaptureState.Stopped or ManualCaptureState.Stopping)
            return;
        _state = ManualCaptureState.Running;
        _trigger = ManualPauseTrigger.None;
        _countdownAt = -1;
        _reason = "running";
        _ = nowMs;
    }

    /// <summary>Implicit pause from loading, focus loss or mouse activity. Explicit latch
    /// states are never overwritten by an implicit trigger.</summary>
    internal void ImplicitPause(ManualPauseTrigger trigger, double nowMs)
    {
        if (_explicitLatch || _state is ManualCaptureState.Stopped or ManualCaptureState.Stopping)
            return;
        if (_state == ManualCaptureState.ImplicitlyPaused) return;
        // A fresh activity trigger during the pre-resume countdown cancels that
        // countdown and restarts the quiet window; dispatch must not reopen while
        // the owner is actively moving/using the mouse.
        _state = ManualCaptureState.ImplicitlyPaused;
        _trigger = trigger;
        _pausedAt = nowMs;
        _countdownAt = -1;
        _reason = trigger switch
        {
            ManualPauseTrigger.Loading => "loading",
            ManualPauseTrigger.FocusLost => "focus-lost",
            ManualPauseTrigger.MouseActivity => "mouse-activity",
            _ => "verification-lost",
        };
    }

    /// <summary>Explicit pause is a latch. Only Stop()/HardStop() may leave it, and the
    /// hard-stop keypress is handled by Stop() in every mode.</summary>
    internal void ExplicitPause(double nowMs)
    {
        if (_state is ManualCaptureState.Stopped) return;
        _explicitLatch = true;
        _state = ManualCaptureState.ExplicitlyPaused;
        _trigger = ManualPauseTrigger.None;
        _countdownAt = -1;
        _reason = "explicit-pause";
        _ = nowMs;
    }

    internal void Stop(double nowMs)
    {
        _explicitLatch = true;
        _state = ManualCaptureState.Stopped;
        _countdownAt = -1;
        _reason = "stopped";
        _ = nowMs;
    }

    /// <summary>Resume from SAFE mode or after an explicit pause is never automatic: the
    /// host must call this in response to a real hotkey/click. A hard-stop latch cannot be
    /// cleared by Resume; use Start() only after a fresh session is created.</summary>
    internal bool Resume(double nowMs)
    {
        if (_state == ManualCaptureState.Stopped) return false;
        if (!_verified() || _recoveryRequired()) return false;
        _explicitLatch = false;
        _state = ManualCaptureState.Running;
        _trigger = ManualPauseTrigger.None;
        _countdownAt = -1;
        _reason = "resumed";
        _ = nowMs;
        return true;
    }

    /// <summary>Advances implicit-pause timers. NORMAL only: SAFE never leaves an implicit
    /// pause without an explicit Resume(). Verification is re-checked at BOTH transitions
    /// and any failure restarts the quiet window.</summary>
    internal void Observe(double nowMs)
    {
        if (_mode != ManualCaptureMode.Normal || _explicitLatch) return;
        if (_state == ManualCaptureState.ImplicitlyPaused)
        {
            if (!_verified() || _recoveryRequired())
            {
                _pausedAt = nowMs;
                _reason = "verification-withheld";
                return;
            }
            if (nowMs - _pausedAt < QuietRequiredMs) return;
            _state = ManualCaptureState.ResumePending;
            _countdownAt = nowMs;
            _reason = "resume-countdown";
            return;
        }
        if (_state == ManualCaptureState.ResumePending)
        {
            if (!_verified() || _recoveryRequired())
            {
                _state = ManualCaptureState.ImplicitlyPaused;
                _pausedAt = nowMs;
                _countdownAt = -1;
                _trigger = ManualPauseTrigger.VerificationLost;
                _reason = "verification-withheld";
                return;
            }
            if (nowMs - _countdownAt < ResumeCountdownMs) return;
            _state = ManualCaptureState.Running;
            _trigger = ManualPauseTrigger.None;
            _countdownAt = -1;
            _reason = "auto-resumed";
        }
    }
}
