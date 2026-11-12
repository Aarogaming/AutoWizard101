using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ProjectMaelstrom.Utilities;

internal sealed record DodgePulseResult(string Status, double? DownMs, double? UpMs, string? Reason = null);

internal interface IDodgeKeySink
{
    bool Ready(GameWindow target, Size expectedClientSize);
    bool StopRequested();
    bool MovementKeyHeld();
    void KeyDown(string key);
    void KeyUp(string key);
}

/// <summary>One bounded, foreground-only key pulse. This class owns no focus changes.</summary>
internal sealed class DodgeInputPulse(IDodgeKeySink sink, Func<bool> permitted, Func<double>? clock = null)
{
    private static readonly SemaphoreSlim SinglePulse = new(1, 1);
    private static readonly HashSet<string> Keys = new(StringComparer.Ordinal) { "LEFT", "RIGHT", "UP", "DOWN", "A", "D", "W", "S" };
    private readonly Func<double> _clock = clock ?? FrameTimeline.TimestampMs;

    /// <summary>Checks capture age and an optional absolute release deadline again
    /// immediately before key-down. Actual receipt times remain authoritative:
    /// desktop scheduling cannot provide a hard real-time completion guarantee.</summary>
    public async Task<DodgePulseResult> RunAsync(GameWindow target, Size expectedClientSize, string key, int durationMs,
        double captureMs, CancellationToken token, double maximumCaptureAgeMs = 200, double? responseDeadlineMs = null)
    {
        if (!Keys.Contains(key) || durationMs is < 1 or > 200 || !double.IsFinite(captureMs) ||
            !double.IsFinite(maximumCaptureAgeMs) || maximumCaptureAgeMs <= 0 ||
            responseDeadlineMs is { } requestedDeadline && (!double.IsFinite(requestedDeadline) || requestedDeadline < 0))
            return new("invalid-action", null, null);
        if (!await SinglePulse.WaitAsync(0, token).ConfigureAwait(false)) return new("busy", null, null);
        double? down = null, up = null;
        var status = "rejected";
        string? reason = null;
        try
        {
            var age = _clock() - captureMs;
            if (!permitted()) return new("rejected", null, null, "live-controls-disabled");
            if (!double.IsFinite(age) || age < 0 || age > maximumCaptureAgeMs) return new("rejected", null, null, "stale-or-invalid-frame");
            if (!sink.Ready(target, expectedClientSize)) return new("rejected", null, null, "target-not-focused-or-changed");
            if (sink.StopRequested()) return new("rejected", null, null, "escape");
            if (sink.MovementKeyHeld()) return new("rejected", null, null, "movement-key-already-held");
            token.ThrowIfCancellationRequested();
            // Permission/target checks can take time. Do not reuse their entry clock.
            var dispatchMs = _clock();
            age = dispatchMs - captureMs;
            if (!double.IsFinite(age) || age < 0 || age > maximumCaptureAgeMs)
                return new("rejected", null, null, "stale-or-invalid-frame");
            if (responseDeadlineMs is { } responseDeadline && dispatchMs + durationMs > responseDeadline)
                return new("rejected", null, null, "response-window-expired");
            sink.KeyDown(key);
            down = _clock();
            status = "dispatched";
            var deadline = Math.Min(down.Value + durationMs, responseDeadlineMs ?? double.PositiveInfinity);
            while (_clock() < deadline)
            {
                if (token.IsCancellationRequested || !permitted() || !sink.Ready(target, expectedClientSize) || sink.StopRequested())
                {
                    status = "interrupted";
                    break;
                }
                var remainingMs = deadline - _clock();
                if (remainingMs <= 0) break;
                await Task.Delay((int)Math.Clamp(remainingMs, 1, 15)).ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                if (down != null)
                {
                    try { sink.KeyUp(key); up = _clock(); }
                    catch (Exception ex)
                    {
                        status = "release-failed";
                        reason = ex.Message;
                        try { sink.KeyUp(key); up = _clock(); status = "release-retried"; }
                        catch (Exception retry) { reason += "; retry: " + retry.Message; GameInputLease.Current?.RequireRecovery(reason); }
                    }
                }
            }
            finally { SinglePulse.Release(); }
        }
        return new(status, down, up, reason);
    }
}

internal sealed class WindowsDodgeKeySink(nuint injectionTag = 0) : IDodgeKeySink
{
    private static readonly Dictionary<string, ushort> VirtualKeys = new(StringComparer.Ordinal)
    {
        ["LEFT"] = 0x25,
        ["UP"] = 0x26,
        ["RIGHT"] = 0x27,
        ["DOWN"] = 0x28,
        ["A"] = 0x41,
        ["D"] = 0x44,
        ["W"] = 0x57,
        ["S"] = 0x53
    };
    public bool Ready(GameWindow target, Size expectedClientSize) =>
        GetForegroundWindow() == target.Handle && GameWindows.IsCurrent(target) && !GameWindows.Minimized(target) &&
        GetClientRect(target.Handle, out var rectangle) &&
        rectangle.Right - rectangle.Left == expectedClientSize.Width && rectangle.Bottom - rectangle.Top == expectedClientSize.Height;
    public static async Task<bool> TryFocusAsync(GameWindow target)
    {
        bool Valid() => GameWindows.IsCurrent(target) && !GameWindows.Minimized(target);
        bool Focused() => GetForegroundWindow() == target.Handle;
        if (!Valid()) return false;
        if (Focused()) return true;
        var foreground = GetForegroundWindow();
        if (foreground == 0) return false;
        GetWindowThreadProcessId(foreground, out var foregroundProcessId);
        if (!MayRequestFocus(foregroundProcessId, (uint)Environment.ProcessId)) return false;
        if (!SetForegroundWindow(target.Handle) && !Focused()) return false;
        return await WaitForFocusAsync(Valid, Focused).ConfigureAwait(false);
    }

    // Do not take focus away from another application the user is working in.
    internal static bool MayRequestFocus(uint foregroundProcessId, uint ownProcessId) =>
        foregroundProcessId != 0 && foregroundProcessId == ownProcessId;

    // Foreground changes can complete after SetForegroundWindow returns. Wait briefly,
    // without sending input, and revalidate the selected window on every check.
    internal static async Task<bool> WaitForFocusAsync(Func<bool> valid, Func<bool> focused)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (!valid()) return false;
            if (focused()) return true;
            await Task.Delay(20).ConfigureAwait(false);
        }
        return valid() && focused();
    }
    public bool StopRequested() => (GetAsyncKeyState(0x1B) & 0x8000) != 0;
    public bool MovementKeyHeld() => VirtualKeys.Values.Any(key => (GetAsyncKeyState(key) & 0x8000) != 0);
    public void KeyDown(string key)
    {
        if (GameInputLease.Current == null) throw new InvalidOperationException("A shared input owner is required before sending a game key.");
        Send(key, false);
    }
    public void KeyUp(string key) => Send(key, true);
    private void Send(string key, bool release)
    {
        var encoded = Encode(key, release);
        var input = new Input { Type = 1, Data = new InputData { Keyboard = new KeyboardInput { VirtualKey = 0, Scan = encoded.Scan, Flags = encoded.Flags, Extra = injectionTag } } };
        if (SendInput(1, new[] { input }, Marshal.SizeOf<Input>()) != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows did not accept the game input.");
    }
    // Games may bind physical keys instead of virtual keys. KEYEVENTF_SCANCODE supplies
    // the physical scan code, and extended arrows must retain KEYEVENTF_EXTENDEDKEY.
    internal static (ushort Scan, uint Flags) Encode(string key, bool release)
    {
        var vk = VirtualKeys[key];
        var scan = MapVirtualKey(vk, 0);
        if (scan is 0 or > 255) throw new InvalidOperationException("Windows could not map the game key to a physical scan code.");
        return ((ushort)scan, 8u | (release ? 2u : 0u) | (vk is >= 0x25 and <= 0x28 ? 1u : 0u));
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputData Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputData { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out Rect rectangle);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")] private static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
}
