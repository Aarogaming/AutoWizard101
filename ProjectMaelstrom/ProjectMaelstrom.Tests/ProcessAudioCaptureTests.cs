using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class ProcessAudioCaptureTests
{
    private static ProcessAudioRunLimits QuickLimits => new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1));

    [Fact]
    public void ActivationIncludesOnlyTheRequestedProcessTree()
    {
        var bytes = ProcessAudioCapture.ActivationBytes(12345);
        Assert.Equal(12, bytes.Length);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        Assert.Equal(12345u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProcessAudioCapture.ActivationBytes(0));
    }

    [Fact]
    public void ActivationOwnershipSurvivesEveryLateCallbackOrdering()
    {
        int[][] orders = [[0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0]];
        foreach (var order in orders)
        {
            var releases = 0;
            var lifetime = new ProcessAudioActivationLifetime(() => releases++);
            Action[] finish = [lifetime.InvocationReturned, lifetime.CallbackFinished, lifetime.CallerFinished];
            finish[order[0]]();
            finish[order[0]]();
            finish[order[1]]();
            Assert.Equal(0, releases);
            finish[order[2]]();
            foreach (var signal in finish) signal();
            Assert.Equal(1, releases);
        }
    }

    [Fact]
    public void PacketOwnsItsCopyAndPreservesNativePositionsAndFlags()
    {
        using var source = new FakeSource([1, 2, 3, 4, 5, 6, 7, 8], frames: 2, flags: 5);
        var packet = Assert.IsType<ProcessAudioPacket>(ProcessAudioCapture.CopyPacket(source));
        Marshal.WriteByte(source.Data, 0, 99);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, packet.PcmBytes);
        Assert.Equal(2, packet.FrameCount);
        Assert.Equal(123456ul, packet.DevicePosition);
        Assert.Equal(9876543210ul, packet.QpcPosition100Ns);
        Assert.Equal(5u, packet.Flags);
        Assert.Equal(1, source.Releases);
    }

    [Fact]
    public void SilentPacketWithNoNativePointerProducesZeros()
    {
        using var source = new FakeSource(null, frames: 2, flags: 2);
        var packet = Assert.IsType<ProcessAudioPacket>(ProcessAudioCapture.CopyPacket(source));
        Assert.Equal(new byte[8], packet.PcmBytes);
        Assert.Equal(2u, packet.Flags);
        Assert.Equal(1, source.Releases);
    }

    [Fact]
    public void InvalidOrOversizedPacketsStillReleaseTheirNativeBuffer()
    {
        using var absent = new FakeSource(null, frames: 1);
        Assert.Throws<InvalidDataException>(() => ProcessAudioCapture.CopyPacket(absent));
        Assert.Equal(1, absent.Releases);
        using var oversized = new FakeSource(null, frames: uint.MaxValue, flags: 2);
        Assert.Throws<InvalidDataException>(() => ProcessAudioCapture.CopyPacket(oversized));
        Assert.Equal(1, oversized.Releases);
    }

    [Fact]
    public void ZeroFramesReleaseWithoutInventingAPacket()
    {
        using var source = new FakeSource(null, frames: 0);
        Assert.Null(ProcessAudioCapture.CopyPacket(source));
        Assert.Equal(1, source.Releases);
    }

    [Fact]
    public async Task WorkerReleasesBeforeConsumerAndDisposesOnItsOwnMtaThread()
    {
        var source = new FakeSource([1, 2, 3, 4], frames: 1);
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var capture = await ProcessAudioCapture.StartForTestsAsync(1, packet =>
        {
            Assert.Equal(1, source.Releases);
            Assert.Equal(ApartmentState.MTA, Thread.CurrentThread.GetApartmentState());
            received.TrySetResult(true);
        }, () => true, CancellationToken.None, _ => source, QuickLimits);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var result = await capture.StopAsync();
        Assert.Equal("stopped", result.Status);
        Assert.Equal(1, result.Packets);
        Assert.Equal(1, result.Frames);
        Assert.Equal(1, source.Starts);
        Assert.Equal(1, source.Stops);
        Assert.True(source.Disposed.Task.IsCompletedSuccessfully);
        Assert.Single(source.ThreadIds.Distinct());
        Assert.Equal(ProcessAudioCapture.FixedFormat, capture.Format);
    }

    [Fact]
    public async Task ConsumerFailureStopsAndCleansUpWithoutCountingAnAcceptedPacket()
    {
        var source = new FakeSource([1, 2, 3, 4], frames: 1);
        await using var capture = await ProcessAudioCapture.StartForTestsAsync(1,
            _ => throw new InvalidOperationException("consumer refused"), () => true,
            CancellationToken.None, _ => source, QuickLimits);
        var result = await capture.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("failed", result.Status);
        Assert.Contains("consumer refused", result.Error);
        Assert.Equal(0, result.Packets);
        Assert.Equal(1, source.Releases);
        Assert.Equal(1, source.Stops);
        Assert.True(source.Disposed.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task TargetExitStopsWithoutWaitingForTheFullDuration()
    {
        var current = 1;
        var source = new FakeSource(null);
        await using var capture = await ProcessAudioCapture.StartForTestsAsync(1, _ => { },
            () => Volatile.Read(ref current) == 1, CancellationToken.None, _ => source, QuickLimits);
        Volatile.Write(ref current, 0);
        var result = await capture.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("target-exited", result.Status);
        Assert.Equal(0, result.Packets);
        Assert.Equal(1, source.Stops);
    }

    [Fact]
    public async Task CallerCancellationCompletesOnlyAfterSourceDisposal()
    {
        using var cancel = new CancellationTokenSource();
        var source = new FakeSource(null);
        await using var capture = await ProcessAudioCapture.StartForTestsAsync(1, _ => { }, () => true,
            cancel.Token, _ => source, QuickLimits);
        cancel.Cancel();
        var result = await capture.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("stopped", result.Status);
        Assert.True(source.Disposed.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task DurationLimitCompletesAnOtherwiseIdleCapture()
    {
        var source = new FakeSource(null);
        await using var capture = await ProcessAudioCapture.StartForTestsAsync(1, _ => { }, () => true,
            CancellationToken.None, _ => source, QuickLimits with { Duration = TimeSpan.FromMilliseconds(25) });
        var result = await capture.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("duration-limit", result.Status);
        Assert.Equal(1, source.Stops);
    }

    [Fact]
    public async Task FormatMismatchFailsExplicitlyAndDisposesTheUnstartedSource()
    {
        var source = new FakeSource(null) { Format = new(48000, 1, 32, 4, "float32") };
        await Assert.ThrowsAsync<InvalidDataException>(() => ProcessAudioCapture.StartForTestsAsync(1, _ => { },
            () => true, CancellationToken.None, _ => source, QuickLimits));
        await source.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, source.Starts);
        Assert.Equal(0, source.Stops);
    }

    [Fact]
    public async Task StartupTimeoutDoesNotDisposeAResourceStillOwnedByTheOpeningThread()
    {
        using var releaseOpen = new ManualResetEventSlim();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new FakeSource(null);
        var starting = ProcessAudioCapture.StartForTestsAsync(1, _ => { }, () => true, CancellationToken.None, _ =>
        {
            entered.TrySetResult(true);
            releaseOpen.Wait();
            return source;
        }, QuickLimits with { Startup = TimeSpan.FromMilliseconds(150) });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => starting);
            Assert.False(source.Disposed.Task.IsCompleted);
        }
        finally { releaseOpen.Set(); }
        await source.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, source.Starts);
    }

    [Fact]
    public void ResourceLimitsCannotExceedTheProductionCaps()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            (ProcessAudioRunLimits.Default with { Duration = TimeSpan.FromSeconds(211) }).Validate());
        Assert.Equal(TimeSpan.FromSeconds(210), ProcessAudioRunLimits.Default.Duration);
        (ProcessAudioRunLimits.Default with { Duration = TimeSpan.FromSeconds(210) }).Validate();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            (ProcessAudioRunLimits.Default with { Poll = TimeSpan.Zero }).Validate());
    }

    private sealed class FakeSource : IProcessAudioSource
    {
        public ProcessAudioFormat Format { get; init; } = ProcessAudioCapture.FixedFormat;
        public WaitHandle? PacketReady => null;
        public IntPtr Data { get; private set; }
        public ConcurrentQueue<int> ThreadIds { get; } = new();
        public TaskCompletionSource<bool> Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Starts, Stops, Releases;
        private readonly uint _frames;
        private readonly uint _flags;
        private bool _consumed;

        public FakeSource(byte[]? bytes, uint frames = 0, uint flags = 0)
        {
            _frames = frames;
            _flags = flags;
            if (bytes == null) return;
            Data = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, Data, bytes.Length);
        }
        private void NoteThread() => ThreadIds.Enqueue(Environment.CurrentManagedThreadId);
        public void Start() { NoteThread(); Starts++; }
        public uint NextPacketFrames() { NoteThread(); return _consumed ? 0 : _frames; }
        public ProcessAudioBuffer AcquireBuffer()
        {
            NoteThread();
            return new(Data, _frames, 123456, 9876543210, _flags);
        }
        public void ReleaseBuffer(uint frames) { NoteThread(); Releases++; _consumed = true; }
        public void Stop() { NoteThread(); Stops++; }
        public void Dispose()
        {
            NoteThread();
            if (Data != IntPtr.Zero) { Marshal.FreeHGlobal(Data); Data = IntPtr.Zero; }
            Disposed.TrySetResult(true);
        }
    }
}
