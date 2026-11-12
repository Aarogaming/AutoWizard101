using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ProjectMaelstrom.Utilities;

/// <summary>Counts activity only; never stores key identities, text, or pointer positions.</summary>
internal sealed class DesktopActivityMonitor : IDisposable
{
    private readonly nuint _ownTag;
    private readonly HookProc _keyboard, _mouse;
    private nint _keyboardHook, _mouseHook;
    private long _revision;
    private double _lastActivity = FrameTimeline.TimestampMs();
    public long Revision => Interlocked.Read(ref _revision);
    public double LastActivityMs => Volatile.Read(ref _lastActivity);
    public bool EscapeRequested { get; private set; }
    public bool Installed => _keyboardHook != 0 && _mouseHook != 0;

    private DesktopActivityMonitor(nuint ownTag) { _ownTag = ownTag; _keyboard = Keyboard; _mouse = Mouse; }

    // Install/remove on the form's message-loop thread. Remote/injected input counts
    // as takeover unless both its injection flag and this run's private tag match.
    public static DesktopActivityMonitor Install(nuint ownTag)
    {
        if (ownTag == 0) throw new ArgumentOutOfRangeException(nameof(ownTag));
        var monitor = new DesktopActivityMonitor(ownTag);
        try
        {
            monitor._keyboardHook = SetWindowsHookEx(13, monitor._keyboard, GetModuleHandle(null), 0);
            monitor._mouseHook = SetWindowsHookEx(14, monitor._mouse, GetModuleHandle(null), 0);
            if (!monitor.Installed) throw new Win32Exception(Marshal.GetLastWin32Error(), "Desktop activity detection is unavailable; home movement was not started.");
            return monitor;
        }
        catch { monitor.Dispose(); throw; }
    }

    internal static bool IsTakeover(bool injected, nuint extraInfo, nuint ownTag) => !injected || ownTag == 0 || extraInfo != ownTag;

    private void Activity() { Volatile.Write(ref _lastActivity, FrameTimeline.TimestampMs()); Interlocked.Increment(ref _revision); }
    private nint Keyboard(int code, nint message, nint data)
    {
        if (code >= 0 && (int)message is 0x100 or 0x101 or 0x104 or 0x105)
        {
            var value = Marshal.PtrToStructure<KeyboardData>(data);
            if (IsTakeover((value.Flags & 0x10) != 0, value.ExtraInfo, _ownTag))
            {
                if (value.VirtualKey == 0x1B) EscapeRequested = true;
                Activity();
            }
        }
        return CallNextHookEx(_keyboardHook, code, message, data);
    }
    private nint Mouse(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var value = Marshal.PtrToStructure<MouseData>(data);
            if (IsTakeover((value.Flags & 1) != 0, value.ExtraInfo, _ownTag)) Activity();
        }
        return CallNextHookEx(_mouseHook, code, message, data);
    }
    internal static bool AnyKeyHeld() => Enumerable.Range(1, 254).Any(key => (GetAsyncKeyState(key) & 0x8000) != 0);
    public void Dispose()
    {
        if (_keyboardHook != 0) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = 0; }
        if (_mouseHook != 0) { UnhookWindowsHookEx(_mouseHook); _mouseHook = 0; }
    }
    private delegate nint HookProc(int code, nint message, nint data);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardData { public uint VirtualKey, ScanCode, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseData { public Point Point; public uint MouseDataValue, Flags, Time; public nuint ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int id, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
