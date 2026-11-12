namespace ProjectMaelstrom.Utilities;

/// <summary>Presentation only. A ready screen is never permission to clear recovery:
/// the workflow must verify cleanup again when Restore is explicitly requested.</summary>
internal sealed record InputRecoveryPresentation(string Heading, string Message, string Steps,
    bool CanStop, bool CanCheck, bool CanRestore)
{
    internal static InputRecoveryPresentation From(RecoveryFlowView state, bool busy = false,
        bool closing = false, bool failed = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (closing) return new("Finishing safely", "Waiting for the current check to finish before closing. Controls stay paused unless restoration already completed.",
            "Closing does not start or restart a routine.", false, false, false);
        if (failed) return new("Controls remain paused", "This step could not finish. Try stopping routines again, then check cleanup. No routine was restarted.",
            "Stop routines → Check cleanup → Restore controls", !busy, false, false);
        var restored = state.Stage == RecoveryFlowStage.Restored;
        var ready = state.Stage == RecoveryFlowStage.Ready;
        var heading = state.Stage switch
        {
            RecoveryFlowStage.Stopping => "Stopping routines",
            RecoveryFlowStage.Checking => "Checking cleanup",
            RecoveryFlowStage.Restored => "Game controls restored",
            RecoveryFlowStage.Ready when state.CanRestore => "Ready to restore controls",
            RecoveryFlowStage.Ready => "Routines stopped",
            RecoveryFlowStage.Blocked => "Controls remain paused",
            _ => "Restore game controls"
        };
        // Message is fixed, user-facing application copy from IInputRecoveryWorkflow.
        // Exception details, saved notices and EvidenceDirectory never enter this view.
        var message = string.IsNullOrWhiteSpace(state.Message)
            ? restored ? "You can start a new routine when you choose. Previous runs remain stopped."
            : "Stop routines, check that cleanup finished, then restore game controls."
            : state.Message;
        var idle = !busy && state.Stage is not (RecoveryFlowStage.Stopping or RecoveryFlowStage.Checking);
        return new(heading, message,
            restored ? "Start a new routine yourself when ready. Nothing resumes automatically."
                : "1. Stop routines   →   2. Check cleanup   →   3. Restore controls",
            idle && !restored, idle && state.Stage is RecoveryFlowStage.Ready or RecoveryFlowStage.Blocked,
            idle && ready && state.CanRestore);
    }
}
