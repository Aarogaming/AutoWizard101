using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

if (args.Length > 0 && args[0] == "--audio-tone") return AudioToneProcess.Run(args);

var argsDict = ParseArgs(args);
if (argsDict.TryGetValue("inputLock", out var lockDirectory))
{
    try
    {
        if (argsDict.ContainsKey("failRecoveryWarning"))
        {
            using (var recoveryLease = ProjectMaelstrom.Utilities.GameInputLease.AcquireForTesting(
                "isolated-recovery-test", lockDirectory!,
                writeWarning: (_, _) => throw new IOException("Injected recovery notice failure")))
            {
                try { recoveryLease.RequireRecovery("Input-free release failure fixture"); }
                catch (IOException) { }
                if (!recoveryLease.RecoveryRequired || recoveryLease.CanDispatch ||
                    string.IsNullOrWhiteSpace(recoveryLease.RecoveryNoticeFailure)) return 10;
            }
            if (File.Exists(Path.Combine(lockDirectory!, "input-recovery-required.txt")) ||
                File.ReadAllText(Path.Combine(lockDirectory!, "game-input.lock")) != "1") return 11;
            Console.Out.WriteLine("recovery-latched-without-notice");
            return 0;
        }
        using var lease = ProjectMaelstrom.Utilities.GameInputLease.Acquire("isolated-lock-test", lockDirectory);
        if (argsDict.TryGetValue("lockReady", out var ready)) File.WriteAllText(ready!, "held");
        if (argsDict.TryGetValue("holdLockMs", out var hold)) Thread.Sleep(Math.Clamp(int.Parse(hold!), 0, 10000));
        return 0;
    }
    catch (InvalidOperationException) { return 9; }
}

var stdout = argsDict.TryGetValue("stdout", out var so) ? so : null;
var stderr = argsDict.TryGetValue("stderr", out var se) ? se : null;
var exitCode = argsDict.TryGetValue("exit", out var ec) && int.TryParse(ec, out var ecParsed) ? ecParsed : 0;
var sleepMs = argsDict.TryGetValue("sleepMs", out var sm) && int.TryParse(sm, out var smParsed) ? smParsed : 0;
var printCwd = argsDict.ContainsKey("printCwd");
var envKey = argsDict.TryGetValue("printEnv", out var env) ? env : null;

if (argsDict.TryGetValue("spawnChildPidFile", out var pidFile) && !string.IsNullOrWhiteSpace(pidFile))
{
    var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
    info.ArgumentList.Add(System.Reflection.Assembly.GetExecutingAssembly().Location);
    info.ArgumentList.Add("--sleepMs");
    info.ArgumentList.Add("10000");
    using var child = Process.Start(info)!;
    File.WriteAllText(pidFile, child.Id.ToString());
}

if (sleepMs > 0)
{
    Thread.Sleep(Math.Min(sleepMs, 10_000));
}

if (stdout != null)
{
    Console.Out.Write(stdout);
}

if (printCwd)
{
    Console.Out.Write(Environment.CurrentDirectory);
}

if (!string.IsNullOrEmpty(envKey))
{
    var value = Environment.GetEnvironmentVariable(envKey) ?? string.Empty;
    Console.Out.Write(value);
}

if (stderr != null)
{
    Console.Error.Write(stderr);
}

return exitCode;

static Dictionary<string, string?> ParseArgs(string[] input)
{
    var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    string? pendingKey = null;
    foreach (var arg in input)
    {
        if (arg.StartsWith("--", StringComparison.Ordinal))
        {
            pendingKey = arg.TrimStart('-');
            dict[pendingKey] = null;
        }
        else if (pendingKey != null)
        {
            dict[pendingKey] = arg;
            pendingKey = null;
        }
    }
    return dict;
}

/// <summary>Explicitly invoked controlled playback fixture. No microphone, capture, focus or game input.
/// Start and stop markers allow the test coordinator to activate process capture before playback.</summary>
internal static class AudioToneProcess
{
    private const int Rate = 48000;
    private const double Amplitude = 0.03;
    private const uint WaveDone = 1;

    internal static int Run(string[] arguments)
    {
        if (!OperatingSystem.IsWindows() || arguments.Length != 4 ||
            !double.TryParse(arguments[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var frequency) ||
            !double.IsFinite(frequency) || frequency is < 200 or > 5000 ||
            !int.TryParse(arguments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var durationMs) ||
            durationMs is < 100 or > 5000 || !Path.IsPathFullyQualified(arguments[3]))
        {
            Console.Error.Write("Usage: --audio-tone <200..5000 Hz> <100..5000 ms> <new absolute report.json>");
            return 2;
        }
        var reportPath = Path.GetFullPath(arguments[3]);
        var readyPath = reportPath + ".ready.json";
        var startPath = reportPath + ".start.request";
        var stopPath = reportPath + ".stop.request";
        if (File.Exists(reportPath) || File.Exists(readyPath) || File.Exists(startPath) || File.Exists(stopPath) ||
            !Directory.Exists(Path.GetDirectoryName(reportPath))) return 2;

        using var report = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        IntPtr output = IntPtr.Zero, header = IntPtr.Zero;
        GCHandle pinned = default;
        var prepared = false;
        var status = "failed";
        string? error = null;
        long? submittedQpc = null, completedQpc = null;
        var nativeCleanupSucceeded = true;
        var waitClock = Stopwatch.StartNew();
        try
        {
            var samples = CreateTone(frequency, durationMs);
            pinned = GCHandle.Alloc(samples, GCHandleType.Pinned);
            var format = new WaveFormat
            {
                FormatTag = 1,
                Channels = 1,
                SamplesPerSecond = Rate,
                AverageBytesPerSecond = Rate * 2,
                BlockAlign = 2,
                BitsPerSample = 16,
                ExtraSize = 0
            };
            Check(waveOutOpen(out output, uint.MaxValue, ref format, IntPtr.Zero, UIntPtr.Zero, 0), "open");
            header = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHeader>());
            Marshal.StructureToPtr(new WaveHeader { Data = pinned.AddrOfPinnedObject(), BufferLength = (uint)(samples.Length * sizeof(short)) }, header, false);
            Check(waveOutPrepareHeader(output, header, (uint)Marshal.SizeOf<WaveHeader>()), "prepare");
            prepared = true;
            using (var process = Process.GetCurrentProcess())
            using (var ready = new FileStream(readyPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                JsonSerializer.Serialize(ready, new
                {
                    kind = "controlled-audio-tone-ready",
                    pid = Environment.ProcessId,
                    process_started_utc_ticks = process.StartTime.ToUniversalTime().Ticks,
                    frequency_hz = frequency,
                    sample_rate = Rate,
                    channels = 1,
                    bits_per_sample = 16,
                    duration_ms = durationMs,
                    amplitude = Amplitude,
                    start_marker = startPath,
                    stop_marker = stopPath,
                    ready_qpc_ticks = Stopwatch.GetTimestamp(),
                    qpc_frequency = Stopwatch.Frequency,
                    sound_started = false
                });

            while (!File.Exists(startPath) && waitClock.ElapsedMilliseconds < 15000 &&
                !cancellation.IsCancellationRequested && !File.Exists(stopPath)) Thread.Sleep(10);
            if (cancellation.IsCancellationRequested || File.Exists(stopPath)) status = "cancelled-before-playback";
            else if (!File.Exists(startPath)) status = "start-marker-timeout";
            else
            {
                submittedQpc = Stopwatch.GetTimestamp();
                Check(waveOutWrite(output, header, (uint)Marshal.SizeOf<WaveHeader>()), "write");
                var playbackClock = Stopwatch.StartNew();
                while (playbackClock.ElapsedMilliseconds <= durationMs + 2000 &&
                    !cancellation.IsCancellationRequested && !File.Exists(stopPath))
                {
                    if ((Marshal.PtrToStructure<WaveHeader>(header).Flags & WaveDone) != 0)
                    {
                        completedQpc = Stopwatch.GetTimestamp();
                        status = "playback-complete";
                        break;
                    }
                    Thread.Sleep(5);
                }
                if (completedQpc == null)
                    status = cancellation.IsCancellationRequested || File.Exists(stopPath) ? "cancelled-during-playback" : "playback-timeout";
            }
        }
        catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; }
        finally
        {
            Console.CancelKeyPress -= handler;
            if (output != IntPtr.Zero)
            {
                var reset = waveOutReset(output);
                uint unprepare = 0;
                if (prepared)
                {
                    for (var attempt = 0; attempt < 50; attempt++)
                    {
                        unprepare = waveOutUnprepareHeader(output, header, (uint)Marshal.SizeOf<WaveHeader>());
                        if (unprepare != 33) break; // WAVERR_STILLPLAYING: bounded cleanup only.
                        Thread.Sleep(10);
                    }
                }
                var close = waveOutClose(output);
                nativeCleanupSucceeded = reset == 0 && unprepare == 0 && close == 0;
                if (!nativeCleanupSucceeded)
                {
                    error = $"native-cleanup reset={reset}, unprepare={unprepare}, close={close}";
                    status = "cleanup-failed";
                }
                // A native driver may retain queued pointers if close fails. In that case keep
                // the tiny bounded allocation pinned until this test process exits.
                if (close != 0) { header = IntPtr.Zero; pinned = default; }
            }
            if (header != IntPtr.Zero) Marshal.FreeHGlobal(header);
            if (pinned.IsAllocated) pinned.Free();
            JsonSerializer.Serialize(report, new
            {
                kind = "controlled-audio-tone-result",
                status,
                error,
                pid = Environment.ProcessId,
                frequency_hz = frequency,
                duration_ms = durationMs,
                sample_rate = Rate,
                channels = 1,
                bits_per_sample = 16,
                amplitude = Amplitude,
                submitted_qpc_ticks = submittedQpc,
                completed_qpc_ticks = completedQpc,
                qpc_frequency = Stopwatch.Frequency,
                native_cleanup_succeeded = nativeCleanupSucceeded,
                timestamp_scope = "waveOut submission and observed completion; not speaker or packet-onset timestamps",
                microphone_used = false,
                focus_changed = false
            });
            report.Flush(true);
        }
        return status == "playback-complete" ? 0 : status.StartsWith("cancelled", StringComparison.Ordinal) ? 4 : 3;
    }

    private static short[] CreateTone(double frequency, int durationMs)
    {
        var samples = new short[Rate * durationMs / 1000];
        const int ramp = Rate / 40;
        for (var i = 0; i < samples.Length; i++)
        {
            var envelope = Math.Min(1, Math.Min(i, samples.Length - 1 - i) / (double)ramp);
            samples[i] = (short)Math.Round(32767 * Amplitude * envelope * Math.Sin(2 * Math.PI * frequency * i / Rate));
        }
        return samples;
    }

    private static void Check(uint code, string operation)
    {
        if (code != 0) throw new InvalidOperationException($"waveOut {operation} failed: {code}");
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public ushort FormatTag, Channels;
        public uint SamplesPerSecond, AverageBytesPerSecond;
        public ushort BlockAlign, BitsPerSample, ExtraSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public uint BufferLength, BytesRecorded;
        public UIntPtr User;
        public uint Flags, Loops;
        public IntPtr Next;
        public UIntPtr Reserved;
    }

    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutOpen(out IntPtr output, uint deviceId, ref WaveFormat format, IntPtr callback, UIntPtr instance, uint flags);
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutPrepareHeader(IntPtr output, IntPtr header, uint size);
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutWrite(IntPtr output, IntPtr header, uint size);
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutReset(IntPtr output);
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutUnprepareHeader(IntPtr output, IntPtr header, uint size);
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutClose(IntPtr output);
}
