using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class VisualTransitionCadenceTests
{
    // Models the observed starvation mechanism: reading releases a buffered frame,
    // the producer supplies its next frame shortly afterward, and that timestamp
    // remains queued until the next read. No real window or capture is involved.
    private sealed class BufferedSource
    {
        private double? _queuedAt;
        public int Reads { get; private set; }
        public double? Read(double now)
        {
            Reads++;
            var captured = _queuedAt is { } produced && produced <= now ? _queuedAt : null;
            if (captured.HasValue || _queuedAt == null) _queuedAt = now + 16;
            return captured;
        }
    }

    [Fact]
    public async Task FastReacquisitionBreaksTheOldFiveHundredMillisecondStarvationLoop()
    {
        static async Task<(int Ocr, List<double> Starts, List<double> Ages, int Reads)> Replay(bool repaired)
        {
            var source = new BufferedSource();
            var cadence = new VisualTransitionCadence();
            var now = 0d; var previous = -1d;
            var starts = new List<double>(); var ages = new List<double>();
            while (now < 3500)
            {
                var captured = source.Read(now);
                var admitted = await PetDancePostGameFrameGate.InspectAsync(captured.HasValue, captured, previous,
                    () => now, () => true, CancellationToken.None);
                var plan = repaired ? cadence.Plan(now, admitted.Accepted) : new VisualTransitionCadencePlan(admitted.Accepted, 500, "old-fixed-delay");
                if (admitted.Accepted)
                {
                    previous = admitted.CapturedMs!.Value;
                    if (plan.Analyze) { starts.Add(now); ages.Add(admitted.AgeMs!.Value); }
                }
                now += plan.DelayMs;
            }
            return (starts.Count, starts, ages, source.Reads);
        }

        var old = await Replay(false);
        var repaired = await Replay(true);
        Assert.Equal(0, old.Ocr);
        Assert.InRange(repaired.Ocr, 6, 7);
        Assert.All(repaired.Ages, age => Assert.InRange(age, 0, 300));
        Assert.All(repaired.Starts.Zip(repaired.Starts.Skip(1), (first, next) => next - first),
            interval => Assert.True(interval >= 500));
        Assert.InRange(repaired.Reads, repaired.Ocr, repaired.Ocr * 2 + 2);
    }

    [Fact]
    public void ContinuouslyFreshFramesCannotExceedTwoAnalysesPerSecond()
    {
        var cadence = new VisualTransitionCadence();
        var starts = new List<double>();
        for (var now = 0; now < 3000; now += 25)
        {
            var plan = cadence.Plan(now, true);
            Assert.InRange(plan.DelayMs, 1, 500);
            if (plan.Analyze) starts.Add(now);
        }
        Assert.Equal(new double[] { 0, 500, 1000, 1500, 2000, 2500 }, starts);
        Assert.Equal(6, cadence.AnalysisCount);
        Assert.True(cadence.RateLimitedCount > 0);
    }

    [Fact]
    public void RepeatedCaptureFailureHasAnAttemptBudgetAndCooldown()
    {
        var cadence = new VisualTransitionCadence();
        var now = 0d;
        for (var i = 0; i < VisualTransitionCadence.MaximumQuickRetries; i++)
        {
            var retry = cadence.Plan(now, false);
            Assert.False(retry.Analyze);
            Assert.Equal(25, retry.DelayMs);
            now += retry.DelayMs;
        }
        var cooldown = cadence.Plan(now, false);
        Assert.Equal("reacquisition-budget-cooldown", cooldown.Reason);
        Assert.Equal(500, cooldown.DelayMs);
        Assert.False(cooldown.Analyze);
        Assert.Equal(1, cadence.CooldownCount);
        Assert.Equal(500, cadence.Plan(now, false).DelayMs);
        now += cooldown.DelayMs;
        Assert.Equal(25, cadence.Plan(now, false).DelayMs);
    }

    [Fact]
    public void SlowCaptureCannotKeepAReacquisitionBurstAlivePastItsTimeBudget()
    {
        var cadence = new VisualTransitionCadence();
        Assert.Equal(25, cadence.Plan(0, false).DelayMs);
        Assert.Equal(25, cadence.Plan(150, false).DelayMs);
        var exhausted = cadence.Plan(240, false);
        Assert.Equal("reacquisition-budget-cooldown", exhausted.Reason);
        Assert.Equal(500, exhausted.DelayMs);
        Assert.Equal(2, cadence.QuickRetryCount);
    }

    [Fact]
    public void LongOutageCannotBecomeAnUnboundedHotPoll()
    {
        var cadence = new VisualTransitionCadence();
        var now = 0d; var reads = 0;
        while (now < 30000)
        {
            var plan = cadence.Plan(now, false);
            Assert.False(plan.Analyze);
            Assert.InRange(plan.DelayMs, 25, 500);
            reads++;
            now += plan.DelayMs;
        }
        Assert.InRange(reads, 1, 400);
        Assert.True(cadence.CooldownCount >= 40);
        Assert.Equal(0, cadence.AnalysisCount);
    }

    [Fact]
    public async Task CancellationDuringRetryPreventsAnotherCaptureOrOcr()
    {
        using var stop = new CancellationTokenSource();
        var cadence = new VisualTransitionCadence();
        var source = new BufferedSource();
        var now = 0d; var ocr = 0;
        async Task Run()
        {
            while (now < 1000)
            {
                stop.Token.ThrowIfCancellationRequested();
                var captured = source.Read(now);
                var admission = await PetDancePostGameFrameGate.InspectAsync(captured.HasValue, captured, -1,
                    () => now, () => true, stop.Token);
                var plan = cadence.Plan(now, admission.Accepted, stop.Token);
                if (plan.Analyze) ocr++;
                stop.Cancel();
                await Task.Delay(plan.DelayMs, stop.Token);
                now += plan.DelayMs;
            }
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(Run);
        Assert.Equal(1, source.Reads);
        Assert.Equal(0, ocr);
        Assert.Throws<OperationCanceledException>(() => cadence.Plan(now, true, stop.Token));
    }

    [Fact]
    public void FreshRecoveryResetsTheBurstWithoutRelaxingFrameAdmission()
    {
        var cadence = new VisualTransitionCadence();
        for (var i = 0; i < 7; i++) cadence.Plan(i * 25, false);
        Assert.True(cadence.Plan(175, true).Analyze);
        Assert.Equal("analysis-rate-limit", cadence.Plan(200, false).Reason);
        Assert.Equal(25, cadence.Plan(675, false).DelayMs);
        Assert.Equal(0, cadence.CooldownCount);
    }

    [Fact]
    public void InvalidOrReversedClockCannotRemoveRateLimits()
    {
        var cadence = new VisualTransitionCadence();
        cadence.Plan(1000, true);
        foreach (var now in new[] { -1d, 999, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => cadence.Plan(now, true));
        Assert.False(cadence.Plan(1000, true).Analyze);
    }
}
