using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectMaelstrom.Utilities;

[JsonConverter(typeof(JsonStringEnumConverter<OperationState>))]
internal enum OperationState { Queued, Running, Completed, Failed, Cancelled, TimedOut, Interrupted, Unknown, CleanupFailed }
internal sealed record OperationRequest(string Id, string Operation, string Version, int TimeoutSeconds,
    bool WritesGame, string Target, IReadOnlyDictionary<string, string> Pins, bool DelegatedInput = false);
internal sealed record OperationOutcome(OperationState State, string Message, string[] Artifacts)
{
    public static OperationOutcome Completed(string message, params string[] artifacts) => new(OperationState.Completed, message, artifacts);
}
internal sealed record OperationReceipt(OperationRequest Request, OperationState State, DateTime UpdatedUtc, string Message, string[] Artifacts, int SchemaVersion = 1);
internal sealed record OperationContext(OperationRequest Request, string Directory, GameInputLease? Input);
internal sealed class OperationTicket(OperationRequest request, Task<OperationReceipt> completion, Action cancel)
{
    public OperationRequest Request { get; } = request;
    public Task<OperationReceipt> Completion { get; } = completion;
    public void Cancel() => cancel();
}

/// <summary>One sequential worker, durable receipts and no automatic restart. Trusted application
/// delegates only, never code from packs. Cancellation retains ownership until the worker exits.</summary>
internal sealed class OperationScheduler : IAsyncDisposable
{
    private sealed record Work(OperationRequest Request, Func<OperationContext, CancellationToken, Task<OperationOutcome>> Execute,
        CancellationTokenSource Cancel, TaskCompletionSource<OperationReceipt> Completion)
    {
        internal readonly object CancellationGate = new();
        internal bool ExecutionFinished;
        internal string? CancellationFailure;
        internal TaskCompletionSource? CancellationCompletion;
        internal long RecoveryGeneration;
        internal int CountedLive;
        internal readonly TaskCompletionSource Drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class RecoveryScope(string directory)
    {
        internal readonly string Directory = directory;
        internal readonly HashSet<OperationScheduler> Schedulers = [];
        internal readonly HashSet<LiveRecoveryHold> Holds = [];
        internal long Generation;
    }
    private static readonly object RecoveryGate = new();
    private static readonly Dictionary<string, RecoveryScope> RecoveryScopes = new(StringComparer.OrdinalIgnoreCase);
    private sealed record StoredJob(string Folder, OperationRequest Request, OperationReceipt? Receipt, OperationJournalState? Journal);
    private readonly object _gate = new();
    private readonly Queue<Work> _queue = new();
    private readonly HashSet<string> _ids = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _directory;
    private readonly string? _leaseDirectory;
    private readonly FileStream _schedulerLease;
    private readonly RecoveryScope _recoveryScope;
    private int _livePending;
    private Work? _active;
    private Task _worker = Task.CompletedTask;
    private bool _closed;
    internal const int Capacity = 32;
    public int Pending { get { lock (_gate) return _queue.Count + (_active == null ? 0 : 1); } }
    public OperationScheduler(string dataRoot, string? leaseDirectory = null)
    {
        _directory = Path.Combine(Path.GetFullPath(dataRoot), "operations"); _leaseDirectory = leaseDirectory;
        Directory.CreateDirectory(_directory);
        _schedulerLease = new FileStream(Path.Combine(_directory, "scheduler.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            // Validate the whole immediate spool before mutating recovery evidence.
            var jobs = JobFolders(_directory).Select(ReadJob).Where(job => job is not null).Cast<StoredJob>().ToArray();
            foreach (var job in jobs)
            {
                if (!_ids.Add(job.Request.Id)) throw new InvalidDataException("Duplicate saved operation ID.");
            }
            foreach (var job in jobs)
            {
                var receipt = job.Receipt;
                if (receipt is null || receipt.State is OperationState.Running or OperationState.Queued)
                {
                    receipt = new(job.Request, OperationState.Interrupted, DateTime.UtcNow,
                        receipt is null ? "Admission was committed without a lifecycle snapshot. No actions were resumed." :
                        "Previous session ended. Inspect evidence and explicitly submit a new run; no actions were resumed.", receipt?.Artifacts ?? []);
                    if (job.Journal is not null) receipt = OperationJournal.Append(job.Folder, receipt);
                    else GameExperiments.WriteAtomic(Path.Combine(job.Folder, "receipt.json"), receipt);
                }
                if (job.Journal is not null) OperationJournal.Project(job.Folder, receipt);
            }
        }
        catch { _schedulerLease.Dispose(); throw; }
        lock (RecoveryGate)
        {
            _recoveryScope = RecoveryScopeFor(leaseDirectory);
            _recoveryScope.Schedulers.Add(this);
        }
    }
    public OperationTicket Submit(OperationRequest request, Func<OperationContext, CancellationToken, Task<OperationOutcome>> execute)
    {
        ArgumentNullException.ThrowIfNull(execute); request = OperationRequestSnapshot.Freeze(request);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (_ids.Contains(request.Id)) throw new InvalidOperationException("This job ID was already submitted. Inspect its receipt instead of replaying it.");
            if (Pending >= Capacity) throw new InvalidOperationException("The operation queue is full (32 jobs). Finish or cancel a job first.");
            var work = new Work(request, execute, new(), new(TaskCreationOptions.RunContinuationsAsynchronously));
            try
            {
                if (request.WritesGame)
                    lock (RecoveryGate)
                    {
                        if (_recoveryScope.Holds.Count != 0)
                            throw new InvalidOperationException("Live operations are paused for input recovery. Start a new run after recovery finishes.");
                        work.RecoveryGeneration = _recoveryScope.Generation;
                        work.CountedLive = 1; Interlocked.Increment(ref _livePending);
                    }
                if (Directory.Exists(Folder(work))) throw new InvalidOperationException("This run folder already exists; use a new run ID.");
                _ids.Add(request.Id);
                Directory.CreateDirectory(Folder(work)); OperationJournal.Create(Folder(work), request);
                Save(work, OperationState.Queued, "Queued; no inputs sent.", []);
            }
            catch { work.Cancel.Dispose(); MarkDrained(work); throw; }
            _queue.Enqueue(work);
            if (_worker.IsCompleted) _worker = Task.Run(PumpAsync);
            return new(request, work.Completion.Task, () => Cancel(work));
        }
    }
    private void Cancel(Work work)
    {
        CancellationTokenSource? active = null;
        lock (_gate)
        {
            if (work.Completion.Task.IsCompleted) return;
            if (_active == work) active = work.Cancel;
            else
            {
                var remaining = _queue.Where(w => w != work).ToArray(); _queue.Clear();
                foreach (var item in remaining) _queue.Enqueue(item);
                try { work.Completion.TrySetResult(Save(work, OperationState.Cancelled, "Cancelled before starting; no actions were run.", [])); }
                catch (Exception error) { work.Completion.TrySetException(error); }
                work.Cancel.Dispose();
                MarkDrained(work);
            }
        }
        // User callbacks may take service/UI locks; never invoke them under the queue lock.
        if (active is not null) CancelSafely(work, active);
    }
    public bool Cancel(string id)
    {
        Work? work; lock (_gate) work = _active?.Request.Id == id ? _active : _queue.FirstOrDefault(w => w.Request.Id == id);
        if (work == null) return false;
        Cancel(work); return true;
    }
    internal static OperationReceipt[] History(string dataRoot)
    {
        var directory = Path.Combine(Path.GetFullPath(dataRoot), "operations");
        return JobFolders(directory).OrderByDescending(folder =>
            {
                var receipt = Path.Combine(folder, "receipt.json");
                var journal = OperationJournal.DirectoryPath(folder);
                return new[] { File.Exists(receipt) ? File.GetLastWriteTimeUtc(receipt) : DateTime.MinValue,
                    Directory.Exists(journal) ? Directory.GetLastWriteTimeUtc(journal) : DateTime.MinValue }.Max();
            }).Take(100).Select(ReadJob).Where(job => job is not null).Select(job => job!.Receipt ??
                new(job.Request, OperationState.Unknown, Directory.GetLastWriteTimeUtc(OperationJournal.DirectoryPath(job.Folder)),
                    "Admission exists; no lifecycle snapshot was committed. Recovery has not run.", [])).ToArray();
    }
    private static IEnumerable<string> JobFolders(string directory)
    {
        if (!Directory.Exists(directory)) yield break;
        // Artifacts may contain their own receipt.json. Only this scheduler's
        // immediate GUID job folders belong to its journal/recovery boundary.
        foreach (var folder in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
        {
            if (!Guid.TryParseExact(Path.GetFileName(folder), "N", out _)) continue;
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Operation job folders cannot be links.");
            var path = Path.Combine(folder, "receipt.json");
            var journal = OperationJournal.DirectoryPath(folder);
            if (!File.Exists(path) && !Path.Exists(journal)) continue;
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Operation receipts cannot be links.");
            yield return folder;
        }
    }
    private static StoredJob? ReadJob(string folder)
    {
        var journal = OperationJournal.Read(folder);
        if (journal is not null) return new(folder, journal.Request, journal.Latest, journal);
        var path = Path.Combine(folder, "receipt.json");
        if (!File.Exists(path)) return null;
        if (Path.Exists(OperationJournal.DirectoryPath(folder)))
            throw new InvalidDataException("An incomplete journal cannot be treated as a legacy receipt-only job.");
        var receipt = ReadReceipt(path);
        return new(folder, receipt.Request, receipt, null);
    }
    private static OperationReceipt ReadReceipt(string path)
    {
        OperationReceipt receipt;
        try
        {
            receipt = JsonSerializer.Deserialize<OperationReceipt>(GameExperiments.BoundedBytes(path, 1048576))
                ?? throw new InvalidDataException("Missing operation receipt.");
        }
        catch (JsonException error) { throw new InvalidDataException("Invalid operation receipt.", error); }
        if (receipt.SchemaVersion != 1 || receipt.Request is null || !Enum.IsDefined(receipt.State))
            throw new InvalidDataException("Unsupported or invalid operation receipt.");
        Validate(receipt.Request);
        if (!string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), receipt.Request.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Operation receipt does not match its job folder.");
        return receipt;
    }
    internal static void Validate(OperationRequest request)
    {
        if (request is null || !Guid.TryParseExact(request.Id, "N", out _) || string.IsNullOrWhiteSpace(request.Operation) || request.Operation.Length > 120 ||
            string.IsNullOrWhiteSpace(request.Version) || request.Version.Length > 256 || request.Target is { Length: > 32768 } ||
            request.TimeoutSeconds is < 1 or > 1800 || request.Pins == null || request.Pins.Count > 32 ||
            (request.WritesGame && string.IsNullOrWhiteSpace(request.Target))) throw new InvalidDataException("Operation needs an ID, version, finite 1–1800 second budget and explicit live target.");
        foreach (var pin in request.Pins)
            if (string.IsNullOrEmpty(pin.Key) || pin.Key.Length > 32768 || !Path.IsPathFullyQualified(pin.Key) || pin.Value is not { Length: 64 } || !pin.Value.All(Uri.IsHexDigit))
                throw new InvalidDataException("Operation pins need absolute file paths and SHA-256 digests.");
    }
    private string Folder(Work work) => Path.Combine(_directory, work.Request.Id);
    private OperationReceipt Save(Work work, OperationState state, string message, string[] artifacts)
    {
        var receipt = new OperationReceipt(work.Request, state, DateTime.UtcNow, message, artifacts);
        receipt = OperationJournal.Append(Folder(work), receipt);
        OperationJournal.Project(Folder(work), receipt);
        return receipt;
    }
    private async Task PumpAsync()
    {
        while (true)
        {
            Work work;
            lock (_gate)
            {
                if (_queue.Count == 0) { _active = null; _worker = Task.CompletedTask; return; }
                work = _queue.Dequeue(); _active = work;
            }
            using var deadline = new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, work.Cancel.Token);
            using var timer = new System.Threading.Timer(_ => CancelSafely(work, deadline), null,
                TimeSpan.FromSeconds(work.Request.TimeoutSeconds), Timeout.InfiniteTimeSpan);
            OperationOutcome? outcome = null;
            GameInputLease? input = null;
            try
            {
                bool recoveryCancelled;
                lock (RecoveryGate) recoveryCancelled = work.Request.WritesGame && work.RecoveryGeneration != _recoveryScope.Generation;
                if (recoveryCancelled) CancelSafely(work, work.Cancel);
                linked.Token.ThrowIfCancellationRequested();
                foreach (var pin in work.Request.Pins)
                    if (!GameExperiments.Digest(pin.Key).Equals(pin.Value, StringComparison.OrdinalIgnoreCase)) throw new IOException("An operation input changed after it was queued: " + Path.GetFileName(pin.Key));
                using var inputOwner = work.Request.WritesGame && !work.Request.DelegatedInput ? GameInputLease.Acquire(work.Request.Operation, _leaseDirectory) : null;
                input = inputOwner;
                using var scope = GameInputLease.EnterScope(input);
                try
                {
                    Save(work, OperationState.Running, "Running with pinned inputs and a finite budget.", []);
                    outcome = ValidateOutcome(work.Request, await work.Execute(new(work.Request, Folder(work), input), linked.Token).ConfigureAwait(false));
                    if (outcome.State == OperationState.CleanupFailed) input?.RequireRecovery(outcome.Message);
                }
                finally { await FinishCancellationAsync(work, timer).ConfigureAwait(false); }
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            { outcome = new(deadline.IsCancellationRequested ? OperationState.TimedOut : OperationState.Cancelled, "Stopped without automatically repeating actions.", []); }
            catch (Exception error) { outcome = new(outcome?.State == OperationState.CleanupFailed ? OperationState.CleanupFailed : OperationState.Failed, Diagnostic(error.Message), outcome?.Artifacts ?? []); }
            await FinishCancellationAsync(work, timer).ConfigureAwait(false);
            outcome ??= new(OperationState.Failed, "Worker ended without an outcome.", []);
            // A worker's verdict or cancellation cannot clear uncertain input
            // cleanup. The lease latches this before attempting diagnostic I/O.
            if (input?.RecoveryRequired == true)
                outcome = new(OperationState.CleanupFailed,
                    Diagnostic("Input recovery required; gameplay remains blocked. " + outcome.Message +
                        (input.RecoveryNoticeFailure is { } noticeFailure ? " Recovery warning could not be saved: " + noticeFailure : "")), outcome.Artifacts);
            if (linked.IsCancellationRequested && outcome.State != OperationState.CleanupFailed)
                outcome = new(deadline.IsCancellationRequested ? OperationState.TimedOut : OperationState.Cancelled,
                    "Run stopped; inspect artifacts for partial results.", outcome.Artifacts);
            if (work.CancellationFailure is { } failure)
                outcome = outcome with { Message = Diagnostic(outcome.Message + " Cancellation callback failed: " + failure) };
            outcome = ValidateOutcome(work.Request, outcome);
            try { work.Completion.TrySetResult(Save(work, outcome.State, outcome.Message, outcome.Artifacts)); }
            catch (Exception error) { work.Completion.TrySetException(error); }
            lock (_gate) { work.Cancel.Dispose(); _active = null; MarkDrained(work); }
        }
    }
    private void MarkDrained(Work work)
    {
        if (Interlocked.Exchange(ref work.CountedLive, 0) != 0) Interlocked.Decrement(ref _livePending);
        work.Drained.TrySetResult();
    }
    private static OperationOutcome ValidateOutcome(OperationRequest request, OperationOutcome? outcome)
    {
        try
        {
            if (outcome is null || !OperationJournal.IsTerminal(outcome.State)) throw new InvalidDataException("Worker did not return a defined terminal outcome.");
            outcome = outcome with { Artifacts = outcome.Artifacts?.ToArray()! };
            OperationJournal.ValidateReceipt(new(request, outcome.State, DateTime.UtcNow, outcome.Message, outcome.Artifacts));
            return outcome;
        }
        catch (InvalidDataException error)
        {
            return new(outcome?.State == OperationState.CleanupFailed ? OperationState.CleanupFailed : OperationState.Failed,
                Diagnostic("Worker returned an invalid outcome: " + error.Message), []);
        }
    }
    private static string Diagnostic(string message)
    {
        const string suffix = " [diagnostic truncated]";
        return message.Length <= OperationJournal.MaximumMessageCharacters ? message :
            message[..(OperationJournal.MaximumMessageCharacters - suffix.Length)] + suffix;
    }
    private static void CancelSafely(Work work, CancellationTokenSource source)
    {
        TaskCompletionSource completion;
        lock (work.CancellationGate)
        {
            if (work.ExecutionFinished || work.CancellationCompletion is not null) return;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            work.CancellationCompletion = completion;
        }
        try { source.Cancel(); }
        catch (ObjectDisposedException) { /* Worker cleanup already ended. */ }
        catch (Exception error) { work.CancellationFailure = Diagnostic(error.Message); }
        finally { completion.TrySetResult(); }
    }
    private static async Task FinishCancellationAsync(Work work, System.Threading.Timer timer)
    {
        timer.Dispose();
        Task? callbacks;
        lock (work.CancellationGate)
        { work.ExecutionFinished = true; callbacks = work.CancellationCompletion?.Task; }
        // Cancellation can resume an awaited worker inline on the cancelling
        // thread. Awaiting a separate asynchronous barrier prevents reentrant
        // cleanup from releasing input before the remaining callbacks finish.
        if (callbacks is not null) await callbacks.ConfigureAwait(false);
    }
    public async ValueTask DisposeAsync()
    {
        Task worker;
        Work[] pending;
        lock (_gate)
        {
            if (_closed) return;
            _closed = true; pending = _queue.Concat(_active == null ? [] : new[] { _active }).ToArray(); worker = _worker;
        }
        foreach (var work in pending) Cancel(work);
        try { await worker.ConfigureAwait(false); }
        finally
        {
            _schedulerLease.Dispose();
            lock (RecoveryGate)
            {
                _recoveryScope.Schedulers.Remove(this);
                RemoveUnusedScope(_recoveryScope);
            }
        }
    }

    private static RecoveryScope RecoveryScopeFor(string? leaseDirectory)
    {
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(leaseDirectory ?? GameInputLease.DirectoryPath));
        if (!RecoveryScopes.TryGetValue(directory, out var scope))
            RecoveryScopes.Add(directory, scope = new(directory));
        return scope;
    }
    private static void RemoveUnusedScope(RecoveryScope scope)
    {
        if (scope.Holds.Count == 0 && scope.Schedulers.Count == 0) RecoveryScopes.Remove(scope.Directory);
    }

    /// <summary>Stops new live admission across every scheduler sharing this input
    /// directory. This is not input authority and does not clear any recovery state.
    /// Already queued live jobs are invalidated permanently, even if the hold is disposed.
    /// Call StopAndWaitAsync to cancel active work and await actual cleanup.</summary>
    internal static LiveRecoveryHold PauseLiveForRecovery(string? leaseDirectory = null) => LiveRecoveryHold.Create(leaseDirectory);

    internal sealed class LiveRecoveryHold : IDisposable
    {
        private readonly RecoveryScope _scope;
        private readonly long _generation;
        private Task<bool>? _stop;
        private bool _disposed;
        private LiveRecoveryHold(RecoveryScope scope, long generation) { _scope = scope; _generation = generation; }
        internal static LiveRecoveryHold Create(string? leaseDirectory)
        {
            lock (RecoveryGate)
            {
                var scope = RecoveryScopeFor(leaseDirectory);
                scope.Generation = checked(scope.Generation + 1);
                var hold = new LiveRecoveryHold(scope, scope.Generation);
                scope.Holds.Add(hold);
                return hold;
            }
        }
        public bool IsHeld { get { lock (RecoveryGate) return !_disposed; } }
        /// <summary>No admitted live callback remains in this scope. This does not
        /// prove input release, desktop readiness or permission to clear recovery.</summary>
        public bool IsDrained
        {
            get { lock (RecoveryGate) return !_disposed && _scope.Schedulers.All(scheduler => Volatile.Read(ref scheduler._livePending) == 0); }
        }
        /// <summary>Cancel live work without disposing schedulers or stopping offline
        /// jobs. Timeout returns false; cancellation stops waiting only. The hold and
        /// in-progress cleanup remain intact until their owners finish explicitly.</summary>
        public async Task<bool> StopAndWaitAsync(TimeSpan timeout, CancellationToken token = default)
        {
            if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30)) throw new ArgumentOutOfRangeException(nameof(timeout));
            token.ThrowIfCancellationRequested();
            Task<bool> stop;
            lock (RecoveryGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                stop = _stop ??= Task.Run(StopAsync);
            }
            try { return await stop.WaitAsync(timeout, token).ConfigureAwait(false) && IsDrained; }
            catch (TimeoutException) { return false; }
        }
        private async Task<bool> StopAsync()
        {
            OperationScheduler[] schedulers;
            lock (RecoveryGate) schedulers = _scope.Schedulers.ToArray();
            var groups = new List<(OperationScheduler Scheduler, Work[] Work)>();
            foreach (var scheduler in schedulers)
                lock (scheduler._gate)
                {
                    var work = scheduler._queue.Concat(scheduler._active is null ? [] : new[] { scheduler._active })
                        .Where(item => item.Request.WritesGame && item.RecoveryGeneration < _generation && !item.Drained.Task.IsCompleted).ToArray();
                    if (work.Length != 0) groups.Add((scheduler, work));
                }
            // One cancellation lane per scheduler; a blocked callback cannot keep
            // another scheduler's queued jobs from receiving their stop request.
            var cancellations = groups.Select(group => Task.Run(() =>
            {
                foreach (var work in group.Work) group.Scheduler.Cancel(work);
            })).ToArray();
            await Task.WhenAll(cancellations).ConfigureAwait(false);
            await Task.WhenAll(groups.SelectMany(group => group.Work).Select(work => work.Drained.Task)).ConfigureAwait(false);
            return IsDrained;
        }
        public void Dispose()
        {
            lock (RecoveryGate)
            {
                if (_disposed) return;
                _disposed = true; _scope.Holds.Remove(this); RemoveUnusedScope(_scope);
            }
        }
    }
}

internal static class ApplicationOperations
{
    private static readonly ConcurrentDictionary<string, Lazy<OperationScheduler>> Schedulers = new(StringComparer.OrdinalIgnoreCase);
    public static OperationScheduler For(string dataRoot) => Schedulers.GetOrAdd(Path.GetFullPath(dataRoot), root => new(() => new OperationScheduler(root))).Value;
    public static async Task StopAllAsync()
    {
        foreach (var scheduler in Schedulers.Values.Where(s => s.IsValueCreated)) await scheduler.Value.DisposeAsync().ConfigureAwait(false);
        Schedulers.Clear();
    }
}
