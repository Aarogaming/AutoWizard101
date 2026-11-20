using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class GameInputRecoveryTransactionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-recovery-transaction-" + Guid.NewGuid().ToString("N"));
    private long _clock = 100;
    private string LockPath => Path.Combine(_root, "game-input.lock");
    private string NoticePath => Path.Combine(_root, "input-recovery-required.txt");
    private InputRecoveryRuntime Runtime(Action<string, byte[]>? publish = null, Action<FileStream, byte>? persist = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null) => new(() => _clock,
            delay ?? ((duration, _) => { _clock += (long)duration.TotalMilliseconds; return Task.CompletedTask; }),
            persist ?? GameInputLease.PersistState, publish ?? GameInputRecovery.Publish);
    private InputRecoverySample Good(InputRecoveryChallenge challenge) => new(challenge.Id, _clock, 7, true, true, true, true);
    private Task<InputRecoverySample> Verify(InputRecoveryChallenge challenge, CancellationToken _) => Task.FromResult(Good(challenge));
    private void Seed(string state = "1", bool notice = true)
    {
        Directory.CreateDirectory(_root); File.WriteAllText(LockPath, state);
        if (notice) File.WriteAllText(NoticePath, "Original uncertain cleanup, preserved verbatim.");
    }

    [Fact]
    public async Task VerifiedQuietCleanupPreservesEvidenceAndEnablesOnlyNewExplicitOwnership()
    {
        Seed(); var original = File.ReadAllBytes(NoticePath); var calls = 0;
        var result = await GameInputRecovery.RunAsync(_root, (challenge, token) =>
        {
            calls++; Assert.Null(GameInputLease.Current);
            Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competing-gameplay", _root));
            return Verify(challenge, token);
        }, CancellationToken.None, Runtime());
        Assert.True(result.Recovered, result.Message); Assert.Equal("recovered", result.Code); Assert.Equal(2, calls);
        Assert.Equal("0", File.ReadAllText(LockPath)); Assert.False(Path.Exists(NoticePath));
        Assert.NotNull(result.EvidenceDirectory);
        Assert.Equal(new[] { (byte)'1' }, File.ReadAllBytes(Path.Combine(result.EvidenceDirectory!, "original-guard.bin")));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(result.EvidenceDirectory!, "original-warning.txt")));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(result.EvidenceDirectory!, "archived-warning.txt")));
        using var proof = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.EvidenceDirectory!, "verification.json")));
        Assert.Equal("verified-for-commit", proof.RootElement.GetProperty("status").GetString());
        using var next = GameInputLease.Acquire("explicit-next-run", _root); Assert.True(next.CanDispatch);
    }

    [Fact]
    public async Task BusyOwnerIsNeverStoppedOrTakenOverByRecovery()
    {
        using var owner = GameInputLease.Acquire("active-owner", _root); int calls = 0;
        var result = await GameInputRecovery.RunAsync(_root, (challenge, token) =>
        { calls++; return Verify(challenge, token); }, CancellationToken.None, Runtime());
        Assert.False(result.Recovered); Assert.Equal(0, calls); Assert.Null(result.EvidenceDirectory);
        Assert.True(owner.CanDispatch); Assert.True(owner.IsHeld);
    }

    [Theory]
    [InlineData("workers")]
    [InlineData("desktop")]
    [InlineData("unknown-input")]
    [InlineData("held-input")]
    [InlineData("wrong-challenge")]
    [InlineData("future-time")]
    [InlineData("old-time")]
    [InlineData("negative-revision")]
    public async Task IncompleteOrUnboundProofNeverClearsGuard(string failure)
    {
        Seed();
        var result = await GameInputRecovery.RunAsync(_root, (challenge, _) =>
        {
            var sample = Good(challenge);
            return Task.FromResult(failure switch
            {
                "workers" => sample with { WorkersStopped = false },
                "desktop" => sample with { DesktopAvailable = false },
                "unknown-input" => sample with { InputStateKnown = false },
                "held-input" => sample with { InputsReleased = false },
                "wrong-challenge" => sample with { ChallengeId = "another-attempt" },
                "future-time" => sample with { ObservedAtMilliseconds = _clock + 1 },
                "old-time" => sample with { ObservedAtMilliseconds = _clock - 1 },
                _ => sample with { ActivityRevision = -1 }
            });
        }, CancellationToken.None, Runtime());
        Assert.False(result.Recovered); Assert.Equal("1", File.ReadAllText(LockPath));
        Assert.True(File.Exists(NoticePath)); Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("blocked", _root));
    }

    [Theory]
    [InlineData("activity")]
    [InlineData("short-interval")]
    [InlineData("backward-clock")]
    [InlineData("expired-clock")]
    public async Task QuietIntervalRequiresStableActivityAndMonotonicBoundedTime(string failure)
    {
        Seed(); int calls = 0;
        var runtime = Runtime(delay: (duration, _) =>
        {
            _clock += failure switch { "short-interval" => 999, "backward-clock" => -1, "expired-clock" => 10001, _ => (long)duration.TotalMilliseconds };
            return Task.CompletedTask;
        });
        var result = await GameInputRecovery.RunAsync(_root, (challenge, _) =>
        {
            var sample = Good(challenge);
            if (++calls == 2 && failure == "activity") sample = sample with { ActivityRevision = 8 };
            return Task.FromResult(sample);
        }, CancellationToken.None, runtime);
        Assert.False(result.Recovered); Assert.Equal("1", File.ReadAllText(LockPath));
    }

    [Fact]
    public async Task FinalProofCannotAgeWhileEvidenceIsWritten()
    {
        Seed();
        var result = await GameInputRecovery.RunAsync(_root, Verify, CancellationToken.None, Runtime((path, bytes) =>
        {
            GameInputRecovery.Publish(path, bytes);
            if (Path.GetFileName(path) == "verification.json") _clock += 101;
        }));
        Assert.False(result.Recovered); Assert.Equal("1", File.ReadAllText(LockPath)); Assert.True(File.Exists(NoticePath));
    }

    [Fact]
    public async Task MissingWarningDoesNotPreventVerifiedRecoveryOfDirtyGuard()
    {
        Seed(notice: false);
        var result = await GameInputRecovery.RunAsync(_root, Verify, CancellationToken.None, Runtime());
        Assert.True(result.Recovered, result.Message); Assert.Equal("0", File.ReadAllText(LockPath));
        Assert.False(File.Exists(Path.Combine(result.EvidenceDirectory!, "original-warning.txt")));
        using var next = GameInputLease.Acquire("next", _root); Assert.True(next.CanDispatch);
    }

    [Fact]
    public async Task RecoveryClearsOnlyExactCurrentLocalGeneration()
    {
        var former = GameInputLease.Acquire("former-owner", _root);
        former.RequireRecovery("Original report."); former.Dispose();
        int calls = 0;
        var result = await GameInputRecovery.RunAsync(_root, (challenge, _) =>
        {
            if (++calls == 2) Assert.Throws<ObjectDisposedException>(() => former.RequireRecovery("A newly discovered cleanup problem."));
            return Task.FromResult(Good(challenge));
        }, CancellationToken.None, Runtime());
        Assert.False(result.Recovered); Assert.Equal("1", File.ReadAllText(LockPath));
        Assert.Contains("newly discovered", File.ReadAllText(NoticePath));
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("blocked", _root));
    }

    [Fact]
    public async Task SuccessfulRecoveryDoesNotReviveTheOldPoisonedLease()
    {
        var former = GameInputLease.Acquire("former-owner", _root);
        former.RequireRecovery("Original report."); former.Dispose();
        var result = await GameInputRecovery.RunAsync(_root, Verify, CancellationToken.None, Runtime());
        Assert.True(result.Recovered, result.Message); Assert.True(former.RecoveryRequired); Assert.False(former.CanDispatch);
        using (GameInputLease.EnterScope(former)) Assert.Null(GameInputLease.Current);
        using var next = GameInputLease.Acquire("explicit-next-run", _root); Assert.True(next.CanDispatch);
    }

    [Fact]
    public async Task NewWarningAfterSamplesAbortsInsteadOfDiscardingItsEvidence()
    {
        Seed();
        var result = await GameInputRecovery.RunAsync(_root, Verify, CancellationToken.None, Runtime((path, bytes) =>
        {
            GameInputRecovery.Publish(path, bytes);
            if (Path.GetFileName(path) == "verification.json") File.WriteAllText(NoticePath, "New warning.");
        }));
        Assert.False(result.Recovered); Assert.Equal("1", File.ReadAllText(LockPath));
        Assert.Equal("New warning.", File.ReadAllText(NoticePath));
        Assert.Contains("Original uncertain", File.ReadAllText(Path.Combine(result.EvidenceDirectory!, "original-warning.txt")));
    }

    [Fact]
    public async Task CancellationAfterVerificationNeverCommitsClean()
    {
        Seed(); using var cancel = new CancellationTokenSource();
        var result = await GameInputRecovery.RunAsync(_root, Verify, cancel.Token, Runtime((path, bytes) =>
        {
            GameInputRecovery.Publish(path, bytes);
            if (Path.GetFileName(path) == "verification.json") cancel.Cancel();
        }));
        Assert.False(result.Recovered); Assert.Equal("cancelled", result.Code);
        Assert.Equal("1", File.ReadAllText(LockPath)); Assert.True(File.Exists(NoticePath));
    }

    [Theory]
    [InlineData("original-guard.bin")]
    [InlineData("attempt.json")]
    [InlineData("verification.json")]
    public async Task UnwritableEvidenceCannotEnableControls(string filename)
    {
        Seed();
        var result = await GameInputRecovery.RunAsync(_root, Verify, CancellationToken.None, Runtime((path, bytes) =>
        {
            if (Path.GetFileName(path) == filename) throw new IOException("Injected evidence failure.");
            GameInputRecovery.Publish(path, bytes);
        }));
        Assert.False(result.Recovered); Assert.Equal("1", File.ReadAllText(LockPath)); Assert.True(File.Exists(NoticePath));
    }

    [Fact]
    public async Task CleanWriteFailureRetainsOriginalWarningInArchiveAndRearmsDirty()
    {
        Seed();
        var result = await GameInputRecovery.RunAsync(_root, Verify, CancellationToken.None, Runtime(persist: (stream, state) =>
        {
            GameInputLease.PersistState(stream, state);
            if (state == (byte)'0') throw new IOException("Injected clean flush acknowledgment failure.");
        }));
        Assert.False(result.Recovered); Assert.Equal("1", File.ReadAllText(LockPath));
        Assert.Contains("Original uncertain", File.ReadAllText(Path.Combine(result.EvidenceDirectory!, "archived-warning.txt")));
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("blocked", _root));
    }

    [Fact]
    public async Task NewRecoveryReportDuringCleanCommitWinsOverOlderProof()
    {
        var former = GameInputLease.Acquire("former", _root); former.RequireRecovery("Original."); former.Dispose();
        var result = await GameInputRecovery.RunAsync(_root, Verify, CancellationToken.None, Runtime(persist: (stream, state) =>
        {
            GameInputLease.PersistState(stream, state);
            if (state == (byte)'0') Assert.Throws<ObjectDisposedException>(() => former.RequireRecovery("New cleanup problem during commit."));
        }));
        Assert.False(result.Recovered); Assert.Equal("1", File.ReadAllText(LockPath));
        Assert.Contains("during commit", File.ReadAllText(NoticePath));
    }

    [Fact]
    public async Task VerificationCannotBeReplayedForAnotherRecoveryChallenge()
    {
        Seed(); InputRecoverySample? saved = null;
        var first = await GameInputRecovery.RunAsync(_root, (challenge, _) =>
        { saved = Good(challenge); return Task.FromResult(saved!); }, CancellationToken.None, Runtime());
        Assert.True(first.Recovered, first.Message);
        var second = await GameInputRecovery.RunAsync(_root, (_, _) => Task.FromResult(saved!), CancellationToken.None, Runtime());
        Assert.False(second.Recovered); Assert.Equal("1", File.ReadAllText(LockPath));
        Assert.NotEqual(first.EvidenceDirectory, second.EvidenceDirectory);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("00")]
    public async Task UnknownGuardContentsArePreservedForReview(string state)
    {
        Seed(state); int calls = 0;
        var result = await GameInputRecovery.RunAsync(_root, (challenge, token) =>
        { calls++; return Verify(challenge, token); }, CancellationToken.None, Runtime());
        Assert.False(result.Recovered); Assert.Equal(0, calls); Assert.Equal(state, File.ReadAllText(LockPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonregularOrOversizedWarningNeverBecomesAnEmptyProof(bool oversized)
    {
        Seed(notice: false);
        if (oversized) File.WriteAllBytes(NoticePath, new byte[65537]);
        else Directory.CreateDirectory(NoticePath);
        var result = await GameInputRecovery.RunAsync(_root, Verify, CancellationToken.None, Runtime());
        Assert.False(result.Recovered); Assert.Equal("1", File.ReadAllText(LockPath));
        Assert.True(Path.Exists(NoticePath));
    }

    [Fact]
    public async Task ExistingImmutableEvidenceIsNotOverwritten()
    {
        Seed(); string? occupied = null;
        var result = await GameInputRecovery.RunAsync(_root, Verify, CancellationToken.None, Runtime((path, bytes) =>
        {
            if (Path.GetFileName(path) == "verification.json") { occupied = path; File.WriteAllText(path, "existing evidence"); }
            GameInputRecovery.Publish(path, bytes);
        }));
        Assert.False(result.Recovered); Assert.NotNull(occupied); Assert.Equal("existing evidence", File.ReadAllText(occupied!));
        Assert.Equal("1", File.ReadAllText(LockPath));
    }

    [Fact]
    public async Task FailedInitialArmUsesReservationRollbackBeforeReleasingLock()
    {
        Seed("0", notice: false); int writes = 0, samples = 0;
        var result = await GameInputRecovery.RunAsync(_root, (challenge, token) =>
        { samples++; return Verify(challenge, token); }, CancellationToken.None, Runtime(persist: (stream, state) =>
        {
            Assert.Equal((byte)'1', state);
            if (++writes == 1) throw new IOException("Injected initial arm failure.");
            GameInputLease.PersistState(stream, state);
        }));
        Assert.False(result.Recovered); Assert.Equal(0, samples); Assert.Equal(2, writes);
        Assert.Equal("1", File.ReadAllText(LockPath)); Assert.Null(result.EvidenceDirectory);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("blocked", _root));
    }

    [Fact]
    public async Task HandleCloseFailureAfterUnlockCannotRewriteNewOwnersState()
    {
        Seed(); var writes = new List<byte>(); GameInputLease? next = null;
        var runtime = Runtime(persist: (stream, state) =>
        { writes.Add(state); GameInputLease.PersistState(stream, state); }) with
        {
            Close = stream =>
            {
                next = GameInputLease.Acquire("new-owner-after-commit", _root);
                stream.Dispose();
                throw new IOException(new string('x', 10000));
            }
        };
        try
        {
            var result = await GameInputRecovery.RunAsync(_root, Verify, CancellationToken.None, runtime);
            Assert.True(result.Recovered); Assert.Equal("recovered-with-close-warning", result.Code);
            Assert.Contains("diagnostic truncated", result.Message); Assert.InRange(result.Message.Length, 1, 2200);
            Assert.NotNull(next); Assert.True(next!.CanDispatch); Assert.False(next.RecoveryRequired);
            Assert.Equal(new[] { (byte)'1', (byte)'0' }, writes);
        }
        finally { next?.Dispose(); }
        Assert.Equal("0", File.ReadAllText(LockPath));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
