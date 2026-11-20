using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class GameSessionSettingsTests
{
    private static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
        { "ui-scale", "resolution", "window-mode" };

    private static GameSettingsObservation Observe(long revision, string scale = "Tiny", string resolution = "1280x720", string windowMode = "Windowed") =>
        new(revision, true, new Dictionary<string, string?> { ["ui-scale"] = scale, ["resolution"] = resolution, ["window-mode"] = windowMode });

    private static GameSettingsSession Capture(params GameSettingRequirement[] requirements) =>
        GameSessionSettings.Capture("synthetic-session", requirements.Length == 0 ? new[] { new GameSettingRequirement("ui-scale", "Huge") } : requirements,
            Allowed, Observe(1));

    private static GameSettingsSession Applied()
    {
        var proposed = GameSessionSettings.Advance(Capture(), Observe(2), true);
        var dispatched = GameSessionSettings.RecordDispatch(proposed.Session, proposed.Proposal!.Id);
        var applied = GameSessionSettings.Advance(dispatched, Observe(3, "Huge"), true);
        Assert.Equal(GameSettingsStatus.Ready, applied.Status);
        return applied.Session;
    }

    [Fact]
    public void AppliesOnlyAfterObservationThenRestoresTheActualEntryValue()
    {
        var proposed = GameSessionSettings.Advance(Capture(), Observe(2), true);
        Assert.Equal("Tiny", proposed.Proposal!.BeforeValue);
        Assert.Equal("Huge", proposed.Proposal.AfterValue);
        var dispatched = GameSessionSettings.RecordDispatch(proposed.Session, proposed.Proposal.Id);
        Assert.False(dispatched.Entries[0].Applied);
        var applied = GameSessionSettings.Advance(dispatched, Observe(3, "Huge"), true);
        Assert.True(applied.Session.Entries[0].Applied);
        Assert.Equal(GameSettingsStatus.Ready, applied.Status);
        var restore = GameSessionSettings.Advance(GameSessionSettings.RequestEnd(applied.Session), Observe(4, "Huge"), true);
        Assert.Equal(GameSettingChangeKind.Restore, restore.Proposal!.Kind);
        Assert.Equal("Tiny", restore.Proposal.AfterValue);
        var restoring = GameSessionSettings.RecordDispatch(restore.Session, restore.Proposal.Id);
        Assert.False(restoring.Entries[0].Restored);
        var restored = GameSessionSettings.Advance(restoring, Observe(5), true);
        Assert.Equal(GameSettingsStatus.Restored, restored.Status);
        Assert.True(restored.Session.Entries[0].Restored);
        Assert.Null(restored.Proposal);
    }

    [Fact]
    public void AlreadyCorrectValuesNeedNoChangesOrRestoration()
    {
        var ready = GameSessionSettings.Advance(Capture(new GameSettingRequirement("ui-scale", "Tiny")), Observe(2), true);
        Assert.Equal(GameSettingsStatus.Ready, ready.Status);
        Assert.Null(ready.Proposal);
        Assert.False(ready.Session.Entries[0].Applied);
        var end = GameSessionSettings.Advance(GameSessionSettings.RequestEnd(ready.Session), Observe(3, "Medium"), true);
        Assert.Equal(GameSettingsStatus.Restored, end.Status);
        Assert.Null(end.Proposal); // Never overwrite a later user change to a setting this session did not change.
    }

    [Fact]
    public void MissingUnverifiedAndInvalidOriginalsRejectTheEntireCapture()
    {
        foreach (var observation in new[] {
            Observe(1) with { Verified = false }, Observe(0),
            new GameSettingsObservation(1, true, new Dictionary<string, string?>()),
            new GameSettingsObservation(1, true, new Dictionary<string, string?> { ["ui-scale"] = null }) })
        {
            var session = GameSessionSettings.Capture("test", new[] { new GameSettingRequirement("ui-scale", "Huge") }, Allowed, observation);
            Assert.Equal("unknown-baseline", session.Rejection);
            Assert.Empty(session.Entries);
            Assert.Equal(GameSettingsStatus.Rejected, GameSessionSettings.Advance(session, Observe(2), true).Status);
        }
    }

    [Fact]
    public void PackCannotIntroduceUnreviewedOrSecuritySettings()
    {
        var keys = new HashSet<string> { "security", "ui-scale" };
        var rejected = GameSessionSettings.Capture("test", new[] { new GameSettingRequirement("security", "off") }, keys, Observe(1));
        Assert.Equal("setting-not-reviewed", rejected.Rejection);
        rejected = GameSessionSettings.Capture("test", new[] { new GameSettingRequirement("ui-scale", "Huge") }, new HashSet<string>(), Observe(1));
        Assert.Equal("setting-not-reviewed", rejected.Rejection);
        Assert.Equal("invalid-requirement", Capture(new("ui-scale", "Huge"), new("ui-scale", "Small")).Rejection);
    }

    [Fact]
    public void ProposalIsStableAndIssuedOnceUntilExplicitDispatch()
    {
        var first = GameSessionSettings.Advance(Capture(), Observe(2), true);
        var again = GameSessionSettings.Advance(first.Session, Observe(3), true);
        Assert.Equal(first.Proposal!.Id, again.Session.Pending!.Id);
        Assert.Null(again.Proposal);
        Assert.Equal(GameSettingsStatus.AwaitingDispatch, again.Status);
        var dispatched = GameSessionSettings.RecordDispatch(again.Session, first.Proposal.Id);
        Assert.Throws<InvalidOperationException>(() => GameSessionSettings.RecordDispatch(dispatched, first.Proposal.Id));
        Assert.Throws<InvalidOperationException>(() => GameSessionSettings.RecordDispatch(Capture(), "missing"));
    }

    [Fact]
    public void UnverifiedOrOldObservationCannotConfirmADispatchedClick()
    {
        var first = GameSessionSettings.Advance(Capture(), Observe(2), true);
        var dispatched = GameSessionSettings.RecordDispatch(first.Session, first.Proposal!.Id);
        foreach (var observation in new[] { Observe(3, "Huge") with { Verified = false }, Observe(2, "Huge") })
        {
            var waiting = GameSessionSettings.Advance(dispatched, observation, true);
            Assert.Equal(GameSettingsStatus.Uncertain, waiting.Status);
            Assert.False(waiting.Session.Entries[0].Applied);
            Assert.NotNull(waiting.Session.Pending);
            Assert.Null(waiting.Proposal);
        }
        Assert.Equal(GameSettingsStatus.Ready, GameSessionSettings.Advance(dispatched, Observe(3, "Huge"), true).Status);
    }

    [Fact]
    public void UnchangedObservationRetainsIntentWithoutRetryOrFalseRestoration()
    {
        var first = GameSessionSettings.Advance(Capture(), Observe(2), true);
        var dispatched = GameSessionSettings.RecordDispatch(first.Session, first.Proposal!.Id);
        var unchanged = GameSessionSettings.Advance(dispatched, Observe(3), true);
        Assert.Equal(GameSettingsStatus.Uncertain, unchanged.Status);
        var repeated = GameSessionSettings.Advance(unchanged.Session, Observe(4), true);
        Assert.Equal(GameSettingsStatus.Uncertain, repeated.Status);
        Assert.Null(repeated.Proposal);
        Assert.Equal(first.Proposal, repeated.Session.Pending);
        Assert.Equal(dispatched.DispatchedAfterRevision, repeated.Session.DispatchedAfterRevision);
        Assert.Throws<InvalidOperationException>(() => GameSessionSettings.RecordDispatch(repeated.Session, first.Proposal.Id));
        var ended = GameSessionSettings.Advance(GameSessionSettings.RequestEnd(repeated.Session), Observe(5), true);
        Assert.Equal(GameSettingsStatus.Uncertain, ended.Status);
        Assert.NotNull(ended.Session.Pending);
        Assert.Null(ended.Proposal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DelayedApplyIsReconciledAndRestoredEvenWhenCancellationPrecedesItsObservation(bool cancelBeforeUnchangedFrame)
    {
        var proposed = GameSessionSettings.Advance(Capture(), Observe(2), true);
        var dispatched = GameSessionSettings.RecordDispatch(proposed.Session, proposed.Proposal!.Id);
        var awaiting = cancelBeforeUnchangedFrame ? GameSessionSettings.RequestEnd(dispatched) : dispatched;
        var unchanged = GameSessionSettings.Advance(awaiting, Observe(3), true);
        Assert.Equal(GameSettingsStatus.Uncertain, unchanged.Status);
        Assert.False(unchanged.Session.Entries[0].Applied);

        var ending = GameSessionSettings.RequestEnd(unchanged.Session);
        var lateChangeDuringTakeover = GameSessionSettings.Advance(ending, Observe(4, "Huge"), false);
        Assert.Equal(GameSettingsStatus.Paused, lateChangeDuringTakeover.Status);
        Assert.True(lateChangeDuringTakeover.Session.Entries[0].Applied);
        Assert.False(lateChangeDuringTakeover.Session.Entries[0].Restored);
        Assert.Null(lateChangeDuringTakeover.Proposal);

        var restore = GameSessionSettings.Advance(lateChangeDuringTakeover.Session, Observe(5, "Huge"), true);
        Assert.Equal(GameSettingChangeKind.Restore, restore.Proposal!.Kind);
        Assert.Equal("Tiny", restore.Proposal.AfterValue);
        var restoring = GameSessionSettings.RecordDispatch(restore.Session, restore.Proposal.Id);
        var restored = GameSessionSettings.Advance(restoring, Observe(6), true);
        Assert.Equal(GameSettingsStatus.Restored, restored.Status);
        Assert.True(restored.Session.Entries[0].Restored);
    }

    [Fact]
    public void DelayedApplyCanBecomeReadyBeforeCancellation()
    {
        var proposed = GameSessionSettings.Advance(Capture(), Observe(2), true);
        var dispatched = GameSessionSettings.RecordDispatch(proposed.Session, proposed.Proposal!.Id);
        var unchanged = GameSessionSettings.Advance(dispatched, Observe(3), true);
        var applied = GameSessionSettings.Advance(unchanged.Session, Observe(4, "Huge"), true);
        Assert.Equal(GameSettingsStatus.Ready, applied.Status);
        Assert.True(applied.Session.Entries[0].Applied);
        Assert.Null(applied.Session.Pending);
        Assert.Null(applied.Proposal);
    }

    [Fact]
    public void ManualThirdValueLeavesConflictAndIsNeverReplaced()
    {
        var end = GameSessionSettings.RequestEnd(Applied());
        var conflict = GameSessionSettings.Advance(end, Observe(4, "Medium"), true);
        Assert.Equal(GameSettingsStatus.Conflict, conflict.Status);
        Assert.Null(conflict.Proposal);
        Assert.False(conflict.Session.Entries[0].Restored);
        var restoredByUser = GameSessionSettings.Advance(conflict.Session, Observe(5), true);
        Assert.Equal(GameSettingsStatus.Restored, restoredByUser.Status);
        Assert.Null(restoredByUser.Proposal);
    }

    [Fact]
    public void ConflictAfterDispatchRetainsUncertainIntentUntilResolved()
    {
        var first = GameSessionSettings.Advance(Capture(), Observe(2), true);
        var dispatched = GameSessionSettings.RecordDispatch(first.Session, first.Proposal!.Id);
        var conflict = GameSessionSettings.Advance(dispatched, Observe(3, "Medium"), true);
        Assert.Equal(GameSettingsStatus.Conflict, conflict.Status);
        Assert.NotNull(conflict.Session.DispatchedAfterRevision);
        var pendingEnd = GameSessionSettings.RequestEnd(conflict.Session);
        var confirmed = GameSessionSettings.Advance(pendingEnd, Observe(4, "Huge"), false);
        Assert.True(confirmed.Session.Entries[0].Applied);
        Assert.Equal(GameSettingsStatus.Paused, confirmed.Status);
        Assert.Null(confirmed.Proposal);
    }

    [Fact]
    public void TakeoverBlocksApplyAndRestoreDispatch()
    {
        var first = GameSessionSettings.Advance(Capture(), Observe(2), true);
        var takeover = GameSessionSettings.Advance(first.Session, Observe(3), false);
        Assert.Equal(GameSettingsStatus.Paused, takeover.Status);
        Assert.Null(takeover.Proposal);
        Assert.Throws<InvalidOperationException>(() => GameSessionSettings.RecordDispatch(takeover.Session, first.Proposal!.Id));
        var restore = GameSessionSettings.Advance(GameSessionSettings.RequestEnd(Applied()), Observe(4, "Huge"), false);
        Assert.Equal(GameSettingsStatus.Paused, restore.Status);
        Assert.Null(restore.Proposal);
        Assert.False(restore.Session.Entries[0].Restored);
    }

    [Fact]
    public void EndingBeforeDispatchInvalidatesTheApplyProposal()
    {
        var first = GameSessionSettings.Advance(Capture(), Observe(2), true);
        var cancelled = GameSessionSettings.RequestEnd(first.Session);
        Assert.Null(cancelled.Pending);
        Assert.Throws<InvalidOperationException>(() => GameSessionSettings.RecordDispatch(cancelled, first.Proposal!.Id));
        Assert.Equal(GameSettingsStatus.Restored, GameSessionSettings.Advance(cancelled, Observe(3), false).Status);
    }

    [Fact]
    public void ManualRestoreOfAnUndispatchedRestoreProposalNeedsNoClick()
    {
        var restore = GameSessionSettings.Advance(GameSessionSettings.RequestEnd(Applied()), Observe(4, "Huge"), true);
        var restored = GameSessionSettings.Advance(restore.Session, Observe(5), false);
        Assert.Equal(GameSettingsStatus.Restored, restored.Status);
        Assert.Null(restored.Proposal);
        Assert.Null(restored.Session.Pending);
    }

    [Fact]
    public void UnchangedRestoreRemainsUncertainWithoutAutomaticRetry()
    {
        var restore = GameSessionSettings.Advance(GameSessionSettings.RequestEnd(Applied()), Observe(4, "Huge"), true);
        var dispatched = GameSessionSettings.RecordDispatch(restore.Session, restore.Proposal!.Id);
        var unchanged = GameSessionSettings.Advance(dispatched, Observe(5, "Huge"), true);
        Assert.Equal(GameSettingsStatus.Uncertain, unchanged.Status);
        Assert.NotNull(unchanged.Session.Pending);
        var repeated = GameSessionSettings.Advance(unchanged.Session, Observe(6, "Huge"), true);
        Assert.Equal(GameSettingsStatus.Uncertain, repeated.Status);
        Assert.Null(repeated.Proposal);
        Assert.Throws<InvalidOperationException>(() => GameSessionSettings.RecordDispatch(repeated.Session, restore.Proposal.Id));
        Assert.Equal(GameSettingsStatus.Restored, GameSessionSettings.Advance(repeated.Session, Observe(7), true).Status);
    }

    [Fact]
    public void RestoresOnlyChangedSettingsInReverseApplicationOrder()
    {
        var session = Capture(new("ui-scale", "Huge"), new("resolution", "1920x1080"), new("window-mode", "Borderless"));
        var applyScale = GameSessionSettings.Advance(session, Observe(2), true);
        var applyResolution = GameSessionSettings.Advance(GameSessionSettings.RecordDispatch(applyScale.Session, applyScale.Proposal!.Id), Observe(3, "Huge"), true);
        var applyWindow = GameSessionSettings.Advance(GameSessionSettings.RecordDispatch(applyResolution.Session, applyResolution.Proposal!.Id), Observe(4, "Huge", "1920x1080"), true);
        var ready = GameSessionSettings.Advance(GameSessionSettings.RecordDispatch(applyWindow.Session, applyWindow.Proposal!.Id), Observe(5, "Huge", "1920x1080", "Borderless"), true);
        Assert.Equal(GameSettingsStatus.Ready, ready.Status);
        var restoreWindow = GameSessionSettings.Advance(GameSessionSettings.RequestEnd(ready.Session), Observe(6, "Huge", "1920x1080", "Borderless"), true);
        Assert.Equal("window-mode", restoreWindow.Proposal!.Key);
        var restoreResolution = GameSessionSettings.Advance(GameSessionSettings.RecordDispatch(restoreWindow.Session, restoreWindow.Proposal.Id), Observe(7, "Huge", "1920x1080"), true);
        Assert.Equal("resolution", restoreResolution.Proposal!.Key);
        var restoreScale = GameSessionSettings.Advance(GameSessionSettings.RecordDispatch(restoreResolution.Session, restoreResolution.Proposal.Id), Observe(8, "Huge"), true);
        Assert.Equal("ui-scale", restoreScale.Proposal!.Key);
        var restored = GameSessionSettings.Advance(GameSessionSettings.RecordDispatch(restoreScale.Session, restoreScale.Proposal.Id), Observe(9), true);
        Assert.Equal(GameSettingsStatus.Restored, restored.Status);
        Assert.All(restored.Session.Entries, entry => Assert.True(entry.Restored));
    }
}
