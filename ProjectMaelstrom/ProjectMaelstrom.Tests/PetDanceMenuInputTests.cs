using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetDanceMenuInputTests
{
    [Fact]
    public async Task CursorSettlesBeforeDownAndMouseHoldsForTwoBoundedPhases()
    {
        var events = new List<string>(); var elapsed = 0;
        Task Delay(int milliseconds, CancellationToken token) { elapsed += milliseconds; return Task.CompletedTask; }
        var receipt = await PetDanceMenuInput.DispatchSequenceAsync(() => events.Add("move:" + elapsed),
            () => events.Add("down:" + elapsed), () => events.Add("up:" + elapsed), () => true, () => true,
            80, Delay, default, () => throw new InvalidOperationException());
        Assert.Equal(new[] { "move:0", "down:80", "up:160" }, events);
        Assert.True(receipt.Accepted); Assert.True(receipt.InputReleased);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitContinuingGateAllowsNaturalEvidenceAgingOnlyWithinAdmittedAction(bool split)
    {
        var elapsed = 0; var down = 0; var up = 0;
        bool Ready() => elapsed < 40;
        Task Delay(int milliseconds, CancellationToken token) { elapsed += milliseconds; return Task.CompletedTask; }
        var receipt = await PetDanceMenuInput.DispatchSequenceAsync(() => { }, () => down++, () => up++, Ready,
            split ? () => true : Ready, 80, Delay, default, () => throw new InvalidOperationException());
        Assert.Equal(split, receipt.Accepted);
        Assert.Equal(split ? 1 : 0, down); Assert.Equal(down, up);
    }

    [Theory]
    [InlineData(30, false)]
    [InlineData(100, true)]
    public async Task TakeoverDuringSettleOrHoldStopsAndReleasesOnlyAttemptedDown(int takeoverAt, bool pressed)
    {
        var elapsed = 0; var down = 0; var up = 0;
        Task Delay(int milliseconds, CancellationToken token) { elapsed += milliseconds; return Task.CompletedTask; }
        var receipt = await PetDanceMenuInput.DispatchSequenceAsync(() => { }, () => down++, () => up++, () => true,
            () => elapsed < takeoverAt, 80, Delay, default, () => throw new InvalidOperationException());
        Assert.False(receipt.Accepted); Assert.True(receipt.InputReleased);
        Assert.Equal(pressed ? 1 : 0, down); Assert.Equal(down, up);
        Assert.Equal(takeoverAt, elapsed);
    }

    [Fact]
    public async Task CancellationAfterDownStillReleases()
    {
        using var cancellation = new CancellationTokenSource(); var up = 0;
        Task Delay(int milliseconds, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        var receipt = await PetDanceMenuInput.DispatchSequenceAsync(null, () => cancellation.Cancel(), () => up++,
            () => true, () => true, 40, Delay, cancellation.Token, () => throw new InvalidOperationException());
        Assert.False(receipt.Accepted); Assert.True(receipt.InputReleased); Assert.Equal(1, up);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedDownStillReleasesAndFailedReleaseLatchesRecovery(bool releaseFails)
    {
        var releases = 0; var recovery = 0;
        var receipt = await PetDanceMenuInput.DispatchSequenceAsync(null, () => throw new IOException("down failed"),
            () => { releases++; if (releaseFails) throw new IOException("up failed"); }, () => true, () => true,
            40, (_, _) => Task.CompletedTask, default, () => recovery++);
        Assert.False(receipt.Accepted); Assert.Equal(!releaseFails, receipt.InputReleased);
        Assert.Equal(releaseFails ? 2 : 1, releases); Assert.Equal(releaseFails ? 1 : 0, recovery);
    }

    [Fact]
    public async Task KeyboardKeepsFortyMillisecondHoldWithoutCursorSettle()
    {
        var elapsed = 0;
        var receipt = await PetDanceMenuInput.DispatchSequenceAsync(null, () => Assert.Equal(0, elapsed),
            () => Assert.Equal(40, elapsed), () => true, () => true, 40,
            (milliseconds, _) => { elapsed += milliseconds; return Task.CompletedTask; }, default,
            () => throw new InvalidOperationException());
        Assert.True(receipt.Accepted); Assert.True(receipt.InputReleased);
    }

    [Fact]
    public void MappingUsesObservedWindowBoundsAndSupportsNegativeMonitorCoordinates()
    {
        var point = PetDanceMenuInput.MapPoint(new(-1500, 50, 1282, 760), new(-1499, 89, 1280, 720),
            new(1282, 760), new(630, 550, 40, 20));
        Assert.Equal(new Point(-850, 610), point);
    }

    [Fact]
    public void ChangedGeometryOrTitleBarControlsCannotBeClicked()
    {
        var capture = new Rectangle(0, 0, 1282, 760); var client = new Rectangle(1, 39, 1280, 720);
        Assert.Null(PetDanceMenuInput.MapPoint(capture, client, new(1282, 759), new(600, 600, 20, 20)));
        Assert.Null(PetDanceMenuInput.MapPoint(capture, client, capture.Size, new(600, 4, 20, 20)));
        Assert.Null(PetDanceMenuInput.MapPoint(capture, client, capture.Size, new(float.NaN, 400, 20, 20)));
    }

    [Fact]
    public void MenuControlsNeedStableBoundsAcrossObservations()
    {
        Assert.True(PetDanceNativeIdle.BoundsStable(new(400, 500, 50, 20), new(402, 501, 51, 19)));
        Assert.False(PetDanceNativeIdle.BoundsStable(new(400, 500, 50, 20), new(600, 501, 51, 19)));
        Assert.False(PetDanceNativeIdle.BoundsStable(null, new(600, 501, 51, 19)));
    }

    [Theory]
    [InlineData("--arena=0")]
    [InlineData("--arena=Unknown")]
    [InlineData("--arena=")]
    public void UnknownArenaCannotSilentlyUseFirstArena(string option)
        => Assert.Throws<ArgumentException>(() => PetDanceTrialOptions.Parse([option]));
}
