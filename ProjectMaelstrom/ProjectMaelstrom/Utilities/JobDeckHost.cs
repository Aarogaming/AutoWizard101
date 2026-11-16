using System.IO.Pipes;
using System.Text.Json;
using Maelstrom.DeckApi;

namespace ProjectMaelstrom.Utilities;

internal sealed record JobDeckRequest(string Command, JsonElement Arguments, string? RequestId = null);
internal sealed record JobDeckReply(bool Ok, object? Result = null, string? Error = null, int SchemaVersion = 1);
internal sealed record JobRoutineValidation(bool Valid, bool Confirmed, string[] Errors,
    string? RoutineId = null, int? NodeCount = null, string Authority = "native-routine-graphs-v1", bool LiveQualified = false);

/// <summary>Current-user-only API beside the existing application scheduler.
/// No fabricated telemetry, caller-supplied live facts, generic input or shell execution.</summary>
internal sealed class JobDeckHost : IAsyncDisposable
{
    private readonly string _dataRoot;
    internal string DataRoot => _dataRoot;
    private readonly OperationScheduler _scheduler;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _server;
    private readonly Task _refreshLoop;
    private readonly DeckObservationRefresh _refresh = new();
    private JobFacts? _facts;
    private W101UiTreeResult? _questReading;
    private WizwalkerSession? _session;
    private WizwalkerLaunch? _launch;
    private bool _questEnabled;
    private bool _travelEnabled;
    private bool _resourcesEnabled;
    private OperationTicket? _observer;
    private Task _observation = Task.CompletedTask;
    private (string RequestId, bool Continuous, long Revision)? _pendingStart;
    private bool _draining;
    private long _observationGeneration;
    private DateTime? _learningUntil;
    private string? _learningRun;
    private bool Learning => _learningUntil > DateTime.UtcNow;
    internal JobDeckService Deck { get; }
    internal JobDeckHost(string dataRoot, OperationScheduler scheduler)
    {
        _dataRoot = Path.GetFullPath(dataRoot); _scheduler = scheduler;
        var routines = JobDeckRoutines.Create(PrepareQuest, travel: PrepareTravel);
        Deck = new(_dataRoot, scheduler, routines, JobDeckRoutines.Initial(routines), () => Volatile.Read(ref _facts));
        // Construct synchronously so a duplicate host fails visibly before being advertised.
        var pipe = NewPipe(); _server = ServeAsync(pipe);
        _refreshLoop = RefreshAsync();
    }
    private async Task RefreshAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false))
            {
                lock (_gate)
                {
                    var state = Deck.State;
                    var plan = CardScanDemand.Build(state, Learning, Deck.Facts, resourcesEnabled: _resourcesEnabled);
                    var interested = plan.Ui;
                    if (!_refresh.Admit(Learning ? _learningRun : state.LastRunRequest, DateTime.UtcNow,
                        Learning ? _learningUntil : state.StartedUtc?.AddSeconds(state.Deck.DurationSeconds), Deck.Facts?.ObservedUtc,
                        Learning || state.Status == "Waiting", interested && _session != null && _launch != null,
                        _observer != null || state.ActiveOperation != null || _scheduler.Pending > 0,
                        _draining || _stop.IsCancellationRequested || state.RecoveryRequired)) continue;
                    try { Observe(plan.Quest, plan.Resources); }
                    catch (Exception error) when (error is InvalidOperationException or IOException or InvalidDataException)
                    { Volatile.Write(ref _facts, null); }
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }
    private NamedPipeServerStream NewPipe() => new(JobDeckWire.PipeName(_dataRoot), PipeDirection.InOut, 1,
        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    internal JobDeckReply Handle(JobDeckRequest request)
    {
        try
        {
            lock (_gate)
            {
                if ((_stop.IsCancellationRequested || _draining) && request.Command is not ("state" or "catalog" or "snapshot" or "preview" or "validate-routine" or "capabilities"))
                    throw new InvalidOperationException("The deck owner is draining work. New actions are blocked.");
                var args = request.Arguments;
                object? result = request.Command switch
                {
                    "catalog" => Deck.Catalog,
                    "capabilities" => new { schemaVersion = 1, facts = JobFactCatalog.All, routineFacts = Deck.FactDefinitions.ToDictionary(p => p.Key, p => p.Value.Where(f => !JobFactCatalog.All.Any(shared => shared.Id == f.Id)).ToArray()), aliases = JobAliasRegister.Snapshot(null, DateTime.UtcNow), routines = Deck.Catalog, liveQualified = false, note = "Catalog availability is not session readiness or permission." },
                    "validate-routine" => ValidateRoutine(args),
                    "scan-profile" => ConfigureScan(args),
                    "state" => new { state = Deck.State, eligibility = Deck.Preview(), observerOperation = _observer?.Request.Id, pendingStart = _pendingStart?.RequestId },
                    "snapshot" => new { scan = CardScanDemand.Build(Deck.State, Learning, Deck.Facts, resourcesEnabled: _resourcesEnabled), resourcesEnabled = _resourcesEnabled, resourceCandidates = Volatile.Read(ref _questReading)?.ResourceCandidate, learningUntil = _learningUntil, connected = _session != null, facts = Deck.Facts, aliases = JobAliasRegister.Snapshot(Deck.Facts, DateTime.UtcNow), questIntent = CurrentIntent(), freshnessSeconds = 2, refresh = new { attempts = _refresh.Attempts, reason = _refresh.Reason, maximumAttempts = 30, cooldownSeconds = 5 },
                        unavailable = JobFactCatalog.All.Where(f => Deck.Facts?.Values.ContainsKey(f.Id) != true).Select(f => f.Id).ToArray(),
                        note = "Absent readings are unknown. No vitals are inferred from defaults." },
                    "replace" => Deck.Replace(args.GetProperty("deck").Deserialize<JobDeckDocument>(JobDeckRules.Json)!, args.GetProperty("revision").GetInt64()),
                    "deal" => Deal(args),
                    "preview" => new { simulation = true, eligibility = Deck.Preview(args.TryGetProperty("facts", out var supplied) && supplied.ValueKind != JsonValueKind.Null
                        ? supplied.Deserialize<JobFacts>(JobDeckRules.Json) : null), note = "Preview does not dispatch or update live observations." },
                    "step" => Start(request.RequestId ?? "", false),
                    "start" => Start(request.RequestId ?? "", true),
                    "pause" or "stop" => Stop(),
                    "reset" => Deck.Reset(args.GetProperty("revision").GetInt64()),
                    "windows" => GameWindows.List().Select(w => new { handle = w.Handle.ToInt64(), w.ProcessId, w.StartedUtcTicks, w.ProcessName, w.Title }).ToArray(),
                    "connect" => Connect(args),
                    "disconnect" => Disconnect(),
                    "observe" => Observe(),
                    _ => throw new ArgumentException("Unknown deck command.")
                };
                return new(true, result);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return new(false, Error: ex.Message); }
    }
    /// <summary>Structural validation only, using the same reader as replay. This
    /// cannot connect a game, select trusted actions, submit work or qualify input.</summary>
    private static JobRoutineValidation ValidateRoutine(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty("graph", out var graph) ||
            graph.ValueKind != JsonValueKind.Object)
            return new(false, true, ["Provide a routine graph object."]);
        try
        {
            var routine = RoutineGraphs.Read(graph.GetRawText());
            return new(true, true, [], routine.Id, routine.Nodes.Length);
        }
        catch (Exception error) when (error is InvalidDataException or JsonException or ArgumentException)
        {
            return new(false, true, [error.Message.Length > 700 ? error.Message[..700] : error.Message]);
        }
    }
    private W101QuestIntent CurrentIntent() => W101QuestIntent.Parse(Volatile.Read(ref _questReading)?.StableQuestContext?.ObjectiveText);
    private object Start(string requestId, bool continuous)
    {
        if (!Guid.TryParseExact(requestId, "N", out var id)) throw new ArgumentException("A request ID (32 hex digits) is required.");
        requestId = id.ToString("N");
        var state = Deck.State;
        if ((state.RunRequests ?? []).Contains(requestId, StringComparer.OrdinalIgnoreCase)) return state;
        if (_pendingStart is { } pending && pending.RequestId == requestId) return new { status = "Observing", operationId = _observer?.Request.Id };
        if (_observer != null || state.ActiveOperation != null) throw new InvalidOperationException("An operation is already active.");
        if (_session != null && _questEnabled && state.Deck.Cards.Any(c => c.InDeck && c.RoutineId == "w101.questing" &&
            state.Progress.GetValueOrDefault(c.Id, new()).Completed < c.Runs && state.Progress.GetValueOrDefault(c.Id, new()).Attempts < c.AttemptLimit) &&
            (Deck.Facts is not { } facts || DateTime.UtcNow - facts.ObservedUtc > TimeSpan.FromSeconds(2)))
        {
            var reserved = Deck.ReserveStart(requestId);
            if (reserved.PendingRunRequest != requestId) return reserved;
            _pendingStart = (requestId, continuous, state.Revision);
            try { return Observe(); }
            catch { _pendingStart = null; Deck.Stop(); throw; }
        }
        return Deck.Start(requestId, continuous);
    }
    private object Stop()
    {
        _learningUntil = null; _learningRun = null;
        _observationGeneration++; _pendingStart = null; _observer?.Cancel();
        return new { state = Deck.Stop(), observerOperation = _observer?.Request.Id, cleanupPending = _observer != null };
    }
    private JobDeckState Deal(JsonElement args)
    {
        var state = Deck.State;
        var card = args.GetProperty("card").Deserialize<JobCardSpec>(JobDeckRules.Json) ?? throw new InvalidDataException("Provide a configured card.");
        var cards = state.Deck.Cards.Where(c => c.Id != card.Id).Append(card).ToArray();
        return Deck.Replace(state.Deck with { Cards = cards }, args.GetProperty("revision").GetInt64());
    }
    private object Connect(JsonElement args)
    {
        if (_observer is not null || Deck.State.ActiveOperation != null || Deck.State.Status is "Running" or "Waiting")
            throw new InvalidOperationException("Stop active work before changing the selected game.");
        if (!args.GetProperty("enableClientObservation").GetBoolean() || !args.GetProperty("enableUiTree").GetBoolean())
            throw new InvalidOperationException("This optional reader requires both client-observation and UI-tree opt-ins.");
        var window = GameWindows.List().SingleOrDefault(w => w.Handle.ToInt64() == args.GetProperty("handle").GetInt64() &&
            w.ProcessId == args.GetProperty("processId").GetInt32() && w.StartedUtcTicks == args.GetProperty("startedUtcTicks").GetInt64())
            ?? throw new InvalidOperationException("The selected window identity changed.");
        var target = WizwalkerTarget.FromWindow(window);
        var workspace = Path.GetFullPath(args.GetProperty("workspace") .GetString()!);
        var launch = WizwalkerLaunch.FromWorkspace(workspace) with { Worker = Path.Combine(workspace, "tools", "w101_ui_tree.py") };
        _observationGeneration++; _pendingStart = null;
        _session = new(target, true); _launch = launch;
        _questEnabled = args.TryGetProperty("enableQuestActions", out var enabled) && enabled.GetBoolean();
        _resourcesEnabled = args.TryGetProperty("enableResourceObservation", out var resources) && resources.GetBoolean();
        _travelEnabled = args.TryGetProperty("enableTravelActions", out var travel) && travel.GetBoolean();
        Volatile.Write(ref _facts, null); Volatile.Write(ref _questReading, null);
        return new { connected = true, target = W101QuestStep.TargetBinding(target), observationStarted = false, questActionsEnabled = _questEnabled, travelActionsEnabled = _travelEnabled, resourceObservationEnabled = _resourcesEnabled };
    }
    private object Disconnect()
    {
        if (_observer is not null || Deck.State.ActiveOperation is not null) throw new InvalidOperationException("Stop and wait for cleanup before disconnecting.");
        _observationGeneration++; _pendingStart = null;
        Deck.Stop(); _learningUntil = null; _learningRun = null; _session = null; _launch = null; _questEnabled = false; _travelEnabled = false; _resourcesEnabled = false; Volatile.Write(ref _facts, null); Volatile.Write(ref _questReading, null);
        return new { connected = false, note = "No client hook is retained between operations." };
    }
    private object ConfigureScan(JsonElement args)
    {
        var learning = args.GetProperty("learning").GetBoolean();
        if (learning && _session is null) throw new InvalidOperationException("Connect an opted-in observer first.");
        _learningUntil = learning ? DateTime.UtcNow.AddSeconds(60) : null;
        _learningRun = learning ? Guid.NewGuid().ToString("N") : null;
        return new { learning, until = _learningUntil, maximumSeconds = 60 };
    }
    private object Observe(bool includeQuest = true, bool includeResources = true)
    {
        if (_session is null || _launch is null) throw new InvalidOperationException("Connect an exact W101 window with observation opt-ins first.");
        if (_observer is not null || Deck.State.ActiveOperation is not null) throw new InvalidOperationException("An operation is already active.");
        Volatile.Write(ref _facts, null); Volatile.Write(ref _questReading, null);
        var ticket = W101UiTreeOperation.Submit(_scheduler, _session, _launch, true, 2, includeQuestContext: includeQuest,
            includeResourceContext: includeResources && _resourcesEnabled);
        _observer = ticket;
        var state = Deck.State;
        var deadline = _pendingStart != null || state.Status is "Running" or "Waiting" ? state.StartedUtc?.AddSeconds(state.Deck.DurationSeconds) : null;
        _observation = CollectObservationAsync(ticket, deadline, _observationGeneration);
        return new { operationId = ticket.Request.Id, status = "Queued", completed = false };
    }
    private async Task CollectObservationAsync(OperationTicket ticket, DateTime? deadline, long generation)
    {
        try
        {
            if (deadline is { } end)
            {
                using var timer = new CancellationTokenSource();
                var delay = Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, (end - DateTime.UtcNow).TotalMilliseconds)), timer.Token);
                if (await Task.WhenAny(ticket.Completion, delay).ConfigureAwait(false) != ticket.Completion) ticket.Cancel();
                timer.Cancel();
            }
            var receipt = await ticket.Completion.ConfigureAwait(false);
            if (receipt.State != OperationState.Completed) return;
            var path = Path.Combine(_dataRoot, "operations", receipt.Request.Id, "ui-result.json");
            var reading = JsonSerializer.Deserialize<W101UiTreeResult>(GameExperiments.BoundedBytes(path, W101UiTreeOperation.MaximumResultBytes), WizwalkerOperation.Json);
            lock (_gate)
            {
                if (reading != null && DeckObservationRefresh.CanPublish(generation, _observationGeneration,
                    DateTime.UtcNow, deadline, _draining || _stop.IsCancellationRequested)) UpdateFacts(reading);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        { lock (_gate) { if (generation == _observationGeneration) Volatile.Write(ref _facts, null); } }
        finally
        {
            lock (_gate)
            {
                _observer = null;
                var next = _pendingStart; _pendingStart = null;
                if (next is { } start && Deck.State.Revision == start.Revision && Deck.Facts is { } facts && DateTime.UtcNow - facts.ObservedUtc <= TimeSpan.FromSeconds(2))
                {
                    try { Deck.CompleteReservedStart(start.RequestId, start.Revision, start.Continuous); }
                    catch (Exception) { Deck.Stop(); }
                }
                else if (next is not null) Deck.Stop();
            }
        }
    }
    private JobFacts? UpdateFacts(W101UiTreeResult reading)
    {
        if (_session is null || !ObservationReady(_session.Target, reading, DateTime.UtcNow.Ticks))
        { Volatile.Write(ref _facts, null); Volatile.Write(ref _questReading, null); return null; }
        var facts = new Dictionary<string, JsonElement>();
        foreach (var (name, value) in reading.StableStates)
            if (value.HasValue && reading.States.GetValueOrDefault(name) == value) facts[name] = JsonSerializer.SerializeToElement(value.Value);
        if (reading.StableQuestContext?.ObjectiveText is { } objective) facts["quest.objective"] = JsonSerializer.SerializeToElement(objective);
        var intent = W101QuestIntent.Parse(reading.StableQuestContext?.ObjectiveText);
        if (intent.IsRecognized)
        {
            facts["quest.action"] = JsonSerializer.SerializeToElement(intent.Action.ToString());
            facts["quest.area"] = JsonSerializer.SerializeToElement(intent.Area);
            facts["quest.target"] = JsonSerializer.SerializeToElement(intent.Target);
        }
        W101ResourceFacts.Add(facts, reading.Resources, reading.StatesObservedUtcTicks);
        W101WizardSubject? subject = W101WizardFacts.AddObserved(facts, reading, DateTime.UtcNow.Ticks);
        if (reading.WizardReference is { } reference && W101WizardFacts.Add(facts, reading, reading.WizardSubject, reference, DateTime.UtcNow.Ticks))
            subject = reading.WizardSubject;
          var result = new JobFacts("wizard101", W101QuestStep.TargetBinding(_session.Target), new DateTime(reading.StatesObservedUtcTicks, DateTimeKind.Utc), facts, "reviewed-w101-ui-tree", subject);
        Volatile.Write(ref _questReading, reading); Volatile.Write(ref _facts, result); return result;
    }
    /// <summary>Observation validity is independent of quest-action readiness.
    /// Idle/menu readings are useful telemetry, never permission to advance dialogue.</summary>
    internal static bool ObservationReady(WizwalkerTarget target, W101UiTreeResult reading, long now)
    {
        string[] names = ["interactionPrompt", "dialogue", "menu", "nativeIdle"];
        return reading.Target == target && reading.Operation == W101UiTreeOperation.Version && reading.SchemaVersion == 1 &&
            reading.SourceSha256 == WizwalkerOperation.SourceSha256 && reading.Status == "observed" &&
            reading.CleanupConfirmed && reading.HookRestored && reading.TreeSamples is >= 2 and <= 120 && reading.Loading == false &&
            reading.ObservedUtcTicks > 0 && reading.ObservedUtcTicks <= now && reading.StatesObservedUtcTicks > 0 &&
            reading.StatesObservedUtcTicks <= reading.ObservedUtcTicks && now - reading.StatesObservedUtcTicks <= TimeSpan.FromSeconds(2).Ticks &&
            reading.States is { Count: 4 } && reading.StableStates is { Count: 4 } &&
            names.All(name => reading.States.ContainsKey(name) && reading.StableStates.ContainsKey(name)) &&
            reading.StableStates.All(pair => pair.Value is null || reading.States[pair.Key] == pair.Value) &&
            W101UiTreeOperation.ValidQuestContext(reading, true, now);
    }
    private PreparedDeckJob PrepareTravel(JobCardSpec card, JobFacts? facts)
    {
        if (_session is null || _launch is null || !_travelEnabled) throw new InvalidOperationException("Connect W101 and enable Travel cards first.");
        return W101WorldGateVisit.Prepare(_session, _launch, true, options: W101TravelCard.Options(card));
    }
    private PreparedDeckJob PrepareQuest(JobCardSpec card, JobFacts? facts)
    {
        if (_session is null || _launch is null || !_questEnabled) throw new InvalidOperationException("Connect W101 and separately enable selected-conversation actions first.");
        var selectionFile = card.Settings?.GetValueOrDefault("selectionFile");
        if (JobDeckRoutines.QuestMode(card) != "selected-conversation")
        {
            var reading = Volatile.Read(ref _questReading);
            if (reading is null || !W101QuestStep.EvidenceReady(_session.Target, reading, DateTime.UtcNow.Ticks) ||
                reading.StableStates.GetValueOrDefault("dialogue") != true || reading.StableQuestContext?.ObjectiveText is not { } objective ||
                reading.StableQuestContext.DialogueText is not { } dialogue)
                throw new InvalidOperationException("Quest auto-fill needs a fresh, open conversation for the currently qualified adapter.");
            var directory = Path.Combine(_dataRoot, "job-decks", "selections"); Directory.CreateDirectory(directory);
            var id = Guid.NewGuid().ToString("N"); selectionFile = Path.Combine(directory, id + ".json");
            GameExperiments.WriteAtomic(selectionFile, new W101QuestConversationSelection(id, objective, dialogue,
                MaxPages: int.Parse(card.Settings?.GetValueOrDefault("maxPages", "12") ?? "12", System.Globalization.CultureInfo.InvariantCulture)));
        }
        return W101QuestConversationOperation.Prepare(_session, _launch, selectionFile!, true, scope: reading =>
            JobDeckRules.FactBlock(card, "wizard101", UpdateFacts(reading), DateTime.UtcNow, true) is null,
            maximumPages: int.Parse(card.Settings?.GetValueOrDefault("maxPages", "12") ?? "12", System.Globalization.CultureInfo.InvariantCulture));
    }
    private async Task ServeAsync(NamedPipeServerStream pipe)
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using (pipe)
                {
                    await pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token); timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    try
                    {
                        var bytes = await JobDeckWire.ReadAsync(pipe, timeout.Token).ConfigureAwait(false);
                        var request = JsonSerializer.Deserialize<JobDeckRequest>(bytes, JobDeckRules.Json) ?? throw new InvalidDataException("Empty deck request.");
                        await JobDeckWire.WriteAsync(pipe, JsonSerializer.SerializeToUtf8Bytes(Handle(request), JobDeckRules.Json), timeout.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or OperationCanceledException) { }
                }
                if (!_stop.IsCancellationRequested) pipe = NewPipe();
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        finally { pipe.Dispose(); }
    }
    public async ValueTask DisposeAsync()
    {
        lock (_gate) { _observationGeneration++; _pendingStart = null; _stop.Cancel(); _observer?.Cancel(); }
        try { await _server.ConfigureAwait(false); await _refreshLoop.ConfigureAwait(false); await _observation.ConfigureAwait(false); }
        finally { try { await Deck.DisposeAsync().ConfigureAwait(false); } finally { _stop.Dispose(); } }
    }
    internal bool IsBusy { get { lock (_gate) return _observer != null || Deck.IsBusy; } }
    internal async Task StopAndDrainAsync()
    {
        Task observation, deck;
        lock (_gate)
        {
            _draining = true; _observationGeneration++; _pendingStart = null; _observer?.Cancel();
            try { Deck.Stop(); } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (InvalidDataException) { }
            observation = _observation; deck = Deck.DrainAsync();
        }
        try { try { await observation.ConfigureAwait(false); } finally { await deck.ConfigureAwait(false); } }
        finally { lock (_gate) _draining = false; }
    }
}

internal static class ApplicationJobDeck
{
    private static JobDeckHost? _host;
    internal static JobDeckHost For(string dataRoot) => _host ??= new(dataRoot, ApplicationOperations.For(dataRoot));
    internal static async Task StopAsync() { if (_host is not null) { await _host.DisposeAsync(); _host = null; } }
}



