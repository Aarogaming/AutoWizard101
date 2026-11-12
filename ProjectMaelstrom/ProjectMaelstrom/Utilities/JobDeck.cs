using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectMaelstrom.Utilities;

internal sealed record JobCondition(string Fact, string Operator, JsonElement Value);
internal sealed record JobFilter(string Fact, string[] Include, string[] Exclude);
internal sealed record JobCardSpec(string Id, string RoutineId, string Title, bool InDeck = false,
    int Priority = 50, int Runs = 1, int AttemptLimit = 3, int CooldownSeconds = 5,
    Dictionary<string, string>? Settings = null, JobCondition[]? Conditions = null, JobFilter[]? Filters = null);
internal sealed record JobDeckDocument(string Name, string GameId, JobCardSpec[] Cards,
    int AttemptLimit = 50, int DurationSeconds = 600, int SchemaVersion = 1);
internal sealed record JobFacts(string GameId, string TargetId, DateTime ObservedUtc,
    Dictionary<string, JsonElement> Values, string Source);
internal sealed record JobCardProgress(int Attempts = 0, int Completed = 0, DateTime? LastAttemptUtc = null,
    string Status = "Waiting", string? OperationId = null, string? Reason = null);
internal sealed record JobDeckState(JobDeckDocument Deck, long Revision, string Status,
    Dictionary<string, JobCardProgress> Progress, string? ActiveCard = null, string? ActiveOperation = null,
    DateTime? StartedUtc = null, string? LastRunRequest = null, string? Error = null, string[]? RunRequests = null,
    bool RecoveryRequired = false, string? PendingRunRequest = null);
internal sealed record JobOption(string Name, string Label, string Kind, bool Required = false, string? Default = null);
internal sealed record JobRoutineInfo(string Id, string Title, string Collection, string Icon, string GameId,
    string Qualification, bool Available, bool WritesGame, JobOption[] Options, string Description);
internal sealed record JobEligibility(string CardId, bool Ready, string Reason);
internal sealed record PreparedDeckJob(OperationRequest Request,
    Func<OperationContext, CancellationToken, Task<OperationOutcome>> Execute);
internal sealed record JobRoutine(JobRoutineInfo Info, Action<JobCardSpec> Validate,
    Func<JobCardSpec, JobFacts?, PreparedDeckJob> Prepare, Func<JobCardSpec, JobFacts?, string?>? Eligibility = null, IReadOnlyList<JobFactDefinition>? Facts = null);

/// <summary>Data-only cards are configuration, never executable code or permission.
/// Missing facts defer; unsupported routines remain visible without dispatch.</summary>
internal static class JobDeckRules
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 24
    };
    internal static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value, Json), Json)!;
    internal static void Validate(JobDeckDocument deck, IReadOnlyDictionary<string, JobRoutine> registry)
    {
        if (deck.SchemaVersion != 1 || string.IsNullOrWhiteSpace(deck.Name) || deck.Name.Length > 100 ||
            string.IsNullOrWhiteSpace(deck.GameId) || deck.GameId.Length > 64 || deck.Cards is null || deck.Cards.Length > 64 ||
            deck.AttemptLimit is < 1 or > 1000 || deck.DurationSeconds is < 1 or > 3600 ||
            JsonSerializer.SerializeToUtf8Bytes(deck, Json).Length > 65536)
            throw new InvalidDataException("A deck needs a name, game, up to 64 cards and finite run budgets (maximum one hour). ");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in deck.Cards)
        {
            if (card is null || !Guid.TryParseExact(card.Id, "N", out _) || !ids.Add(card.Id) ||
                string.IsNullOrWhiteSpace(card.Title) || card.Title.Length > 100 ||
                card.Priority is < 0 or > 999 || card.Runs is < 1 or > 100 ||
                card.AttemptLimit < card.Runs || card.AttemptLimit > 100 || card.CooldownSeconds is < 1 or > 3600 ||
                !registry.TryGetValue(card.RoutineId, out var routine) ||
                routine.Info.GameId != "*" && routine.Info.GameId != deck.GameId)
                throw new InvalidDataException("Invalid card, routine, game, copies or attempt limit.");
            var settings = card.Settings ?? [];
            if (settings.Count > 16 || settings.Any(p => p.Value is null || p.Value.Length > 4096 || !routine.Info.Options.Any(o => o.Name == p.Key)))
                throw new InvalidDataException("Use only the routine's documented settings.");
            foreach (var option in routine.Info.Options.Where(o => o.Required && card.InDeck))
                if (string.IsNullOrWhiteSpace(settings.GetValueOrDefault(option.Name)))
                    throw new InvalidDataException($"{card.Title}: {option.Label} is required.");
            foreach (var option in routine.Info.Options.Where(o => o.Kind.StartsWith("choice:", StringComparison.Ordinal)))
                if (settings.TryGetValue(option.Name, out var choice) && !option.Kind[7..].Split(',').Contains(choice))
                    throw new InvalidDataException($"{card.Title}: choose one of {option.Kind[7..]} for {option.Label}.");
            if ((card.Conditions?.Length ?? 0) > 16 || (card.Filters?.Length ?? 0) > 8)
                throw new InvalidDataException("Too many card conditions or filters.");
            foreach (var condition in card.Conditions ?? [])
            {
                CheckFact(condition.Fact);
                JobFactCatalog.ValidateCondition(condition, routine.Facts);
                if (condition.Operator is not ("eq" or "gte" or "lte") ||
                    condition.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) ||
                    condition.Value.GetRawText().Length > 512 ||
                    condition.Operator != "eq" && !FiniteNumber(condition.Value, out _))
                    throw new InvalidDataException("Use eq, gte or lte with a finite scalar value.");
            }
            foreach (var filter in card.Filters ?? [])
            {
                CheckFact(filter.Fact);
                JobFactCatalog.ValidateFilter(filter, routine.Facts);
                if (filter.Include is null || filter.Exclude is null || filter.Include.Length + filter.Exclude.Length > 64 ||
                    filter.Include.Concat(filter.Exclude).Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 256))
                    throw new InvalidDataException("Filters need bounded exact values.");
            }
            if (card.InDeck) routine.Validate(card);
        }
    }
    private static void CheckFact(string fact)
    {
        if (string.IsNullOrWhiteSpace(fact) || fact.Length > 80 || fact.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_')))
            throw new InvalidDataException("Use a registered-style fact name, such as energy or quest.type.");
    }
    private static bool FiniteNumber(JsonElement value, out double number)
    {
        number = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out number) && double.IsFinite(number);
    }
    internal static string? FactBlock(JobCardSpec card, string game, JobFacts? facts, DateTime now, bool needsTarget, IReadOnlyList<JobFactDefinition>? definitions = null)
    {
        if (!needsTarget && (card.Conditions?.Length ?? 0) == 0 && (card.Filters?.Length ?? 0) == 0) return null;
        if (facts is null || facts.GameId != game || string.IsNullOrWhiteSpace(facts.TargetId) ||
            now - facts.ObservedUtc > TimeSpan.FromSeconds(2) || facts.ObservedUtc > now || facts.Values is null)
            return "Waiting for fresh observations of the selected game.";
        foreach (var condition in card.Conditions ?? [])
        {
            if (!facts.Values.TryGetValue(condition.Fact, out var value) || !JobFactCatalog.ValueMatches(condition.Fact, value, definitions)) return $"Unknown: {condition.Fact}.";
            var matched = condition.Operator == "eq" ? value.ValueKind == condition.Value.ValueKind &&
                (value.ValueKind == JsonValueKind.Number ? FiniteNumber(value, out var a) && FiniteNumber(condition.Value, out var b) && a == b : value.ToString() == condition.Value.ToString()) :
                FiniteNumber(value, out var n) && FiniteNumber(condition.Value, out var limit) && (condition.Operator == "gte" ? n >= limit : n <= limit);
            if (!matched) return $"Condition unmet: {condition.Fact} {condition.Operator} {condition.Value}.";
        }
        foreach (var filter in card.Filters ?? [])
        {
            if (filter.Include.Length + filter.Exclude.Length == 0) continue;
            if (!facts.Values.TryGetValue(filter.Fact, out var value) || value.ValueKind != JsonValueKind.String)
                return $"Unknown filter value: {filter.Fact}.";
            var text = value.GetString()!;
            if (filter.Exclude.Contains(text, StringComparer.OrdinalIgnoreCase) ||
                filter.Include.Length > 0 && !filter.Include.Contains(text, StringComparer.OrdinalIgnoreCase))
                return $"Outside selected scope: {filter.Fact}.";
        }
        return null;
    }
}

/// <summary>One bounded deck above OperationScheduler. It does not own or send input.
/// Saving/editing never starts work. Deferred cards consume no attempts or copies.
/// Restarts preserve counters and interrupt runs; they never resume gameplay.</summary>
internal sealed class JobDeckService : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly OperationScheduler _scheduler;
    private readonly Dictionary<string, JobRoutine> _registry;
    private readonly Func<JobFacts?> _observe;
    private readonly Func<DateTime> _now;
    private readonly string _path;
    private readonly FileStream _lease;
    private JobDeckState _state;
    private OperationTicket? _active;
    private Task _completion = Task.CompletedTask;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _loop;
    private bool _continuous;
    private bool _closed;
    private bool _storageFailed;
    internal JobDeckService(string dataRoot, OperationScheduler scheduler, IEnumerable<JobRoutine> registry,
        JobDeckDocument initial, Func<JobFacts?>? observe = null, Func<DateTime>? now = null)
    {
        _scheduler = scheduler; _registry = registry.Select(r => r with { Facts = JobFactCatalog.Freeze(r.Facts) }).ToDictionary(r => r.Info.Id, StringComparer.Ordinal);
        _observe = observe ?? (() => null); _now = now ?? (() => DateTime.UtcNow);
        var folder = Path.Combine(dataRoot, "job-decks"); Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "active.json");
        _lease = new FileStream(Path.Combine(folder, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
        _state = File.Exists(_path) ? JsonSerializer.Deserialize<JobDeckState>(GameExperiments.BoundedBytes(_path, 262144), JobDeckRules.Json)
            ?? throw new InvalidDataException("Deck file is empty.") : new(initial, 0, "Stopped", []);
        JobDeckRules.Validate(_state.Deck, _registry);
        if (_state.Progress is null || _state.Progress.Count > 128 || _state.Progress.Any(p => !Guid.TryParseExact(p.Key, "N", out _) || p.Value.Attempts < 0 || p.Value.Completed < 0 || p.Value.Completed > p.Value.Attempts))
            throw new InvalidDataException("Invalid saved deck progress.");
        if ((_state.RunRequests?.Length ?? 0) > 1024 || (_state.RunRequests ?? []).Any(id => !Guid.TryParseExact(id, "N", out _)))
            throw new InvalidDataException("Invalid saved request history.");
        _storageFailed = _state.RecoveryRequired || _state.Status is "StorageError" or "OutcomeUnknown";
        if (_state.Status is "Running" or "Waiting" or "Observing" || _state.ActiveOperation is not null || _state.PendingRunRequest != null)
        {
            if (_state.ActiveCard is { } card) _state.Progress[card] = _state.Progress.GetValueOrDefault(card, new()) with { Status = "Interrupted", Reason = "Application stopped; inspect the operation receipt before resetting this card." };
            Save(_state with { Status = "Interrupted", ActiveCard = null, ActiveOperation = null, PendingRunRequest = null });
        }
        _loop = Task.Run(PumpAsync);
        }
        catch { _lease.Dispose(); _lifetime.Dispose(); throw; }
    }
    internal JobRoutineInfo[] Catalog => _registry.Values.Select(r => r.Info).ToArray();
    /// <summary>Snapshots of trusted standard and extension vocabulary, scoped by routine.</summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<JobFactDefinition>> FactDefinitions =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<JobFactDefinition>>(
            _registry.ToDictionary(p => p.Key,
                p => (IReadOnlyList<JobFactDefinition>)Array.AsReadOnly(JobFactCatalog.All.Concat(p.Value.Facts ?? []).ToArray()),
                StringComparer.Ordinal));
    internal JobFacts? Facts => _observe() is { } facts ? JobDeckRules.Copy(facts) : null;
    internal JobDeckState State { get { lock (_gate) return JobDeckRules.Copy(_state); } }
    internal bool IsBusy { get { lock (_gate) return _active != null; } }
    internal Task DrainAsync() { lock (_gate) return _completion; }
    internal JobEligibility[] Preview(JobFacts? simulation = null)
    {
        lock (_gate) return Evaluate(simulation ?? _observe()).ToArray();
    }
    private IEnumerable<JobEligibility> Evaluate(JobFacts? facts)
    {
        foreach (var card in _state.Deck.Cards.OrderBy(c => c.Priority))
        {
            var info = _registry[card.RoutineId].Info;
            var progress = _state.Progress.GetValueOrDefault(card.Id, new());
            string? reason = !card.InDeck ? "In collection; not dealt." : !info.Available ? info.Qualification :
                progress.Completed >= card.Runs ? "All copies completed." : progress.Attempts >= card.AttemptLimit ? "Attempt limit reached." :
                progress.LastAttemptUtc is { } last && _now() - last < TimeSpan.FromSeconds(card.CooldownSeconds) ? "Cooling down." :
                JobDeckRules.FactBlock(card, _state.Deck.GameId, facts, _now(), info.WritesGame, _registry[card.RoutineId].Facts) ?? _registry[card.RoutineId].Eligibility?.Invoke(card, facts);
            yield return new(card.Id, reason is null, reason ?? "Ready");
        }
    }
    internal JobDeckState Replace(JobDeckDocument document, long expectedRevision)
    {
        var frozen = JobDeckRules.Copy(document); JobDeckRules.Validate(frozen, _registry);
        lock (_gate)
        {
            CheckEditable(expectedRevision);
            // Editing is not a reset. Unchanged and changed card IDs retain consumed budgets.
            // Keep tombstones for removed cards: swapping cards must not refill the run budget.
            var progress = _state.Progress.ToDictionary();
            Save(_state with { Deck = frozen, Revision = _state.Revision + 1, Progress = progress, Status = "Stopped", Error = null });
            return State;
        }
    }
    internal JobDeckState Reset(long expectedRevision)
    {
        lock (_gate)
        {
            CheckEditable(expectedRevision);
            Save(_state with { Progress = [], Revision = _state.Revision + 1, StartedUtc = null, Status = "Stopped", Error = null });
            return State;
        }
    }
    private void CheckEditable(long revision)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        if (_storageFailed || _active != null || _continuous || _state.PendingRunRequest != null || revision != _state.Revision)
            throw new InvalidOperationException("Stop the deck and reload its current revision before editing or resetting.");
    }
    internal JobDeckState Start(string requestId, bool continuous)
    {
        lock (_gate)
        {
            if ((_state.RunRequests ?? []).Contains(requestId, StringComparer.OrdinalIgnoreCase)) return State;
            var state = ReserveStart(requestId);
            if (state.PendingRunRequest != requestId.ToLowerInvariant()) return state;
            return CompleteReservedStart(requestId, state.Revision, continuous);
        }
    }
    internal JobDeckState ReserveStart(string requestId)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (!Guid.TryParseExact(requestId, "N", out _)) throw new ArgumentException("A new request ID (32 hex digits) is required.");
            requestId = Guid.ParseExact(requestId, "N").ToString("N");
            if ((_state.RunRequests ?? []).Contains(requestId, StringComparer.OrdinalIgnoreCase) || string.Equals(_state.LastRunRequest, requestId, StringComparison.OrdinalIgnoreCase)) return State;
            if ((_state.RunRequests?.Length ?? 0) >= 1024) throw new InvalidOperationException("This deck session reached its request-history limit. Archive its saved state before creating a new session.");
            if (_storageFailed || _active != null || _continuous || _state.PendingRunRequest != null) throw new InvalidOperationException("The deck is busy or its storage needs attention.");
            if (_state.Progress.Values.Sum(p => p.Attempts) >= _state.Deck.AttemptLimit ||
                _state.StartedUtc is { } start && _now() >= start.AddSeconds(_state.Deck.DurationSeconds))
            { Save(_state with { Status = "BudgetReached" }); return State; }
            Save(_state with { Status = "Observing", PendingRunRequest = requestId, LastRunRequest = requestId,
                RunRequests = [.. _state.RunRequests ?? [], requestId], StartedUtc = _state.StartedUtc ?? _now(), Error = null });
            return State;
        }
    }
    internal JobDeckState CompleteReservedStart(string requestId, long revision, bool continuous)
    {
        lock (_gate)
        {
            if (_state.PendingRunRequest != requestId.ToLowerInvariant() || _state.Revision != revision || _storageFailed || _closed)
                throw new InvalidOperationException("This start request was cancelled or its deck changed.");
            Save(_state with { Status = "Running", PendingRunRequest = null }); _continuous = continuous;
            Advance(); return State;
        }
    }
    private void Advance()
    {
        if (_active != null || _storageFailed || _closed) return;
        if (_state.StartedUtc is { } start && _now() - start >= TimeSpan.FromSeconds(_state.Deck.DurationSeconds) ||
            _state.Progress.Values.Sum(p => p.Attempts) >= _state.Deck.AttemptLimit)
        { _continuous = false; Save(_state with { Status = "BudgetReached" }); return; }
        var facts = _observe();
        var next = Evaluate(facts).FirstOrDefault(e => e.Ready);
        if (next is null)
        {
            var remaining = _state.Deck.Cards.Where(c => c.InDeck).Any(c =>
                _registry[c.RoutineId].Info.Available && _state.Progress.GetValueOrDefault(c.Id, new()).Completed < c.Runs &&
                _state.Progress.GetValueOrDefault(c.Id, new()).Attempts < c.AttemptLimit);
            if (!remaining) _continuous = false;
            var unfinished = _state.Deck.Cards.Where(c => c.InDeck).Any(c => _state.Progress.GetValueOrDefault(c.Id, new()).Completed < c.Runs);
            var status = remaining && _continuous ? "Waiting" : remaining ? "Deferred" : unfinished ? "NoPlayableCards" : "Finished";
            if (_state.Status != status) Save(_state with { Status = status });
            return;
        }
        var card = _state.Deck.Cards.Single(c => c.Id == next.CardId);
        var routine = _registry[card.RoutineId];
        var progress = _state.Progress.GetValueOrDefault(card.Id, new());
        try
        {
            // Reserve the attempt durably before preparing/admitting any work.
            if (!_state.Progress.ContainsKey(card.Id) && _state.Progress.Count >= 128)
                throw new InvalidOperationException("This run reached its card history limit. Start an explicit new run before adding more cards.");
            _state.Progress[card.Id] = progress with { Attempts = progress.Attempts + 1, LastAttemptUtc = _now(), Status = "Preparing" };
            Save(_state with { ActiveCard = card.Id });
            var prepared = routine.Prepare(JobDeckRules.Copy(card), facts);
            if (prepared.Request.WritesGame != routine.Info.WritesGame) throw new InvalidDataException("Adapter capability mismatch.");
            Save(_state with { ActiveOperation = prepared.Request.Id });
            var secondsLeft = Math.Max(1, (int)Math.Ceiling((_state.StartedUtc!.Value.AddSeconds(_state.Deck.DurationSeconds) - _now()).TotalSeconds));
            var admittedRevision = _state.Revision;
            var admittedAt = _state.Progress[card.Id].LastAttemptUtc;
            _active = _scheduler.Submit(prepared.Request with { TimeoutSeconds = Math.Min(prepared.Request.TimeoutSeconds, secondsLeft) }, async (context, token) =>
            {
                token.ThrowIfCancellationRequested();
                var admission = Path.Combine(context.Directory, "job-card.json");
                GameExperiments.WriteAtomic(admission, new { schemaVersion = 1, card, deckRevision = admittedRevision,
                    gameId = _state.Deck.GameId, sourceFacts = facts, operationId = context.Request.Id,
                    qualification = routine.Info.Qualification, requestedAtUtc = admittedAt });
                if (_now() >= _state.StartedUtc!.Value.AddSeconds(_state.Deck.DurationSeconds))
                    return new(OperationState.TimedOut, "The deck's absolute deadline elapsed while queued.", [admission]);
                var latest = _observe();
                var blocked = JobDeckRules.FactBlock(card, _state.Deck.GameId, latest, _now(), routine.Info.WritesGame, routine.Facts) ?? routine.Eligibility?.Invoke(card, latest);
                if (blocked != null || routine.Info.WritesGame && latest?.TargetId != facts?.TargetId)
                    return new(OperationState.Failed, blocked ?? "Selected target changed while queued.", [admission]);
                var outcome = await prepared.Execute(context, token).ConfigureAwait(false);
                return outcome with { Artifacts = new[] { admission }.Concat(outcome.Artifacts).ToArray() };
            });
            var ticket = _active;
            _completion = Task.Run(() => FinishAsync(card, ticket));
            _state.Progress[card.Id] = _state.Progress[card.Id] with { Status = "Running", OperationId = prepared.Request.Id };
            Save(_state with { Status = "Running" });
        }
        catch (Exception ex)
        {
            _active?.Cancel(); _continuous = false;
            if (_state.Progress.ContainsKey(card.Id)) _state.Progress[card.Id] = _state.Progress[card.Id] with { Status = "Failed", Reason = ex.Message };
            if (!_storageFailed) Save(_state with { Status = "Paused", ActiveCard = _active is null ? null : card.Id, ActiveOperation = _active?.Request.Id, Error = ex.Message });
        }
    }
    private async Task FinishAsync(JobCardSpec card, OperationTicket ticket)
    {
        OperationReceipt receipt;
        try { receipt = await ticket.Completion.ConfigureAwait(false); }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _active = null; _continuous = false; _storageFailed = true;
                var message = "Operation receipt could not be confirmed. Inspect saved evidence before recovery: " + ex.Message;
                _state.Progress[card.Id] = _state.Progress[card.Id] with { Status = "Unknown", Reason = message };
                _state = _state with { Status = "OutcomeUnknown", ActiveCard = null, ActiveOperation = null, Error = message };
                try { Save(_state); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            return;
        }
        lock (_gate)
        {
            var progress = _state.Progress[card.Id];
            _state.Progress[card.Id] = progress with { Status = receipt.State.ToString(), Reason = receipt.Message,
                Completed = progress.Completed + (receipt.State == OperationState.Completed ? 1 : 0) };
            _active = null;
            if (receipt.State != OperationState.Completed) _continuous = false;
            _state = _state with { ActiveCard = null, ActiveOperation = null, Status = _storageFailed ? _state.Status : _continuous ? "Running" : receipt.State == OperationState.Completed && _state.Status != "Stopping" ? "Stopped" : "Paused" };
            try { Save(_state); }
            catch (IOException) { /* Storage failure is retained in memory and blocks all subsequent dispatch. */ }
            catch (UnauthorizedAccessException) { }
        }
    }
    internal JobDeckState Stop()
    {
        lock (_gate)
        {
            _continuous = false; _active?.Cancel();
            Save(_state with { Status = _storageFailed ? _state.Status : _active is null ? "Paused" : "Stopping", PendingRunRequest = null });
            return State;
        }
    }
    private void Save(JobDeckState state)
    {
        try
        {
            // Full messages remain in scheduler receipts; the deck is a bounded index.
            static string? Brief(string? text) => text?.Length > 700 ? text[..700] : text;
            state = state with { RecoveryRequired = _storageFailed || state.RecoveryRequired, Error = Brief(state.Error), Progress = state.Progress.ToDictionary(p => p.Key, p => p.Value with { Reason = Brief(p.Value.Reason) }) };
            if (JsonSerializer.SerializeToUtf8Bytes(state, JobDeckRules.Json).Length > 196608)
                throw new InvalidDataException("Deck state exceeds its persistence budget. Preserve reports and start a new deck session after review.");
            GameExperiments.WriteAtomic(_path, state); _state = state;
        }
        catch
        {
            _storageFailed = true; _continuous = false; _active?.Cancel();
            _state = _state with { RecoveryRequired = true, Status = "StorageError", Error = "Deck state could not be saved. Dispatch is blocked; preserve operation receipts and repair storage before reopening." };
            throw;
        }
    }
    private async Task PumpAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(_lifetime.Token).ConfigureAwait(false))
                lock (_gate)
                {
                    if (_active is not null && _state.StartedUtc is { } started && _now() >= started.AddSeconds(_state.Deck.DurationSeconds))
                    { _continuous = false; _active.Cancel(); }
                    if (_continuous)
                    {
                        try { Advance(); }
                        catch (Exception ex) { _continuous = false; _state = _state with { Status = "Paused", Error = ex.Message }; }
                    }
                }
        }
        catch (OperationCanceledException) { }
    }
    public async ValueTask DisposeAsync()
    {
        lock (_gate) { _closed = true; _continuous = false; _active?.Cancel(); _lifetime.Cancel(); }
        try { await _loop.ConfigureAwait(false); await _completion.ConfigureAwait(false); }
        finally { _lease.Dispose(); _lifetime.Dispose(); }
    }
}



