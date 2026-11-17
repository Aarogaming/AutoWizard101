using System.Diagnostics;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>One owned Python process, bounded output, cooperative stop then a hard
/// deadline. The scheduler retains its existing control lease until this exits.</summary>
internal static class WizwalkerWorker
{
    internal const int MaximumJsonBytes = 262144;
    internal const int DefaultCooperativeStopSeconds = 3;
    internal static void ValidateLimits(int maximumSeconds, int maximumResultBytes,
        int cooperativeStopSeconds = DefaultCooperativeStopSeconds)
    {
        if (maximumSeconds is < 1 or > 90) throw new ArgumentOutOfRangeException(nameof(maximumSeconds));
        if (maximumResultBytes is < 1 or > MaximumJsonBytes) throw new ArgumentOutOfRangeException(nameof(maximumResultBytes));
        if (cooperativeStopSeconds is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(cooperativeStopSeconds));
    }
    internal static ProcessStartInfo BuildStartInfo(WizwalkerLaunch launch, string directory)
    {
        var info = new ProcessStartInfo(launch.Python)
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.ArgumentList.Add("-I"); info.ArgumentList.Add("-B");
        info.ArgumentList.Add(launch.Worker); info.ArgumentList.Add("--directory"); info.ArgumentList.Add(directory);
        info.Environment["OMP_NUM_THREADS"] = "1";
        info.Environment["OPENBLAS_NUM_THREADS"] = "1";
        return info;
    }

    internal static async Task<WizwalkerResult> ExecuteAsync(WizwalkerLaunch launch, WizwalkerRequest request,
        OperationContext context, CancellationToken token) =>
        await ExecuteJsonAsync<WizwalkerResult>(launch, request, context, token).ConfigureAwait(false);

    internal static async Task<TResult> ExecuteJsonAsync<TResult>(WizwalkerLaunch launch, object request,
        OperationContext context, CancellationToken token, int maximumSeconds = 18, int maximumResultBytes = 16384,
        Action<string>? traceRecord = null, int cooperativeStopSeconds = DefaultCooperativeStopSeconds) where TResult : class
    {
        ValidateLimits(maximumSeconds, maximumResultBytes, cooperativeStopSeconds);
        if (context.Input?.CanDispatch != true) throw new InvalidOperationException("No shared control owner.");
        using var process = new Process { StartInfo = BuildStartInfo(launch, context.Directory) };
        using var job = new OwnedProcessJob();
        bool started = false;
        bool exited = false;
        var clock = Stopwatch.StartNew();
        using var trace = traceRecord is null ? null : new W101TraceTail(Path.Combine(context.Directory, "ui-observations.jsonl"), traceRecord);
        try
        {
            token.ThrowIfCancellationRequested();
            started = process.Start();
            if (!started) throw new IOException("The client observer did not start.");
            // Child blocks on stdin: it cannot open the client before job attachment.
            job.Attach(process);
            process.PriorityClass = ProcessPriorityClass.BelowNormal;
            var output = DrainAsync(process.StandardOutput);
            var errors = DrainAsync(process.StandardError);
            if (context.Input.CanDispatch != true) throw new InvalidOperationException("Control ownership changed.");
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, WizwalkerOperation.Json))
                .WaitAsync(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
            process.StandardInput.Close();
            long? stopAt = null;
            while (!process.HasExited)
            {
                // Never deliver records after ownership loss/cancellation. The
                // callback remains inside this worker's lifetime and cleanup.
                if (!token.IsCancellationRequested && context.Input.CanDispatch) trace?.Poll();
                bool memoryLimit;
                try { process.Refresh(); memoryLimit = process.WorkingSet64 > 384L * 1024 * 1024; }
                catch (InvalidOperationException) when (process.HasExited) { break; }
                catch (System.ComponentModel.Win32Exception) when (process.HasExited) { break; }
                if (token.IsCancellationRequested || !context.Input.CanDispatch || clock.Elapsed.TotalSeconds >= maximumSeconds || memoryLimit)
                {
                    if (stopAt is null)
                    {
                        stopAt = clock.ElapsedMilliseconds;
                        await File.WriteAllTextAsync(Path.Combine(context.Directory, "stop.request"), "stop").ConfigureAwait(false);
                    }
                    if (clock.ElapsedMilliseconds - stopAt.Value >= cooperativeStopSeconds * 1000L)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                        throw new IOException("Client observer exceeded its shutdown budget.");
                    }
                }
                await Task.Delay(50).ConfigureAwait(false);
            }
            exited = true;
            await Task.WhenAll(output, errors).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            if (process.ExitCode != 0) throw new IOException("Client observer ended without valid disconnect evidence.");
            var path = Path.Combine(context.Directory, "result.json");
            return JsonSerializer.Deserialize<TResult>(GameExperiments.BoundedBytes(path, maximumResultBytes), WizwalkerOperation.Json)
                ?? throw new InvalidDataException("Client observer returned no result.");
        }
        finally
        {
            if (started && !exited)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                }
                finally { context.Input.RequireRecovery("Client observer required forced or unconfirmed shutdown."); }
            }
        }
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        // No accumulated log/backlog or private backend text copied into UI.
        var buffer = new char[512];
        while (await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false) != 0) { }
    }
}
