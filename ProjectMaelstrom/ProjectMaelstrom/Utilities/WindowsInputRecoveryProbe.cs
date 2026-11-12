using System.Runtime.InteropServices;
using System.Text;

namespace ProjectMaelstrom.Utilities;

internal sealed record WindowsRecoveryObservation(bool SessionActive, bool DesktopAccessible,
    bool SameDesktop, bool OwnForeground, bool InputReadable, bool AnyHeld, long ActivityRevision,
    bool StableForeground = false);

/// <summary>Read-only, on-demand input check. No hooks, key identities in evidence,
/// key-up injection, focus change, capture or game-process inspection. An unavailable
/// desktop is unknown, never an all-keys-up result.</summary>
internal sealed class WindowsInputRecoveryProbe : IInputRecoveryProbe
{
    private readonly Func<WindowsRecoveryObservation> _observe;
    private readonly Func<long> _clock;
    private readonly bool _requireOwnForeground;
    public WindowsInputRecoveryProbe(bool requireOwnForeground = true) : this(() => ObserveWindows(requireOwnForeground), () => Environment.TickCount64, requireOwnForeground) { }
    internal WindowsInputRecoveryProbe(Func<WindowsRecoveryObservation> observe, Func<long> clock, bool requireOwnForeground = true)
    { _observe = observe; _clock = clock; _requireOwnForeground = requireOwnForeground; }

    public InputRecoverySample Read(InputRecoveryChallenge challenge, bool workersStopped)
    {
        try
        {
            var state = _observe();
            bool desktop = state.SessionActive && state.DesktopAccessible && state.SameDesktop &&
                (_requireOwnForeground ? state.OwnForeground : state.StableForeground);
            bool known = desktop && state.InputReadable;
            return new(challenge.Id, _clock(), state.ActivityRevision, workersStopped, desktop, known, known && !state.AnyHeld);
        }
        catch (Exception)
        { return new(challenge.Id, _clock(), 0, workersStopped, false, false, false); }
    }

    private static WindowsRecoveryObservation ObserveWindows(bool requireOwnForeground)
    {
        if (!OperatingSystem.IsWindows() || !SessionIsActive()) return Unavailable();
        // HOOKCONTROL is requested to establish the documented permission for
        // asynchronous input-state reads; no hook is installed.
        nint desktop = OpenInputDesktop(0, false, 0x0001 | 0x0008);
        if (desktop == 0) return Unavailable();
        try
        {
            nint foreground = GetForegroundWindow();
            if (foreground == 0) return Unavailable();
            string? activeName = ObjectName(desktop);
            string? ownName = ObjectName(GetThreadDesktop(GetCurrentThreadId()));
            if (activeName == null || ownName == null || !string.Equals(activeName, ownName, StringComparison.Ordinal) ||
                !string.Equals(ObjectName(GetProcessWindowStation()), "WinSta0", StringComparison.OrdinalIgnoreCase) ||
                (requireOwnForeground && !OwnsForeground()))
                return Unavailable();
            var keyboard = new byte[256];
            if (!GetKeyboardState(keyboard)) return Unavailable();
            bool held = false;
            for (int key = 1; key < 255; key++) held |= (GetAsyncKeyState(key) & 0x8000) != 0;
            var last = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
            if (!GetLastInputInfo(ref last) || !SessionIsActive() || GetForegroundWindow() != foreground ||
                (requireOwnForeground && !OwnsForeground())) return Unavailable();
            nint finalDesktop = OpenInputDesktop(0, false, 0x0001 | 0x0008);
            if (finalDesktop == 0) return Unavailable();
            try
            {
                if (ObjectName(finalDesktop) != activeName) return Unavailable();
            }
            finally { CloseDesktop(finalDesktop); }
            return new(true, true, true, OwnsForeground(), true, held, last.Time, true);
        }
        finally { CloseDesktop(desktop); }
    }
    private static WindowsRecoveryObservation Unavailable() => new(false, false, false, false, false, false, 0);
    private static bool OwnsForeground()
    {
        nint window = GetForegroundWindow();
        return window != 0 && GetWindowThreadProcessId(window, out uint process) != 0 && process == (uint)Environment.ProcessId;
    }
    private static string? ObjectName(nint handle)
    {
        if (handle == 0) return null;
        var buffer = new StringBuilder(256);
        return GetUserObjectInformation(handle, 2, buffer, buffer.Capacity * 2, out int needed) &&
            needed > 2 && needed <= buffer.Capacity * 2 && buffer.Length > 0 ? buffer.ToString() : null;
    }
    private static bool SessionIsActive()
    {
        nint buffer = 0;
        try
        {
            // WTS_CURRENT_SESSION, WTSConnectState, WTSActive. OpenInputDesktop
            // alone can succeed for a disconnected session, so check both.
            return WTSQuerySessionInformation(0, uint.MaxValue, 8, out buffer, out uint bytes) &&
                buffer != 0 && bytes == sizeof(int) && Marshal.ReadInt32(buffer) == 0;
        }
        finally { if (buffer != 0) WTSFreeMemory(buffer); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct LastInputInfo { public uint Size; public uint Time; }
    [DllImport("user32.dll", SetLastError = true)] private static extern nint OpenInputDesktop(uint flags, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint access);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseDesktop(nint desktop);
    [DllImport("user32.dll")] private static extern nint GetThreadDesktop(uint threadId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern nint GetProcessWindowStation();
    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetUserObjectInformation(nint handle, int index, StringBuilder information, int bytes, out int needed);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetKeyboardState([Out] byte[] keys);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetLastInputInfo(ref LastInputInfo info);
    [DllImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTSQuerySessionInformation(nint server, uint session, int informationClass, out nint buffer, out uint bytes);
    [DllImport("wtsapi32.dll")] private static extern void WTSFreeMemory(nint buffer);
}
