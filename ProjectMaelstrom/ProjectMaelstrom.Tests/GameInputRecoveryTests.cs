using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class GameInputRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-input-recovery-" + Guid.NewGuid().ToString("N"));
    private string LockPath => Path.Combine(_root, "game-input.lock");
    private string NoticePath => Path.Combine(_root, "input-recovery-required.txt");
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    public void CleanOrLegacyOwnershipIsArmedBeforeGrantAndClearedAfterRelease(string initial)
    {
        Seed(initial);
        var states = new List<byte>();
        using (var owner = GameInputLease.AcquireForTesting("native-test", _root, (stream, state) =>
        {
            GameInputLease.PersistState(stream, state);
            stream.Position = 0;
            Assert.Equal(1, stream.Length); Assert.Equal(state, stream.ReadByte());
            states.Add(state);
        }))
        {
            Assert.Equal(new[] { (byte)'1' }, states);
            Assert.True(owner.IsHeld); Assert.True(owner.CanDispatch); Assert.False(owner.RecoveryRequired);
            using (GameInputLease.EnterScope(owner)) Assert.Same(owner, GameInputLease.Current);
            Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("competitor", _root));
        }
        Assert.Equal(new[] { (byte)'1', (byte)'0' }, states);
        Assert.Equal("0", File.ReadAllText(LockPath));
        using var next = GameInputLease.Acquire("next", _root);
        Assert.True(next.CanDispatch);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("x")]
    [InlineData("00")]
    [InlineData("01")]
    [InlineData("\0")]
    public void DirtyOrMalformedStateCannotBeReinterpretedAsClean(string initial)
    {
        Seed(initial);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("blocked", _root));
        Assert.Equal(initial, File.ReadAllText(LockPath));
        Assert.False(Path.Exists(NoticePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingNoticeOrNoticeDirectoryBlocksEvenCleanState(bool directory)
    {
        Seed("0");
        if (directory) Directory.CreateDirectory(NoticePath);
        else File.WriteAllText(NoticePath, "Unconfirmed release.");
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("blocked", _root));
        Assert.Equal("0", File.ReadAllText(LockPath));
    }

    [Fact]
    public void RecoveryLatchPrecedesWarningWriterAndPreservesPhysicalReleaseAuthority()
    {
        GameInputLease? owner = null;
        bool writerCalled = false;
        owner = GameInputLease.AcquireForTesting("native-test", _root, writeWarning: (_, _) =>
        {
            writerCalled = true;
            Assert.NotNull(owner); Assert.True(owner!.RecoveryRequired); Assert.True(owner.IsHeld);
            Assert.False(owner.CanDispatch); Assert.Null(GameInputLease.Current);
            throw new IOException("Injected unavailable warning storage.");
        });
        using (owner)
        using (GameInputLease.EnterScope(owner))
        {
            Assert.Throws<IOException>(() => owner.RequireRecovery("Unconfirmed key release."));
            Assert.True(writerCalled); Assert.True(owner.IsHeld); Assert.False(owner.CanDispatch);
            Assert.Equal("Injected unavailable warning storage.", owner.RecoveryNoticeFailure);
        }
        Assert.False(owner.IsHeld); Assert.True(owner.RecoveryRequired);
        Assert.Equal("1", File.ReadAllText(LockPath)); Assert.False(Path.Exists(NoticePath));
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", _root));
    }

    [Fact]
    public void RemovingFailedWarningObstructionDoesNotRestoreInputAuthority()
    {
        var owner = GameInputLease.Acquire("native-test", _root);
        using (owner)
        {
            Directory.CreateDirectory(NoticePath);
            var error = Record.Exception(() => owner.RequireRecovery("Unconfirmed input release."));
            Assert.True(error is IOException or UnauthorizedAccessException);
            Assert.True(owner.RecoveryRequired); Assert.NotNull(owner.RecoveryNoticeFailure);
            Directory.Delete(NoticePath);
            Assert.False(owner.CanDispatch);
        }
        Assert.Equal("1", File.ReadAllText(LockPath)); Assert.False(Path.Exists(NoticePath));
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", _root));
    }

    [Fact]
    public void SuccessfulNoticeStillCannotBeClearedByOrdinaryDispose()
    {
        var owner = GameInputLease.Acquire("native-test", _root);
        owner.RequireRecovery("Key release requires review.");
        Assert.Null(owner.RecoveryNoticeFailure);
        owner.Dispose(); owner.Dispose();
        Assert.False(owner.IsHeld); Assert.True(owner.RecoveryRequired);
        Assert.Equal("1", File.ReadAllText(LockPath));
        Assert.Contains("Key release requires review.", File.ReadAllText(NoticePath));
        File.Delete(NoticePath);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", _root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedCleanCommitRearmsDirtyBeforeReleasingOwner(bool wroteCleanFirst)
    {
        var states = new List<byte>();
        var owner = GameInputLease.AcquireForTesting("native-test", _root, (stream, state) =>
        {
            states.Add(state);
            if (state == (byte)'0')
            {
                if (wroteCleanFirst) GameInputLease.PersistState(stream, state);
                throw new IOException("Injected clean commit failure.");
            }
            GameInputLease.PersistState(stream, state);
        });
        Assert.Throws<IOException>(owner.Dispose);
        Assert.Equal(new[] { (byte)'1', (byte)'0', (byte)'1' }, states);
        Assert.False(owner.IsHeld); Assert.True(owner.RecoveryRequired); Assert.False(owner.CanDispatch);
        Assert.Equal("1", File.ReadAllText(LockPath));
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", _root));
        owner.Dispose();
    }

    [Fact]
    public void FailedInitialDirtyCommitDoesNotGrantOwnershipAndLeavesRecoveryHold()
    {
        Assert.Throws<IOException>(() => GameInputLease.AcquireForTesting("native-test", _root,
            (_, _) => throw new IOException("Injected dirty commit failure.")));
        Assert.Equal("1", File.ReadAllText(LockPath));
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", _root));
        Assert.Null(GameInputLease.Current);
    }

    [Fact]
    public async Task ConcurrentDisposeWaitsForRecoveryWriterAndNeverClearsPoison()
    {
        using var writerEntered = new ManualResetEventSlim();
        using var writerRelease = new ManualResetEventSlim();
        using var disposeEntered = new ManualResetEventSlim();
        var persisted = new List<byte>();
        var owner = GameInputLease.AcquireForTesting("native-test", _root, (stream, state) =>
        {
            persisted.Add(state); GameInputLease.PersistState(stream, state);
        }, (_, _) =>
        {
            writerEntered.Set();
            if (!writerRelease.Wait(Limit)) throw new TimeoutException("Test warning writer was not released.");
            throw new IOException("Injected warning failure.");
        });
        var recovery = Task.Run(() => Record.Exception(() => owner.RequireRecovery("Uncertain cleanup.")));
        Task? disposal = null;
        try
        {
            Assert.True(writerEntered.Wait(Limit));
            disposal = Task.Run(() => { disposeEntered.Set(); owner.Dispose(); });
            Assert.True(disposeEntered.Wait(Limit));
            Assert.True(owner.IsHeld); Assert.False(owner.CanDispatch); Assert.False(disposal.IsCompleted);
        }
        finally { writerRelease.Set(); }
        Assert.IsType<IOException>(await recovery.WaitAsync(Limit));
        if (disposal != null) await disposal.WaitAsync(Limit);
        owner.Dispose();
        Assert.Equal(new[] { (byte)'1' }, persisted);
        Assert.Equal("1", File.ReadAllText(LockPath)); Assert.False(owner.IsHeld);
    }

    [Fact]
    public void WarningDiagnosticIsBoundedWithoutErasingOriginalFailureType()
    {
        using var owner = GameInputLease.AcquireForTesting("native-test", _root,
            writeWarning: (_, _) => throw new IOException(new string('x', 20000)));
        Assert.Throws<IOException>(() => owner.RequireRecovery(new string('y', 20000)));
        Assert.NotNull(owner.RecoveryNoticeFailure);
        Assert.InRange(owner.RecoveryNoticeFailure!.Length, 1, 2048);
        Assert.EndsWith("[diagnostic truncated]", owner.RecoveryNoticeFailure);
    }

    [Fact]
    public async Task RecoveryReportedDuringCleanWriteRestoresDirtyBeforeRelease()
    {
        using var cleanEntered = new ManualResetEventSlim();
        using var cleanRelease = new ManualResetEventSlim();
        var states = new List<byte>();
        var owner = GameInputLease.AcquireForTesting("native-test", _root, (stream, state) =>
        {
            states.Add(state);
            if (state == (byte)'0')
            {
                cleanEntered.Set();
                if (!cleanRelease.Wait(Limit)) throw new TimeoutException("Test clean writer was not released.");
            }
            GameInputLease.PersistState(stream, state);
        });
        var disposal = Task.Run(owner.Dispose);
        Task<Exception>? recovery = null;
        try
        {
            Assert.True(cleanEntered.Wait(Limit));
            recovery = Task.Run(() => Record.Exception(() => owner.RequireRecovery("Report arrived during close.")));
            using var deadline = new CancellationTokenSource(Limit);
            while (!owner.RecoveryRequired) await Task.Delay(1, deadline.Token);
            Assert.False(owner.CanDispatch); Assert.True(owner.IsHeld);
        }
        finally { cleanRelease.Set(); }
        await disposal.WaitAsync(Limit);
        // Reporting finishes after disposal, so the caller receives an explicit
        // lifetime error; its poison was nevertheless observed before unlock.
        Assert.NotNull(recovery);
        Assert.IsType<ObjectDisposedException>(await recovery!.WaitAsync(Limit));
        Assert.Equal(new[] { (byte)'1', (byte)'0', (byte)'1' }, states);
        Assert.Equal("1", File.ReadAllText(LockPath)); Assert.False(owner.IsHeld);
        Assert.Contains("Report arrived during close.", File.ReadAllText(NoticePath));
        File.Delete(NoticePath);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", _root));
    }

    [Fact]
    public void FullyLateRecoveryReportHasExplicitLifetimeFailureAndBestEffortWarning()
    {
        var owner = GameInputLease.Acquire("native-test", _root);
        owner.Dispose();
        Assert.Equal("0", File.ReadAllText(LockPath));
        Assert.Throws<ObjectDisposedException>(() => owner.RequireRecovery("Late cleanup report."));
        Assert.True(owner.RecoveryRequired); Assert.False(owner.CanDispatch);
        Assert.Contains("Late cleanup report.", File.ReadAllText(NoticePath));
        Assert.Equal("0", File.ReadAllText(LockPath));
    }

    [Fact]
    public void CanonicalDirectoryAliasCannotBypassLocalRecoveryLatch()
    {
        using (var owner = GameInputLease.Acquire("native-test", _root)) owner.RequireRecovery("Unconfirmed release.");
        File.Delete(NoticePath);
        // Even a separately edited byte cannot clear the process-local uncertainty.
        File.WriteAllText(LockPath, "0");
        var child = Path.Combine(_root, "child"); Directory.CreateDirectory(child);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", Path.Combine(child, "..")));
    }

    [Fact]
    public void NoticeAppearingDuringOwnershipPreventsCleanRelease()
    {
        var owner = GameInputLease.Acquire("native-test", _root);
        Directory.CreateDirectory(NoticePath);
        owner.Dispose();
        Assert.True(owner.RecoveryRequired); Assert.Equal("1", File.ReadAllText(LockPath));
        Directory.Delete(NoticePath);
        Assert.Throws<InvalidOperationException>(() => GameInputLease.Acquire("next", _root));
    }

    private void Seed(string value) { Directory.CreateDirectory(_root); File.WriteAllText(LockPath, value); }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
