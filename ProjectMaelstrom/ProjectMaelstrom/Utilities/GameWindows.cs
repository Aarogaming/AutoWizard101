using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectMaelstrom.Utilities;

internal sealed record GameWindow(nint Handle, int ProcessId, long StartedUtcTicks, string ProcessName, string Title)
{
    public override string ToString() => $"{Title} — {ProcessName} (PID {ProcessId})";
}

/// <summary>Read-only window discovery. No input dispatch or process modification.</summary>
internal static class GameWindows
{
    public static IReadOnlyList<GameWindow> List()
    {
        var result = new List<GameWindow>();
        EnumWindows((window, _) =>
        {
            var item = Read(window);
            if (item != null && item.ProcessId != Environment.ProcessId) result.Add(item);
            return true;
        }, 0);
        return result.OrderByDescending(w => w.ProcessName.Contains("Wizard", StringComparison.OrdinalIgnoreCase))
            .ThenBy(w => w.Title).ToList();
    }

    public static GameWindow? Read(nint window)
    {
        if (!IsWindowVisible(window) || GetWindowTextLength(window) == 0) return null;
        GetWindowThreadProcessId(window, out var pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            var title = new StringBuilder(1024);
            GetWindowText(window, title, title.Capacity);
            return new(window, (int)pid, process.StartTime.ToUniversalTime().Ticks, process.ProcessName, title.ToString());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return null; }
    }

    public static bool SameIdentity(GameWindow expected, GameWindow? current) => current != null &&
        expected.Handle == current.Handle && expected.ProcessId == current.ProcessId && expected.StartedUtcTicks == current.StartedUtcTicks;

    public static bool IsCurrent(GameWindow window) => SameIdentity(window, Read(window.Handle));
    public static bool Visible(GameWindow window) => IsWindowVisible(window.Handle);
    public static bool Minimized(GameWindow window) => IsIconic(window.Handle);
    public static Size ClientSize(GameWindow window)
    {
        if (!IsCurrent(window) || !GetClientRect(window.Handle, out var rect)) throw new InvalidOperationException("The selected game window is no longer available.");
        return new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
    }
    private delegate bool EnumWindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out Rect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
