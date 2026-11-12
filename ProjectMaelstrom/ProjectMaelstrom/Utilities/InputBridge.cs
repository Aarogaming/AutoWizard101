using System.Text.Json;
using ProjectMaelstrom.Models;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Polls the cache for input commands and dispatches them through PlayerController.
/// External scripts can drop a JSON array of InputCommand objects at Scripts/.cache/commands.json.
/// </summary>
internal sealed class InputBridge : IDisposable
{
    private readonly string _commandPath;
    private readonly PlayerController _controller;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private System.Threading.Timer? _timer;
    private bool _disposing;
    private readonly Queue<InputCommand> _queue = new();
    private readonly object _lock = new();
    private readonly int _minDelayMs = 35; // small throttle to avoid spamming inputs
    private DateTime _lastDispatchUtc = DateTime.MinValue;
    private readonly object _drainGate = new();
    private readonly List<OperationTicket> _scheduled = new();
    public bool IsIdle
    {
        get
        {
            lock (_lock) return _queue.Count == 0 && _scheduled.All(t => t.Completion.IsCompleted);
        }
    }

    public InputBridge(PlayerController controller)
    {
        _controller = controller;
        _commandPath = Path.Combine(StorageUtils.GetCacheDirectory(), "commands.json");
    }

    public void Start(TimeSpan interval)
    {
        Stop();
        _timer = new System.Threading.Timer(_ => ProcessCommandsSafe(), null, TimeSpan.Zero, interval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        lock (_lock) _queue.Clear();
        OperationTicket[] pending; lock (_lock) pending = _scheduled.ToArray();
        foreach (var ticket in pending) ticket.Cancel();
        _controller.ReleaseAll();
    }

    public void Enqueue(InputCommand command)
    {
        if (command == null) return;
        EnqueueRange([command]);
    }

    public void EnqueueRange(IEnumerable<InputCommand> commands)
    {
        if (commands == null) return;
        var frozen = commands.Take(257).Select(c => new InputCommand { Type = c.Type, X = c.X, Y = c.Y, Key = c.Key, DelayMs = c.DelayMs }).ToArray();
        if (frozen.Length > 256 || frozen.Any(c => c.DelayMs is < 0 or > 5000)) throw new InvalidDataException("Input batches support at most 256 commands and 0–5000 ms per delay.");
        if (GameInputLease.Current == null)
        {
            var target = GameWindows.List().SingleOrDefault(w => HomeMovementGate.IsWizard101(w) && w.Handle == WinAPI.GetForegroundWindow());
            if (!ExecutionPolicyManager.Current.AllowLiveAutomation || target == null) throw new InvalidOperationException("Select foreground Wizard101 and enable controls before submitting inputs.");
            var request = new OperationRequest(Guid.NewGuid().ToString("N"), "legacy.input-batch", "bounded-input-v1", 120, true,
                $"{target.ProcessId}:{target.StartedUtcTicks}:{target.Handle}", new Dictionary<string, string>());
            var ticket = ApplicationOperations.For(WorkshopHost.DataRoot(AppContext.BaseDirectory)).Submit(request, (_, token) =>
            {
                try
                {
                    foreach (var command in frozen)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!GameWindows.IsCurrent(target) || WinAPI.GetForegroundWindow() != target.Handle) throw new InvalidOperationException("Selected game changed or lost foreground.");
                        EnqueueRange([command]);
                    }
                    return Task.FromResult(new OperationOutcome(OperationState.Unknown, "Input batch ended; game outcome is unverified.", []));
                }
                finally { _controller.ReleaseAll(); }
            });
            lock (_lock) { _scheduled.RemoveAll(t => t.Completion.IsCompleted); _scheduled.Add(ticket); }
            return;
        }
        lock (_lock)
        {
            foreach (var cmd in frozen)
            {
                if (cmd != null) _queue.Enqueue(cmd);
            }
        }
        DrainQueue();
    }

    public void Dispose()
    {
        _disposing = true;
        Stop();
    }

    private void ProcessCommandsSafe()
    {
        if (_disposing) return;
        var policy = ExecutionPolicyManager.Current;
        List<InputCommand>? commands = null;
        string? claimed = null;
        try
        {
            if (!File.Exists(_commandPath))
            {
                return;
            }

            var history = Path.Combine(Path.GetDirectoryName(_commandPath)!, "legacy-input-inbox");
            Directory.CreateDirectory(history);
            claimed = Path.Combine(history, Guid.NewGuid().ToString("N") + ".received.json");
            File.Move(_commandPath, claimed);
            if (new FileInfo(claimed).Length > 1048576) throw new InvalidDataException("Input request exceeds 1 MiB.");
            string json = File.ReadAllText(claimed);
            commands = JsonSerializer.Deserialize<List<InputCommand>>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            Logger.LogError("[InputBridge] Failed to read commands", ex);
            if (claimed != null && File.Exists(claimed)) File.WriteAllText(claimed + ".error.txt", ex.Message);
        }

        if (commands == null || commands.Count == 0)
        {
            return;
        }

        if (!policy.AllowLiveAutomation)
        {
            Logger.LogBotAction("InputBridge", $"Blocked {commands.Count} commands by policy (live disabled).");
            return;
        }

        try { EnqueueRange(commands); }
        catch (Exception error) { Logger.LogError("[InputBridge] Request was not admitted", error); }
    }

    private void DrainQueue()
    {
        lock (_drainGate)
        {
            try { DrainQueueCore(); }
            catch (Exception error)
            {
                lock (_lock) _queue.Clear();
                _controller.ReleaseAll();
                Logger.LogError("[InputBridge] Input paused", error);
            }
        }
    }
    private void DrainQueueCore()
    {
        while (true)
        {
            InputCommand? cmd = null;
            lock (_lock)
            {
                if (_queue.Count == 0) break;
                cmd = _queue.Dequeue();
            }

            if (cmd == null) continue;

            // simple rate limiter between dispatched commands
            var now = DateTime.UtcNow;
            var sinceLast = (now - _lastDispatchUtc).TotalMilliseconds;
            if (sinceLast < _minDelayMs)
            {
                Thread.Sleep(_minDelayMs - (int)sinceLast);
            }

            if (Dispatch(cmd) && cmd.DelayMs > 0 && cmd.Type != "delay")
            {
                Thread.Sleep(Math.Clamp(cmd.DelayMs, 0, 5000));
            }
            _lastDispatchUtc = DateTime.UtcNow;
        }
    }

    private bool Dispatch(InputCommand command)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.Type))
        {
            return false;
        }
        if (!ExecutionPolicyManager.Current.AllowLiveAutomation && command.Type != "key_up")
        { _controller.ReleaseAll(); return false; }

        switch (command.Type.Trim().ToLowerInvariant())
        {
            case "click":
                if (!_controller.EnsureGameForeground())
                {
                    Logger.LogBotAction("InputBridge", "Skipped click: Wizard101 not focused");
                    return false;
                }
                _controller.Click(new Point(command.X, command.Y));
                return true;
            case "key_press":
                if (!_controller.EnsureGameForeground())
                {
                    Logger.LogBotAction("InputBridge", "Skipped key_press: Wizard101 not focused");
                    return false;
                }
                _controller.KeyPress(command.Key ?? string.Empty);
                return true;
            case "key_down":
                if (!_controller.EnsureGameForeground())
                {
                    Logger.LogBotAction("InputBridge", "Skipped key_down: Wizard101 not focused");
                    return false;
                }
                _controller.KeyDown(command.Key ?? string.Empty);
                return true;
            case "key_up":
                _controller.KeyUp(command.Key ?? string.Empty);
                return true;
            case "delay":
                if (command.DelayMs > 0)
                {
                    Thread.Sleep(Math.Clamp(command.DelayMs, 0, 5000));
                }
                return true;
            default:
                Logger.LogError($"[InputBridge] Unknown command type: {command.Type}");
                return false;
        }
    }
}
