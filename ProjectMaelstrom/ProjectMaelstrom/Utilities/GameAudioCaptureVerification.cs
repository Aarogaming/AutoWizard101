using System.Diagnostics;
using NAudio.Wave;

namespace ProjectMaelstrom.Utilities;

/// <summary>Finite audio-only verification against explicitly selected owned test processes.</summary>
internal static class GameAudioCaptureVerification
{
    public static async Task RunAsync(int processId, long startedTicks, string directory, int milliseconds,
        double targetHz, double unrelatedHz, CancellationToken token)
    {
        if (milliseconds is < 500 or > 10000) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Choose a new audio verification folder.");
        long? observedTicks = null;
        string? identityError = null;
        bool Current()
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                observedTicks = process.StartTime.ToUniversalTime().Ticks;
                return observedTicks == startedTicks && !process.HasExited;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            { identityError = error.GetType().Name + ": " + error.Message; return false; }
        }
        if (!Current()) throw new InvalidOperationException($"The selected audio test process changed or is inaccessible. PID={processId}, expected start={startedTicks}, observed start={observedTicks}, error={identityError}");
        Directory.CreateDirectory(directory);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var clock = Stopwatch.StartNew();
        await using var recording = await GameAudioRecording.StartAsync(processId, startedTicks, directory, Current, stop.Token).ConfigureAwait(false);
        GameExperiments.WriteAtomic(Path.Combine(directory, "audio-ready.json"), new { process_id = processId, started_utc_ticks = startedTicks, maximum_capture_ms = milliseconds, input_sent = false });
        var captureClock = Stopwatch.StartNew();
        while (captureClock.ElapsedMilliseconds < milliseconds && !token.IsCancellationRequested && !File.Exists(Path.Combine(directory, "stop.request")))
            await Task.Delay(25, CancellationToken.None).ConfigureAwait(false);
        await recording.StopAsync().ConfigureAwait(false);
        using var wave = new WaveFileReader(Path.Combine(directory, "game-audio.wav"));
        if (wave.Length > GameAudioPacketTimeline.MaximumBytes || wave.WaveFormat.BitsPerSample != 16) throw new InvalidDataException("Unexpected audio verification format.");
        var bytes = new byte[checked((int)wave.Length)];
        wave.ReadExactly(bytes);
        var enoughSamples = bytes.Length / wave.WaveFormat.BlockAlign >= wave.WaveFormat.SampleRate / 10;
        var signal = enoughSamples ? ProcessAudioVerification.AnalyzePcm16(bytes, wave.WaveFormat.SampleRate, wave.WaveFormat.Channels, targetHz, unrelatedHz) : null;
        GameExperiments.WriteAtomic(Path.Combine(directory, "signal-verification.json"), new
        {
            schema_version = 1,
            kind = "controlled-process-audio-signal-check",
            processId,
            startedTicks,
            target_hz = targetHz,
            unrelated_hz = unrelatedHz,
            signal,
            status = enoughSamples ? "measured" : "insufficient-recorded-samples",
            silence_established = signal?.Silent == true,
            elapsed_ms = clock.Elapsed.TotalMilliseconds,
            audio_timing = "audio-timing.json",
            audio_only = true,
            note = "A signal check is not a video-sync, game-tone mapping or microphone capture test. Review packet timing and capture status separately."
        });
    }
}
