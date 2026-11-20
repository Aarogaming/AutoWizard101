using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.Wasapi.CoreAudioApi.Interfaces;

namespace ProjectMaelstrom.Utilities;

internal sealed record ProcessAudioFormat(int SampleRate, int Channels, int BitsPerSample, int BlockAlign, string Encoding);
internal sealed record ProcessAudioPacket(byte[] PcmBytes, int FrameCount, ulong DevicePosition, ulong QpcPosition100Ns, uint Flags);
internal sealed record ProcessAudioCaptureResult(string Status, string? Error, long Packets, long Frames);
internal sealed record ProcessAudioBuffer(IntPtr Data, uint Frames, ulong DevicePosition, ulong QpcPosition100Ns, uint Flags);

/// <summary>Test seam; every method and disposal belongs to the capture thread.</summary>
internal interface IProcessAudioSource : IDisposable
{
    ProcessAudioFormat Format { get; }
    WaitHandle? PacketReady { get; }
    void Start();
    uint NextPacketFrames();
    ProcessAudioBuffer AcquireBuffer();
    void ReleaseBuffer(uint frames);
    void Stop();
}

internal sealed record ProcessAudioRunLimits(TimeSpan Startup, TimeSpan Shutdown, TimeSpan Duration,
    TimeSpan Poll, TimeSpan TargetCheck)
{
    public static ProcessAudioRunLimits Default { get; } = new(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(210), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(250));
    public void Validate()
    {
        if (Startup <= TimeSpan.Zero || Startup > Default.Startup || Shutdown <= TimeSpan.Zero || Shutdown > Default.Shutdown ||
            Duration <= TimeSpan.Zero || Duration > Default.Duration || Poll < TimeSpan.FromMilliseconds(1) || Poll > TimeSpan.FromMilliseconds(50) ||
            TargetCheck < TimeSpan.FromMilliseconds(1) || TargetCheck > TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(nameof(ProcessAudioRunLimits));
    }
}

/// <summary>Release only after invocation return, native callback completion, and caller handoff/abandonment.</summary>
internal sealed class ProcessAudioActivationLifetime(Action release)
{
    private readonly object _gate = new();
    private int _finished;
    public void InvocationReturned() => Finish(1);
    public void CallbackFinished() => Finish(2);
    public void CallerFinished() => Finish(4);
    private void Finish(int flag)
    {
        bool ready;
        lock (_gate)
        {
            if (_finished == 8) return;
            _finished |= flag;
            ready = _finished == 7;
            if (ready) _finished = 8;
        }
        if (ready) release();
    }
}

/// <summary>
/// Opt-in capture of one PID and its process tree. No default endpoint or microphone fallback.
/// Native resources remain owned by the MTA worker/activation callback until actual cleanup.
/// </summary>
internal sealed class ProcessAudioCapture : IDisposable, IAsyncDisposable
{
    internal const uint SilentFlag = 2;
    internal const int MaximumPacketBytes = 1_048_576;
    internal const string ProcessLoopbackDevice = @"VAD\Process_Loopback";
    internal static ProcessAudioFormat FixedFormat { get; } = new(44100, 2, 16, 4, "pcm-s16le");
    private static int _nativeSlot;
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<ProcessAudioCaptureResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<ProcessAudioPacket> _onPacket;
    private readonly Func<bool> _targetCurrent;
    private readonly ProcessAudioRunLimits _limits;
    private CancellationTokenRegistration _callerCancellation;
    private Thread? _thread;
    private int _stopRequested;
    public int ProcessId { get; }
    public ProcessAudioFormat Format { get; private set; } = FixedFormat;
    public Task<ProcessAudioCaptureResult> Completion => _completion.Task;
    public static int PendingNativeActivations => ActivationTicket.PendingCount;

    private ProcessAudioCapture(int pid, Action<ProcessAudioPacket> onPacket, Func<bool> targetCurrent,
        CancellationToken token, ProcessAudioRunLimits limits)
    {
        ProcessId = pid;
        _onPacket = onPacket;
        _targetCurrent = targetCurrent;
        _limits = limits;
        _callerCancellation = token.Register(static state => ((ProcessAudioCapture)state!).RequestStop(), this);
    }

    public static Task<ProcessAudioCapture> StartAsync(int pid, Action<ProcessAudioPacket> onPacket,
        Func<bool> targetCurrent, CancellationToken token)
    {
        ValidateArguments(pid, onPacket, targetCurrent, token);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348))
            throw new PlatformNotSupportedException("Selected-process audio requires Windows build 20348 or later. No audio fallback is used.");
        if (Interlocked.CompareExchange(ref _nativeSlot, 1, 0) != 0)
            throw new InvalidOperationException("Another process-audio capture or native cleanup is still active.");
        var permit = new SharedLease<NativeSlot>(new NativeSlot());
        return StartCoreAsync(pid, onPacket, targetCurrent, token, ProcessAudioRunLimits.Default,
            stop => NativeSource.Open(pid, permit, stop), permit, initializeCom: true);
    }

    internal static Task<ProcessAudioCapture> StartForTestsAsync(int pid, Action<ProcessAudioPacket> onPacket,
        Func<bool> targetCurrent, CancellationToken token, Func<CancellationToken, IProcessAudioSource> open,
        ProcessAudioRunLimits? limits = null) =>
        StartCoreAsync(pid, onPacket, targetCurrent, token, limits ?? ProcessAudioRunLimits.Default, open, null, initializeCom: false);

    private static async Task<ProcessAudioCapture> StartCoreAsync(int pid, Action<ProcessAudioPacket> onPacket,
        Func<bool> targetCurrent, CancellationToken token, ProcessAudioRunLimits limits,
        Func<CancellationToken, IProcessAudioSource> open, SharedLease<NativeSlot>? permit, bool initializeCom)
    {
        ProcessAudioCapture? capture = null;
        var threadStarted = false;
        try
        {
            ValidateArguments(pid, onPacket, targetCurrent, token);
            limits.Validate();
            ArgumentNullException.ThrowIfNull(open);
            capture = new(pid, onPacket, targetCurrent, token, limits);
            capture._thread = new Thread(() => capture.Run(open, permit, initializeCom))
            { IsBackground = true, Name = "Maelstrom selected-process audio" };
            capture._thread.SetApartmentState(ApartmentState.MTA);
            capture._thread.Start();
            threadStarted = true;
            await capture._started.Task.WaitAsync(limits.Startup, token).ConfigureAwait(false);
            return capture;
        }
        catch
        {
            capture?.RequestStop();
            // The startup TCS may fault after the caller timeout. Observe it without detaching native ownership.
            if (capture != null)
                _ = capture._started.Task.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            if (!threadStarted)
            {
                capture?._callerCancellation.Dispose();
                capture?._stop.Dispose();
                permit?.Dispose();
            }
            throw;
        }
    }

    private static void ValidateArguments(int pid, Action<ProcessAudioPacket> onPacket, Func<bool> targetCurrent, CancellationToken token)
    {
        if (pid <= 0) throw new ArgumentOutOfRangeException(nameof(pid));
        ArgumentNullException.ThrowIfNull(onPacket);
        ArgumentNullException.ThrowIfNull(targetCurrent);
        token.ThrowIfCancellationRequested();
    }

    private void Run(Func<CancellationToken, IProcessAudioSource> open, SharedLease<NativeSlot>? permit, bool initializeCom)
    {
        IProcessAudioSource? source = null;
        var comInitialized = false;
        var running = false;
        var status = "stopped";
        string? failure = null;
        long packets = 0, frames = 0;
        try
        {
            if (initializeCom) { Check(CoInitializeEx(IntPtr.Zero, 0)); comInitialized = true; }
            _stop.Token.ThrowIfCancellationRequested();
            if (!_targetCurrent()) { status = "target-exited"; throw new InvalidOperationException("The selected audio target is no longer current."); }
            source = open(_stop.Token);
            if (source.Format != FixedFormat) throw new InvalidDataException("Process audio did not provide the requested PCM16 stereo format.");
            Format = source.Format;
            _stop.Token.ThrowIfCancellationRequested();
            if (!_targetCurrent()) { status = "target-exited"; throw new InvalidOperationException("The audio target changed during activation."); }
            source.Start();
            running = true;
            var elapsed = Stopwatch.StartNew();
            var nextTargetCheck = TimeSpan.Zero;
            var waits = source.PacketReady is { } ready ? new[] { _stop.Token.WaitHandle, ready } : new[] { _stop.Token.WaitHandle };
            _started.TrySetResult(true);
            while (!_stop.IsCancellationRequested)
            {
                if (elapsed.Elapsed >= _limits.Duration) { status = "duration-limit"; break; }
                if (elapsed.Elapsed >= nextTargetCheck)
                {
                    if (!_targetCurrent()) { status = "target-exited"; break; }
                    nextTargetCheck = elapsed.Elapsed + _limits.TargetCheck;
                }
                for (var drained = 0; drained < 64 && !_stop.IsCancellationRequested && elapsed.Elapsed < _limits.Duration; drained++)
                {
                    if (source.NextPacketFrames() == 0) break;
                    var packet = CopyPacket(source);
                    if (packet == null) continue;
                    if (_stop.IsCancellationRequested) break;
                    // The native buffer has already been released. Consumer must be bounded/nonblocking.
                    _onPacket(packet);
                    packets++;
                    frames += packet.FrameCount;
                }
                WaitHandle.WaitAny(waits, _limits.Poll);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { status = "stopped"; }
        catch (Exception error)
        {
            if (status != "target-exited") status = "failed";
            failure = error.Message;
            _started.TrySetException(error);
        }
        finally
        {
            if (source != null)
            {
                try { if (running) source.Stop(); }
                catch (Exception error) { status = "failed"; failure = Append(failure, "Stop: " + error.Message); }
                try { source.Dispose(); }
                catch (Exception error) { status = "failed"; failure = Append(failure, "Cleanup: " + error.Message); }
            }
            if (comInitialized) CoUninitialize();
            permit?.Dispose();
            _callerCancellation.Dispose();
            if (!_started.Task.IsCompleted) _started.TrySetCanceled(new CancellationToken(true));
            _stop.Dispose();
            _completion.TrySetResult(new(status, failure, packets, frames));
        }
    }

    /// <summary>Copies into an owned array, then releases native memory before returning to any consumer.</summary>
    internal static ProcessAudioPacket? CopyPacket(IProcessAudioSource source)
    {
        var buffer = source.AcquireBuffer();
        try
        {
            if (buffer.Frames == 0) return null;
            var bytes = checked((long)buffer.Frames * source.Format.BlockAlign);
            if (bytes <= 0 || bytes > MaximumPacketBytes || buffer.Frames > int.MaxValue)
                throw new InvalidDataException("The audio packet exceeds the bounded capture size.");
            var owned = new byte[(int)bytes];
            if ((buffer.Flags & SilentFlag) == 0)
            {
                if (buffer.Data == IntPtr.Zero) throw new InvalidDataException("A non-silent audio packet has no data.");
                Marshal.Copy(buffer.Data, owned, 0, owned.Length);
            }
            return new(owned, (int)buffer.Frames, buffer.DevicePosition, buffer.QpcPosition100Ns, buffer.Flags);
        }
        finally { source.ReleaseBuffer(buffer.Frames); }
    }

    public void RequestStop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) != 0) return;
        try { _stop.Cancel(); } catch (ObjectDisposedException) { }
    }

    public async Task<ProcessAudioCaptureResult> StopAsync()
    {
        RequestStop();
        if (Thread.CurrentThread == _thread) throw new InvalidOperationException("Packet callbacks may request stop but must not await their own capture thread.");
        return await Completion.WaitAsync(_limits.Shutdown).ConfigureAwait(false);
    }

    /// <summary>Nonblocking stop request. Await StopAsync/DisposeAsync to confirm native cleanup.</summary>
    public void Dispose() => RequestStop();
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    internal static byte[] ActivationBytes(int pid)
    {
        if (pid <= 0) throw new ArgumentOutOfRangeException(nameof(pid));
        var bytes = new byte[12];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1); // PROCESS_LOOPBACK activation.
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)pid);
        // Last DWORD is zero: INCLUDE_TARGET_PROCESS_TREE. No exclude-tree/default-device mode exists here.
        return bytes;
    }

    private static string Append(string? previous, string next) => previous == null ? next : previous + "; " + next;
    private static void Check(int result) { if (result < 0) Marshal.ThrowExceptionForHR(result); }
    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    private sealed class NativeSlot : IDisposable { public void Dispose() => Interlocked.Exchange(ref _nativeSlot, 0); }

    private sealed class NativeSource : IProcessAudioSource
    {
        private readonly object _audioObject;
        private readonly NativeAudioClient _client;
        private object? _captureObject;
        private NativeCaptureClient? _capture;
        private readonly AutoResetEvent _ready = new(false);
        public ProcessAudioFormat Format => FixedFormat;
        public WaitHandle PacketReady => _ready;
        private NativeSource(object audioObject) { _audioObject = audioObject; _client = (NativeAudioClient)audioObject; }

        public static IProcessAudioSource Open(int pid, SharedLease<NativeSlot> permit, CancellationToken token)
        {
            using var ticket = new ActivationTicket(pid, permit.Retain());
            ticket.Invoke();
            var audio = ticket.Take(ProcessAudioRunLimits.Default.Startup, token);
            NativeSource? source = null;
            try
            {
                source = new(audio);
                var format = new NativeWaveFormat { Tag = 1, Channels = 2, SampleRate = 44100, AverageBytes = 176400, BlockAlign = 4, Bits = 16 };
                var session = Guid.Empty;
                Check(source._client.Initialize(0, 0x80060000, 0, 0, ref format, ref session));
                var captureId = typeof(NativeCaptureClient).GUID;
                Check(source._client.GetService(ref captureId, out var capture));
                source._captureObject = capture;
                source._capture = (NativeCaptureClient)capture;
                Check(source._client.SetEventHandle(source._ready.SafeWaitHandle.DangerousGetHandle()));
                return source;
            }
            catch { if (source != null) source.Dispose(); else Release(audio); throw; }
        }
        public void Start() => Check(_client.Start());
        public void Stop() => Check(_client.Stop());
        public uint NextPacketFrames() { Check(_capture!.GetNextPacketSize(out var frames)); return frames; }
        public ProcessAudioBuffer AcquireBuffer()
        {
            Check(_capture!.GetBuffer(out var data, out var frames, out var flags, out var position, out var qpc));
            return new(data, frames, position, qpc, flags);
        }
        public void ReleaseBuffer(uint frames) => Check(_capture!.ReleaseBuffer(frames));
        public void Dispose()
        {
            try { Release(_captureObject); }
            finally { _captureObject = null; _capture = null; try { Release(_audioObject); } finally { _ready.Dispose(); } }
        }
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class ActivationTicket : IActivateAudioInterfaceCompletionHandler, AgileObject, IDisposable
    {
        private static readonly ConcurrentDictionary<long, ActivationTicket> Pending = new();
        private static long _sequence;
        private readonly long _id = Interlocked.Increment(ref _sequence);
        private readonly object _gate = new();
        private readonly SharedLease<NativeSlot> _permit;
        private readonly ProcessAudioActivationLifetime _lifetime;
        private readonly TaskCompletionSource<bool> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IntPtr _blob, _variant;
        private IActivateAudioInterfaceAsyncOperation? _operation;
        private object? _audio;
        private Exception? _error;
        public static int PendingCount => Pending.Count;

        public ActivationTicket(int pid, SharedLease<NativeSlot> permit)
        {
            _permit = permit;
            _lifetime = new(Cleanup);
            try
            {
                var bytes = ActivationBytes(pid);
                _blob = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, _blob, bytes.Length);
                var variant = new ActivationVariant { Type = 65, Blob = new NativeBlob { Size = (uint)bytes.Length, Data = _blob } };
                _variant = Marshal.AllocHGlobal(Marshal.SizeOf<ActivationVariant>());
                Marshal.StructureToPtr(variant, _variant, false);
                Pending.TryAdd(_id, this);
            }
            catch { Cleanup(); throw; }
        }

        public void Invoke()
        {
            try
            {
                var id = typeof(NativeAudioClient).GUID;
                var result = ActivateAudioInterfaceAsync(ProcessLoopbackDevice, ref id, _variant, this, out var operation);
                lock (_gate) _operation = operation;
                if (result < 0) throw Marshal.GetExceptionForHR(result)!;
            }
            catch (Exception error)
            {
                lock (_gate) _error = error;
                _finished.TrySetResult(true);
                _lifetime.CallbackFinished(); // Failed invocation has no pending successful operation.
                throw;
            }
            finally { _lifetime.InvocationReturned(); }
        }

        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation)
        {
            object? audio = null;
            Exception? error = null;
            try { operation.GetActivateResult(out var result, out audio); Check(result); }
            catch (Exception caught) { error = caught; }
            lock (_gate) { _audio = audio; _error = error; }
            _finished.TrySetResult(true);
            _lifetime.CallbackFinished();
        }

        public object Take(TimeSpan timeout, CancellationToken token)
        {
            if (!_finished.Task.Wait(timeout, token)) throw new TimeoutException("Selected-process audio activation timed out; late native completion remains owned.");
            object audio;
            lock (_gate)
            {
                if (_error != null) throw new InvalidOperationException("Selected-process audio activation failed.", _error);
                audio = _audio ?? throw new InvalidOperationException("Audio activation supplied no interface.");
                _audio = null;
            }
            _lifetime.CallerFinished();
            return audio;
        }
        public void Dispose() => _lifetime.CallerFinished();
        private void Cleanup()
        {
            object? audio;
            IActivateAudioInterfaceAsyncOperation? operation;
            lock (_gate) { audio = _audio; _audio = null; operation = _operation; _operation = null; }
            try { Release(audio); }
            catch { /* The capture result remains failed/cancelled; never throw across a native callback. */ }
            try { Release(operation); } catch { }
            if (_variant != IntPtr.Zero) { Marshal.FreeHGlobal(_variant); _variant = IntPtr.Zero; }
            if (_blob != IntPtr.Zero) { Marshal.FreeHGlobal(_blob); _blob = IntPtr.Zero; }
            Pending.TryRemove(_id, out _);
            _permit.Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBlob { public uint Size; public IntPtr Data; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ActivationVariant { public ushort Type, Reserved1, Reserved2, Reserved3; public NativeBlob Blob; }
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct NativeWaveFormat
    { public ushort Tag, Channels; public uint SampleRate, AverageBytes; public ushort BlockAlign, Bits, ExtraSize; }

    [ComImport, Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface AgileObject { }
    [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface NativeAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint flags, long duration, long period, ref NativeWaveFormat format, ref Guid session);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, ref NativeWaveFormat format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long normal, out long minimum);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid id, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }
    [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface NativeCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong position, out ulong qpc);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }
    [DllImport("Mmdevapi.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int ActivateAudioInterfaceAsync(string deviceInterfacePath, ref Guid id, IntPtr activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler, out IActivateAudioInterfaceAsyncOperation operation);
    [DllImport("Ole32.dll", ExactSpelling = true)] private static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("Ole32.dll", ExactSpelling = true)] private static extern void CoUninitialize();
}
