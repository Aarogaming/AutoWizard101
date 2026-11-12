using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ProjectMaelstrom.Utilities;

internal enum GameplayInputProfile { Movement, Wizard101Navigation }

/// <summary>Optional low-level observations, retained only while the selected window has focus.</summary>
internal sealed class GameplayInputRecorder : IDisposable
{
    private static readonly IReadOnlyDictionary<int, string> Keys = new Dictionary<int, string>
    {
        [0x41] = "A",
        [0x44] = "D",
        [0x53] = "S",
        [0x57] = "W",
        [0x25] = "Left",
        [0x26] = "Up",
        [0x27] = "Right",
        [0x28] = "Down",
        [0x20] = "Space",
        [0x10] = "Shift",
        [0xA0] = "LeftShift",
        [0xA1] = "RightShift",
        [0x11] = "Control",
        [0xA2] = "LeftControl",
        [0xA3] = "RightControl"
    };
    private static readonly IReadOnlyDictionary<int, string> NavigationKeys = new Dictionary<int, string>
    {
        [0x58] = "X",
        [0x4D] = "M",
        [0x4E] = "N",
        [0x52] = "R",
        [0x23] = "End",
        [0x24] = "Home",
        [0x21] = "PageUp",
        [0x22] = "PageDown",
        [0x1B] = "Escape",
        [0x90] = "NumLock"
    };
    public static string[] KeyNames => KeyNamesFor(GameplayInputProfile.Movement);
    internal static string[] KeyNamesFor(GameplayInputProfile profile) => profile switch
    {
        GameplayInputProfile.Movement => Keys.Values.ToArray(),
        GameplayInputProfile.Wizard101Navigation => Keys.Values.Concat(NavigationKeys.Values).ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };
    private readonly GameWindow _target;
    private readonly GameplayInputTimeline _timeline;
    private readonly GameplayInputProfile _profile;
    private readonly HookProc _keyboard, _mouse;
    private readonly WinEventProc _focus;
    private nint _keyboardHook, _mouseHook, _focusHook;

    private GameplayInputRecorder(GameWindow target, GameplayInputProfile profile)
    {
        _target = target;
        _profile = profile;
        _timeline = new(profile);
        _keyboard = Keyboard;
        _mouse = Mouse;
        _focus = FocusChanged;
    }

    // Install and remove on a thread with a Windows message loop (the workspace UI).
    public static GameplayInputRecorder Install(GameWindow target, GameplayInputProfile profile = GameplayInputProfile.Movement)
    {
        if (!GameWindows.IsCurrent(target)) throw new InvalidOperationException("The selected game window is no longer available.");
        _ = KeyNamesFor(profile);
        if (profile == GameplayInputProfile.Wizard101Navigation && !HomeMovementGate.IsWizard101(target))
            throw new InvalidOperationException("The navigation input profile requires a selected Wizard101 client.");
        var recorder = new GameplayInputRecorder(target, profile);
        try
        {
            recorder._keyboardHook = SetWindowsHookEx(13, recorder._keyboard, GetModuleHandle(null), 0);
            if (recorder._keyboardHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not observe game keys.");
            recorder._mouseHook = SetWindowsHookEx(14, recorder._mouse, GetModuleHandle(null), 0);
            if (recorder._mouseHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not observe the mouse.");
            recorder._focusHook = SetWinEventHook(3, 3, 0, recorder._focus, 0, 0, 0);
            if (recorder._focusHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not observe window focus.");
            return recorder;
        }
        catch { recorder.Dispose(); throw; }
    }

    public void Start(double captureMs, double callbackMs) => _timeline.Start(captureMs, callbackMs, IsTargetFocused());
    public void Stop(double now) => _timeline.Stop(now);
    public void Save(string directory, bool videoVerified) => _timeline.Save(directory, _target, videoVerified);

    private bool IsTargetFocused()
    {
        if (GetForegroundWindow() != _target.Handle) return false;
        GetWindowThreadProcessId(_target.Handle, out var pid);
        return pid == (uint)_target.ProcessId;
    }

    private nint Keyboard(int code, nint message, nint data)
    {
        if (code >= 0 && IsTargetFocused())
        {
            var value = Marshal.PtrToStructure<KeyboardData>(data);
            RecordKeyboard(_timeline, (int)message, value.VirtualKey, value.Flags, FrameTimeline.TimestampMs(), _profile);
        }
        return CallNextHookEx(_keyboardHook, code, message, data);
    }

    internal static void RecordKeyboard(GameplayInputTimeline timeline, int message, uint virtualKey, uint flags, double now,
        GameplayInputProfile profile = GameplayInputProfile.Movement)
    {
        var found = Keys.TryGetValue((int)virtualKey, out var key) ||
            (profile == GameplayInputProfile.Wizard101Navigation && NavigationKeys.TryGetValue((int)virtualKey, out key));
        if (found && message is 0x100 or 0x101 or 0x104 or 0x105)
            timeline.Input(now, true, message is 0x100 or 0x104 ? "key_down" : "key_up", key, injected: (flags & 0x10) != 0);
    }

    private nint Mouse(int code, nint message, nint data)
    {
        if (code >= 0 && IsTargetFocused())
        {
            var kind = (int)message;
            var value = Marshal.PtrToStructure<MouseData>(data);
            var (type, button, wheel) = ClassifyMouse(kind, value.MouseDataValue);
            if (type != null)
            {
                var point = value.Point;
                if (ScreenToClient(_target.Handle, ref point) && GetClientRect(_target.Handle, out var rect) &&
                    point.X >= 0 && point.Y >= 0 && point.X < rect.Right && point.Y < rect.Bottom)
                {
                    _timeline.Input(FrameTimeline.TimestampMs(), true, type, button, point.X, point.Y, wheel, (value.Flags & 1) != 0);
                }
            }
        }
        return CallNextHookEx(_mouseHook, code, message, data);
    }

    internal static (string? Type, string? Button, int? Wheel) ClassifyMouse(int kind, uint data)
    {
        var type = kind switch
        {
            0x200 => "mouse_move",
            0x201 or 0x204 or 0x207 => "mouse_down",
            0x202 or 0x205 or 0x208 => "mouse_up",
            0x20A => "mouse_wheel",
            _ => null
        };
        var button = kind switch { 0x201 or 0x202 => "Left", 0x204 or 0x205 => "Right", 0x207 or 0x208 => "Middle", _ => null };
        return (type, button, kind == 0x20A ? (short)(data >> 16) : null);
    }

    private void FocusChanged(nint hook, uint eventType, nint window, int objectId, int childId, uint thread, uint time) =>
        _timeline.Focus(FrameTimeline.TimestampMs(), IsTargetFocused());

    public void Dispose()
    {
        if (_keyboardHook != 0) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = 0; }
        if (_mouseHook != 0) { UnhookWindowsHookEx(_mouseHook); _mouseHook = 0; }
        if (_focusHook != 0) { UnhookWinEvent(_focusHook); _focusHook = 0; }
    }

    private delegate nint HookProc(int code, nint message, nint data);
    private delegate void WinEventProc(nint hook, uint eventType, nint window, int objectId, int childId, uint thread, uint time);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardData { public uint VirtualKey, ScanCode, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseData { public Point Point; public uint MouseDataValue, Flags, Time; public nuint ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int id, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(nint window, ref Point point);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out Rect rect);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
}
