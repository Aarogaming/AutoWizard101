using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

/// <summary>Synthetic scheduler fixtures only: no client, window or input dispatcher.</summary>
public sealed class W101PathArrivalIntegrationTests
{
    private static NavigationRoute Portal() => new(true, "synthetic-qualified", "portal",
        new("commons", "instance", "variant", "hash", "pose", "input", "profile"),
        [new(0, 0, 0, 20), new(100, 0, 0, 20)], null,
        NavigationArrivalKind.ZoneTransition, "pavilion");

    private static NavigationArrivalResult Verified() =>
        new(NavigationArrivalState.Verified, "synthetic-arrival", true, 100, 150, "after");

    private sealed class Probe
    {
        internal double Now;
        internal int Reads, Applies, Releases, Verifications;
        internal bool Owned = true, Released;
        internal Func<int, double, PathObservation> Observe = (_, time) => new(0, 0, 0, 0, time, "commons", "before", Loading: true);
        internal Func<NavigationRoute, CancellationToken, Task<NavigationArrivalResult>> Verify = (_, _) => Task.FromResult(Verified());

        internal W101PathRuntime Runtime() => new(() => Owned, () => Now,
            _ => { Now += 50; return Task.FromResult(Observe(++Reads, Now)); },
            (motion, _) =>
            {
                Applies++;
                Now += 10;
                return Task.FromResult(new PathMotionReceipt(motion.SequenceId, true, Now, motion.IssuedMs, null, Now));
            },
            _ => { Releases++; Released = true; return Task.FromResult(true); },
            (_, _) => throw new InvalidOperationException("Portal must not call the prompt verifier."), true,
            (route, token) => { Verifications++; return Verify(route, token); });
    }

    private static async Task<OperationReceipt> RunAsync(NavigationRoute route, Probe probe, CancellationToken cancellation = default,
        Action<OperationContext>? inspectOwner = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "nav-arrival-integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var scheduler = new OperationScheduler(Path.Combine(root, "runs"), Path.Combine(root, "locks"));
            var request = new OperationRequest(Guid.NewGuid().ToString("N"), "navigation-arrival-test", "1", 10,
                true, "pid-123-start-456", new Dictionary<string, string>());
            var ticket = scheduler.Submit(request, async (owner, token) =>
            {
                inspectOwner?.Invoke(owner);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellation);
                return await W101PathSession.RunOwnedAsync(owner, route, new("commons", "before"), probe.Runtime(), linked.Token);
            });
            return await ticket.Completion;
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(999, "pavilion")]
    [InlineData((int)NavigationArrivalKind.ZoneTransition, null)]
    [InlineData((int)NavigationArrivalKind.ZoneTransition, " ")]
    [InlineData((int)NavigationArrivalKind.Coordinate, "different-zone")]
    public async Task InvalidArrivalMetadataRejectsBeforeReadingOrMovement(int kind, string? destination)
    {
        var probe = new Probe();
        var result = await RunAsync(Portal() with { ArrivalKind = (NavigationArrivalKind)kind, DestinationZone = destination }, probe);
        Assert.Equal(OperationState.Failed, result.State);
        Assert.Equal("navigation-prerequisites-unqualified", result.Message);
        Assert.Equal(0, probe.Reads);
        Assert.Equal(0, probe.Applies);
        Assert.Equal(0, probe.Verifications);
    }

    [Theory]
    [InlineData(true, true, 0)]
    [InlineData(false, false, 0)]
    [InlineData(false, true, 200)]
    public async Task StoppedOrStaleLoadingNeverInvokesArrival(bool stopped, bool fresh, double age)
    {
        var probe = new Probe
        {
            Now = 250,
            Observe = (_, time) => new(0, 0, 0, 0, time - age, "commons", "before", Fresh: fresh, Loading: true, Stopped: stopped)
        };
        var result = await RunAsync(Portal(), probe);
        Assert.Equal(OperationState.Failed, result.State);
        Assert.Equal(0, probe.Verifications);
        Assert.Equal(0, probe.Applies);
        Assert.Equal(1, probe.Releases);
    }

    [Fact]
    public async Task LegitimateLoadingHandsOffOnlyAfterRelease()
    {
        var probe = new Probe
        {
            Observe = (index, time) => new(0, 0, 0, 0, time, "commons", "before", Loading: index > 1)
        };
        probe.Verify = (route, _) =>
        {
            Assert.True(probe.Released);
            Assert.Equal(1, probe.Releases);
            Assert.Equal("pavilion", route.DestinationZone);
            return Task.FromResult(Verified());
        };
        var result = await RunAsync(Portal(), probe);
        Assert.Equal(OperationState.Completed, result.State);
        Assert.Equal(1, probe.Applies);
        Assert.Equal(1, probe.Verifications);
        Assert.Equal(2, probe.Reads);
    }

    [Fact]
    public async Task OwnerChangeDuringSuccessfulCallbackCannotComplete()
    {
        var probe = new Probe();
        probe.Verify = async (_, _) =>
        {
            await Task.Yield();
            probe.Owned = false;
            return Verified();
        };
        var result = await RunAsync(Portal(), probe);
        Assert.Equal(OperationState.Failed, result.State);
        Assert.Equal("typed-arrival-unverified-or-owner-changed", result.Message);
        Assert.Equal(1, probe.Verifications);
        Assert.True(probe.Released);
    }

    [Fact]
    public async Task CancellationJoinsArrivalCleanupBeforeReleasingLease()
    {
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool cleaned = false, ownedDuringCleanup = false;
        OperationContext? capturedOwner = null;
        var probe = new Probe();
        probe.Verify = async (_, token) =>
        {
            entered.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally
            {
                cleanupStarted.SetResult();
                await allowCleanup.Task;
                ownedDuringCleanup = capturedOwner?.Input is { CanDispatch: true } &&
                    ReferenceEquals(capturedOwner.Input, GameInputLease.Current);
                cleaned = true;
            }
            return Verified();
        };
        var execution = RunAsync(Portal(), probe, cancellation.Token, owner => capturedOwner = owner);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();
            await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(execution.IsCompleted);
            allowCleanup.SetResult();
            var result = await execution.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(OperationState.Cancelled, result.State);
            Assert.True(cleaned);
            Assert.True(ownedDuringCleanup);
            Assert.Equal(1, probe.Releases);
        }
        finally
        {
            cancellation.Cancel();
            allowCleanup.TrySetResult();
            await execution;
        }
    }
}
