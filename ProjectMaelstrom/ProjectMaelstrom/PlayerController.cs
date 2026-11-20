using WindowsInput;
using WindowsInput.Native;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

internal sealed record LegacyInputHooks(Func<bool> Ready, Action<VirtualKeyCode> Down, Action<VirtualKeyCode> Up, Action<Point> Click);

/// <summary>Compatibility input path. Shares ownership with native routines and never steals focus.
/// Legacy held keys expire after two seconds and are released on focus/policy loss.</summary>
internal sealed class PlayerController
{
    private readonly InputSimulator _input = new();
    private readonly object _gate = new();
    private readonly HashSet<VirtualKeyCode> _held = [];
    private GameInputLease? _owner;
    private bool _ownsLease;
    private System.Threading.Timer? _release;
    private long _deadline;
    private GameWindow? _target;
    private readonly LegacyInputHooks? _hooks;
    private readonly Func<GameInputLease> _acquire;
    private readonly Func<long> _clock;
    private readonly bool _timers;
    public PlayerController() : this(null, () => GameInputLease.Acquire("legacy-native-input"), () => Environment.TickCount64, true) { }
    public static PlayerController ForBackgroundWindow(IntPtr hWnd) =>
        new(CreateBackgroundHooks(hWnd), () => GameInputLease.Acquire("background-native-input"), () => Environment.TickCount64, true);

    public static LegacyInputHooks CreateBackgroundHooks(IntPtr hWnd)
    {
        return new LegacyInputHooks(
            Ready: () => hWnd != IntPtr.Zero && WinAPI.IsWindow(hWnd) && ExecutionPolicyManager.Current.AllowLiveAutomation,
            Down: vk =>
            {
                if (hWnd != IntPtr.Zero)
                    WinAPI.PostMessage(hWnd, WinAPI.WM_KEYDOWN, (IntPtr)(int)vk, IntPtr.Zero);
            },
            Up: vk =>
            {
                if (hWnd != IntPtr.Zero)
                    WinAPI.PostMessage(hWnd, WinAPI.WM_KEYUP, (IntPtr)(int)vk, IntPtr.Zero);
            },
            Click: pt =>
            {
                if (hWnd != IntPtr.Zero)
                {
                    IntPtr lParam = (IntPtr)((pt.Y << 16) | (pt.X & 0xFFFF));
                    WinAPI.PostMessage(hWnd, WinAPI.WM_LBUTTONDOWN, (IntPtr)WinAPI.MK_LBUTTON, lParam);
                    WinAPI.PostMessage(hWnd, WinAPI.WM_LBUTTONUP, IntPtr.Zero, lParam);
                }
            }
        );
    }

    internal PlayerController(LegacyInputHooks? hooks, Func<GameInputLease> acquire, Func<long> clock, bool timers)
    { _hooks = hooks; _acquire = acquire; _clock = clock; _timers = timers; }
    public void MoveForward() => KeyDown("UP");
    public void MoveBackward() => KeyDown("DOWN");
    public void MoveLeft() => KeyDown("LEFT");
    public void MoveRight() => KeyDown("RIGHT");
    public void Interact() => KeyPress("VK_X");
    public void PressNumber9() => KeyPress("NUMPAD9");
    public void Click(Point point) => Execute(() => { if (_hooks != null) _hooks.Click(point); else WinAPI.click(point); });
    public void KeyPress(string key)
    {
        // Track the down event even for a tap, so a partial dispatch cannot silently strand a key.
        KeyDown(key);
        KeyUp(key);
    }
    public void KeyDown(string key)
    {
        lock (_gate)
        {
            Check(); Acquire();
            try
            {
                var code = Parse(key); _held.Add(code);
                if (_hooks != null) _hooks.Down(code); else _input.Keyboard.KeyDown(code);
                if (_deadline == 0)
                {
                    _deadline = _clock() + 2000;
                    if (_timers) _release = new System.Threading.Timer(_ => CheckHeld(), null, 50, 50);
                }
            }
            catch { ReleaseAll(); throw; }
        }
    }
    public void KeyUp(string key)
    {
        lock (_gate)
        {
            var code = Parse(key);
            if (!_held.Contains(code)) return;
            SendUp(code); _held.Remove(code);
            if (_held.Count == 0) ReleaseLease();
        }
    }
    internal void ReleaseAll()
    {
        lock (_gate)
        {
            foreach (var key in _held.ToArray())
            {
                try { SendUp(key); _held.Remove(key); }
                catch (Exception error)
                {
                    try { _owner?.RequireRecovery(error.Message); }
                    catch (Exception noticeError) when (noticeError is IOException or UnauthorizedAccessException)
                    { Logger.LogError("Recovery notice could not be saved; input remains blocked", noticeError); }
                    Logger.LogError("Legacy key release failed; retaining ownership", error);
                }
            }
            if (_held.Count == 0) ReleaseLease();
        }
    }
    private void SendUp(VirtualKeyCode key) { if (_hooks != null) _hooks.Up(key); else _input.Keyboard.KeyUp(key); }
    internal void CheckHeld()
    {
        lock (_gate)
            if (_clock() >= _deadline || _owner?.CanDispatch != true || !Permitted()) ReleaseAll();
    }
    private static VirtualKeyCode Parse(string key) => Enum.TryParse<VirtualKeyCode>(key, true, out var code) && Enum.IsDefined(code)
        ? code : throw new InvalidDataException("Unknown game key: " + key);
    private bool Permitted() => _hooks?.Ready() ?? (ExecutionPolicyManager.Current.AllowLiveAutomation &&
        ExecutionPolicyManager.Current.Profile == ExecutionProfile.ExperimentalSimulation &&
        _target != null && GameWindows.IsCurrent(_target) && WinAPI.GetForegroundWindow() == _target.Handle);
    public bool EnsureGameForeground()
    {
        lock (_gate)
        {
            if (_hooks != null) return _hooks.Ready();
            if (_held.Count > 0) return Permitted();
            _target = GameWindows.List().FirstOrDefault(w => HomeMovementGate.IsWizard101(w) && w.Handle == WinAPI.GetForegroundWindow());
            return Permitted();
        }
    }
    private void Check()
    {
        if (!EnsureGameForeground()) { ReleaseAll(); throw new InvalidOperationException("Game input paused: selected Wizard101 is not foreground or controls are disabled."); }
    }
    private void Acquire()
    {
        if (_owner?.IsHeld == true)
        {
            if (!_owner.CanDispatch)
            {
                // Retain release-only cleanup even when this cached owner can no
                // longer authorize a new down event or click.
                ReleaseAll();
                throw new InvalidOperationException("Game input needs recovery review before new controls can run.");
            }
            return;
        }
        _owner = GameInputLease.Current; _ownsLease = _owner == null;
        _owner ??= _acquire();
        if (!_owner.CanDispatch)
        {
            ReleaseAll();
            throw new InvalidOperationException("Game input needs recovery review before new controls can run.");
        }
    }
    private void ReleaseLease()
    {
        _release?.Dispose(); _release = null;
        _deadline = 0;
        if (_ownsLease) _owner?.Dispose();
        _owner = null; _ownsLease = false;
    }
    private void Execute(Action action)
    {
        lock (_gate)
        {
            Check(); Acquire();
            try { action(); }
            finally { if (_held.Count == 0) ReleaseLease(); }
        }
    }
}
