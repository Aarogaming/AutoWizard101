using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceMenuReceipt(bool Accepted, bool InputReleased, string Reason);

/// <summary>Small UI-only postgame dispatcher. It never activates a window, navigates
/// arbitrary coordinates, sends purchase/feeding actions or obtains input ownership.</summary>
internal sealed class PetDanceMenuInput(nuint tag)
{
    internal static Point? MapPoint(Rectangle capture, Rectangle client, Size image, RectangleF button)
    {
        if (capture.Size != image || !capture.Contains(client) || !PetDancePostGameClassifier.ValidBounds(button, image)) return null;
        var point = new Point(capture.X + (int)(button.X + button.Width / 2), capture.Y + (int)(button.Y + button.Height / 2));
        return client.Contains(point) ? point : null;
    }

    /// <summary>Optional continuing must retain current target, geometry, input
    /// owner and takeover checks throughout the admitted click. If omitted, ready
    /// remains the guard for every phase. Each later action needs fresh admission.</summary>
    public async Task<PetDanceMenuReceipt> DispatchAsync(PetDancePostGamePlan plan, GameWindow target, Size image,
        Func<bool> ready, CancellationToken token, Func<bool>? continuing = null)
    {
        var owner = GameInputLease.Current;
        bool Owned() => owner is { CanDispatch: true } && ReferenceEquals(owner, GameInputLease.Current) && GameWindows.IsCurrent(target);
        bool Ready() => Owned() && ready();
        bool Continuing() => Owned() && (continuing ?? ready)();
        if (!plan.IsProposal || !Ready() || DesktopActivityMonitor.AnyKeyHeld()) return new(false, true, "input-not-ready");
        if (plan.Action is not (PetDanceLifecycleAction.Next or PetDanceLifecycleAction.FinishWithoutSnack or
            PetDanceLifecycleAction.OpenMenu or PetDanceLifecycleAction.SelectIdle or PetDanceLifecycleAction.QuitToCharacterSelection or PetDanceLifecycleAction.SelectWizardCandidate or PetDanceLifecycleAction.EnterSelectedWizard or PetDanceLifecycleAction.ExitWorldGate or PetDanceLifecycleAction.TravelWorldGate or PetDanceLifecycleAction.SelectWorldGate)) return new(false, true, "action-not-allowed");
        token.ThrowIfCancellationRequested();
        var keyboard = plan.Action == PetDanceLifecycleAction.OpenMenu;
        Action? move = null;
        if (!keyboard)
        {
            if (plan.ActionBounds is not { } bounds || DwmGetWindowAttribute(target.Handle, 9, out var outer, Marshal.SizeOf<Rect>()) != 0 ||
                !GetClientRect(target.Handle, out var local)) return new(false, true, "geometry-unavailable");
            var origin = new NativePoint();
            if (!ClientToScreen(target.Handle, ref origin)) return new(false, true, "geometry-unavailable");
            var point = MapPoint(outer.Rectangle, new(origin.X, origin.Y, local.Right, local.Bottom), image, bounds);
            if (point == null) return new(false, true, "geometry-changed");
            var virtualWidth = GetSystemMetrics(78); var virtualHeight = GetSystemMetrics(79);
            if (virtualWidth <= 1 || virtualHeight <= 1) return new(false, true, "display-geometry-unavailable");
            var x = (int)Math.Round((point.Value.X - GetSystemMetrics(76)) * 65535d / (virtualWidth - 1));
            var y = (int)Math.Round((point.Value.Y - GetSystemMetrics(77)) * 65535d / (virtualHeight - 1));
            if (x is < 0 or > 65535 || y is < 0 or > 65535 || !Ready()) return new(false, true, "input-not-ready");
            move = () => Send(new() { Type = 0, Data = new() { Mouse = new() { X = x, Y = y, Flags = 0xC001, Extra = tag } } });
        }
        var down = keyboard ? Key(false) : Mouse(false);
        var up = keyboard ? Key(true) : Mouse(true);
        return await DispatchSequenceAsync(move, () => Send(down), () => Send(up), Ready, Continuing,
            keyboard ? 40 : 80, Task.Delay, token,
            () => owner!.RequireRecovery("Postgame input release could not be confirmed.")).ConfigureAwait(false);
    }

    /// <summary>The cursor settles for 80 ms before mouse down, then holds 80 ms.
    /// The initial ready check admits this one bounded click; the continuing
    /// callback must check target/geometry/ownership/takeover throughout settling
    /// and holding, without reinterpreting natural evidence aging. The public
    /// dispatcher defaults continuing to ready for existing callers. This grants
    /// no freshness extension to any subsequent action.
    /// Release is mandatory even if delivery throws or cancellation intervenes.</summary>
    internal static async Task<PetDanceMenuReceipt> DispatchSequenceAsync(Action? move, Action down, Action up,
        Func<bool> ready, Func<bool> continuing, int holdMs, Func<int, CancellationToken, Task> delay,
        CancellationToken token, Action requireRecovery)
    {
        var attempted = false;
        var accepted = false; var released = true; var reason = "dispatched";
        async Task<bool> Wait(int milliseconds)
        {
            for (var elapsed = 0; elapsed < milliseconds; elapsed += 10)
            {
                if (token.IsCancellationRequested || !continuing()) return false;
                await delay(Math.Min(10, milliseconds - elapsed), token).ConfigureAwait(false);
            }
            return !token.IsCancellationRequested && continuing();
        }
        try
        {
            if (holdMs is not (40 or 80) || token.IsCancellationRequested || !ready()) return new(false, true, "input-not-ready");
            if (move is not null)
            {
                move();
                if (!await Wait(80).ConfigureAwait(false)) return new(false, true, "input-not-ready");
            }
            if (token.IsCancellationRequested || !(move is null ? ready() : continuing())) return new(false, true, "input-not-ready");
            attempted = true; down();
            accepted = await Wait(holdMs).ConfigureAwait(false);
            if (!accepted) reason = token.IsCancellationRequested ? "cancelled" : "input-not-ready";
        }
        catch (OperationCanceledException) { reason = "cancelled"; }
        catch (Exception error) { reason = error.Message; }
        finally
        {
            if (attempted)
            {
                try { up(); }
                catch
                {
                    try { up(); }
                    catch
                    {
                        released = false;
                        try { requireRecovery(); }
                        catch (Exception error) { reason += "; recovery notice could not be saved: " + error.Message; }
                    }
                }
            }
        }
        return new(accepted, released, reason);
    }

    private Input Key(bool release) => new() { Type = 1, Data = new() { Keyboard = new() { Scan = 1, Flags = 8u | (release ? 2u : 0u), Extra = tag } } };
    private Input Mouse(bool release) => new() { Type = 0, Data = new() { Mouse = new() { Flags = release ? 4u : 2u, Extra = tag } } };
    private static void Send(Input input)
    {
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected the menu input.");
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect
    {
        public int Left, Top, Right, Bottom;
        public readonly Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputData Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputData { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out Rect rectangle);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint window, ref NativePoint point);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out Rect value, int size);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
}
