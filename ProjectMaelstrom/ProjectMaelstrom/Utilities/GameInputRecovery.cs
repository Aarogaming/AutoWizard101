using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record InputRecoveryChallenge(string Id, long IssuedAtMilliseconds);
internal sealed record InputRecoverySample(string ChallengeId, long ObservedAtMilliseconds, long ActivityRevision,
    bool WorkersStopped, bool DesktopAvailable, bool InputStateKnown, bool InputsReleased);
internal sealed record InputRecoveryResult(bool Recovered, string Code, string Message, string? EvidenceDirectory);
internal sealed record InputRecoveryRuntime(Func<long> Clock, Func<TimeSpan, CancellationToken, Task> Delay,
    Action<FileStream, byte> PersistState, Action<string, byte[]> Publish, Action<FileStream>? Close = null);

/// <summary>Trusted application-only cleanup transaction. Verification runs under
/// exclusive input ownership but never gains an input transport or gameplay lease.
/// Saved JSON and game packs cannot supply verification callbacks.</summary>
internal static class GameInputRecovery
{
    internal const int QuietMilliseconds = 1000;
    internal const int MaximumMilliseconds = 10000;
    internal const int FreshMilliseconds = 100;
    private const int MaximumNoticeBytes = 65536;
    private static readonly InputRecoveryRuntime Runtime = new(() => Environment.TickCount64,
        (delay, token) => Task.Delay(delay, token), GameInputLease.PersistState, Publish);

    internal static Task<InputRecoveryResult> RunAsync(string directory,
        Func<InputRecoveryChallenge, CancellationToken, Task<InputRecoverySample>> verify, CancellationToken token) =>
        RunAsync(directory, verify, token, Runtime);

    internal static async Task<InputRecoveryResult> RunAsync(string directory,
        Func<InputRecoveryChallenge, CancellationToken, Task<InputRecoverySample>> verify, CancellationToken token,
        InputRecoveryRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(verify); ArgumentNullException.ThrowIfNull(runtime);
        string? evidence = null;
        GameInputLease.RecoveryReservation? reservation = null;
        using var budget = new CancellationTokenSource(TimeSpan.FromMilliseconds(MaximumMilliseconds));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, budget.Token);
        var stop = cancellation.Token;
        try
        {
            stop.ThrowIfCancellationRequested();
            directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            reservation = GameInputLease.ReserveRecovery(directory, runtime.PersistState, runtime.Close);
            var challenge = new InputRecoveryChallenge(Guid.NewGuid().ToString("N"), runtime.Clock());
            if (challenge.IssuedAtMilliseconds < 0) throw new InvalidDataException("Recovery clock is invalid.");
            var noticePath = Path.Combine(directory, "input-recovery-required.txt");
            var originalNotice = NoticeBytes(noticePath);
            var history = Path.Combine(directory, "recovery-history");
            Directory.CreateDirectory(history); RejectLink(history);
            evidence = Path.Combine(history, challenge.Id);
            Directory.CreateDirectory(evidence); RejectLink(evidence);
            runtime.Publish(Path.Combine(evidence, "original-guard.bin"), reservation.OriginalState);
            if (originalNotice is not null) runtime.Publish(Path.Combine(evidence, "original-warning.txt"), originalNotice);
            runtime.Publish(Path.Combine(evidence, "attempt.json"), Encode(new
            {
                schema_version = 1, attempt_id = challenge.Id, recovery_epoch = reservation.Epoch,
                status = "verification-pending", started_utc = DateTime.UtcNow, challenge,
                original_guard_sha256 = Digest(reservation.OriginalState),
                original_warning_sha256 = originalNotice is null ? null : Digest(originalNotice),
                mode = "exclusive-cleanup-verification-no-gameplay"
            }));

            var first = await SampleAsync(verify, challenge, runtime, stop).ConfigureAwait(false);
            await runtime.Delay(TimeSpan.FromMilliseconds(QuietMilliseconds), stop).WaitAsync(stop).ConfigureAwait(false);
            var second = await SampleAsync(verify, challenge, runtime, stop).ConfigureAwait(false);
            if (second.ObservedAtMilliseconds - first.ObservedAtMilliseconds < QuietMilliseconds ||
                second.ActivityRevision != first.ActivityRevision)
                throw new InvalidDataException("Input activity changed or the quiet interval was too short. Leave controls untouched and try again.");
            runtime.Publish(Path.Combine(evidence, "verification.json"), Encode(new
            {
                schema_version = 1, attempt_id = challenge.Id, recovery_epoch = reservation.Epoch,
                status = "verified-for-commit", first, second,
                limitation = "Verification precedes the clean-state commit. This receipt alone does not enable input or resume a routine."
            }));

            CheckDeadline(challenge, second, runtime.Clock()); stop.ThrowIfCancellationRequested(); reservation.CheckCurrent();
            if (!Same(originalNotice, NoticeBytes(noticePath))) throw new InvalidDataException("The recovery warning changed during verification.");
            if (originalNotice is not null)
            {
                var archived = Path.Combine(evidence, "archived-warning.txt");
                File.Move(noticePath, archived, overwrite: false);
                if (!Same(originalNotice, NoticeBytes(archived))) throw new InvalidDataException("The warning changed while being archived.");
            }
            if (NoticeBytes(noticePath) is not null) throw new InvalidDataException("A new recovery warning arrived before commit.");
            CheckDeadline(challenge, second, runtime.Clock()); stop.ThrowIfCancellationRequested(); reservation.CheckCurrent();
            reservation.CommitAndRelease();
            var closeWarning = reservation.CloseWarning;
            reservation = null;
            if (closeWarning is not null)
                return new(true, "recovered-with-close-warning", "Cleanup was verified and ownership released. Internal handle-close diagnostic: " + closeWarning, evidence);
            return new(true, "recovered", "Cleanup checks passed. Controls are available; no routine was restarted.", evidence);
        }
        catch (OperationCanceledException)
        {
            return new(false, token.IsCancellationRequested ? "cancelled" : "timed-out",
                "Recovery stopped before completion. This check did not re-enable controls.", evidence);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return new(false, error is InvalidOperationException ? "owner-or-recovery-changed" : "verification-or-storage-failed",
                "Controls were not re-enabled. " + Bounded(error.Message), evidence);
        }
        finally
        {
            // Cleanup is fail-closed. Dirty restore failures retain the reservation
            // lock in quarantine; no recovery error is a reason to grant control.
            try { reservation?.Dispose(); }
            catch (Exception) when (reservation is not null)
            { /* The reservation's dirty state/local hold remains authoritative. */ }
        }
    }

    private static async Task<InputRecoverySample> SampleAsync(
        Func<InputRecoveryChallenge, CancellationToken, Task<InputRecoverySample>> verify,
        InputRecoveryChallenge challenge, InputRecoveryRuntime runtime, CancellationToken token)
    {
        var requested = runtime.Clock();
        if (requested < challenge.IssuedAtMilliseconds || requested - challenge.IssuedAtMilliseconds > MaximumMilliseconds)
            throw new InvalidDataException("Recovery clock moved backward or its budget expired.");
        var sample = await verify(challenge, token).WaitAsync(token).ConfigureAwait(false);
        var now = runtime.Clock();
        if (sample is null || sample.ChallengeId != challenge.Id || sample.ObservedAtMilliseconds < requested ||
            sample.ActivityRevision < 0 || !sample.WorkersStopped || !sample.DesktopAvailable || !sample.InputStateKnown || !sample.InputsReleased)
            throw new InvalidDataException("Cleanup could not be verified: workers must be stopped and the accessible desktop must report released inputs.");
        CheckDeadline(challenge, sample, now);
        return sample;
    }

    private static void CheckDeadline(InputRecoveryChallenge challenge, InputRecoverySample sample, long now)
    {
        if (now < challenge.IssuedAtMilliseconds || now - challenge.IssuedAtMilliseconds > MaximumMilliseconds ||
            sample.ObservedAtMilliseconds > now || now - sample.ObservedAtMilliseconds > FreshMilliseconds)
            throw new InvalidDataException("Recovery verification is stale or its clock is invalid. Start a fresh check.");
    }
    private static byte[]? NoticeBytes(string path)
    {
        FileAttributes attributes;
        try { attributes = File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new InvalidDataException("The recovery warning is not a regular file.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumNoticeBytes) throw new InvalidDataException("The recovery warning exceeds the bounded review size.");
        using var output = new MemoryStream();
        var buffer = new byte[4096]; int count;
        while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + count > MaximumNoticeBytes) throw new InvalidDataException("The recovery warning grew beyond the review limit.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    private static bool Same(byte[]? left, byte[]? right) => left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Bounded(string value) => value.Length <= 2048 ? value : value[..2024] + " [diagnostic truncated]";
    private static byte[] Encode<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions { WriteIndented = true });
    private static void RejectLink(string directory)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Recovery evidence cannot use linked directories.");
    }
    internal static void Publish(string path, byte[] bytes)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        output.Write(bytes); output.Flush(flushToDisk: true);
    }
}
