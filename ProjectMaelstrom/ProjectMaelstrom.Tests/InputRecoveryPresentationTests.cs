using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class InputRecoveryPresentationTests
{
    [Theory]
    [InlineData("Initial", false, true, false, false)]
    [InlineData("Initial", true, true, false, false)]
    [InlineData("Stopping", false, false, false, false)]
    [InlineData("Checking", true, false, false, false)]
    [InlineData("Blocked", true, true, true, false)]
    [InlineData("Ready", false, true, true, false)]
    [InlineData("Ready", true, true, true, true)]
    [InlineData("Restored", true, false, false, false)]
    public void RestoreRequiresExplicitReadyPermission(string stage, bool verified,
        bool stop, bool check, bool restore)
    {
        var view = InputRecoveryPresentation.From(new(Enum.Parse<RecoveryFlowStage>(stage), "Friendly reason", verified));
        Assert.Equal(stop, view.CanStop); Assert.Equal(check, view.CanCheck); Assert.Equal(restore, view.CanRestore);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void BusyClosingOrFailedCheckCannotRestore(bool busy, bool closing, bool failed)
    {
        var view = InputRecoveryPresentation.From(new(RecoveryFlowStage.Ready, "Ready", true), busy, closing, failed);
        Assert.False(view.CanRestore); Assert.False(view.CanCheck);
    }

    [Fact]
    public void FriendlyReasonIsVisibleWhileEvidencePathStaysOutOfNormalCopy()
    {
        var view = InputRecoveryPresentation.From(new(RecoveryFlowStage.Blocked,
            "Another routine is still stopping. Check again when it finishes.", false, @"F:\private\recovery\report.json"));
        Assert.Contains("still stopping", view.Message);
        Assert.DoesNotContain("private", view.ToString());
        Assert.DoesNotContain("report.json", view.ToString());
    }

    [Fact]
    public void CompletionDoesNotPromiseAutomaticResume()
    {
        var view = InputRecoveryPresentation.From(new(RecoveryFlowStage.Restored, "", false));
        Assert.Contains("Previous runs remain stopped", view.Message);
        Assert.Contains("Nothing resumes automatically", view.Steps);
    }
}
