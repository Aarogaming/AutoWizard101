using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ProjectMaelstrom.Utilities;

/// <summary>Optional scope-owned transport controls. Never activates a window or sends game input.
/// One bounded-lifetime STA loop updates at 5 Hz; disposal must precede owner release.</summary>
internal sealed class W101RunningNotice : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop;
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposeRequested;

    private W101RunningNotice(CancellationToken token) => _stop = CancellationTokenSource.CreateLinkedTokenSource(token);

    /// <summary>Status callback accepts Running, Paused or Stopping (optionally prefixed
    /// by Maelstrom). Unknown status is displayed as Paused, never as permission to act.</summary>
    internal static async Task<W101RunningNotice> StartAsync(GameWindow target, Func<string> status, CancellationToken token,
        Action? resume = null, Action? pause = null, Action? stop = null)
    {
        ArgumentNullException.ThrowIfNull(status);
        token.ThrowIfCancellationRequested();
        var notice = new W101RunningNotice(token);
        var thread = new Thread(() => notice.Pump(target, status, resume, pause, stop)) { IsBackground = true, Name = "Maelstrom running caption" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            await notice._started.Task.WaitAsync(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
            return notice;
        }
        catch
        {
            await notice.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal static string Caption(string? status) => status?.Trim().ToUpperInvariant() switch
    {
        "RUNNING" or "MAELSTROM RUNNING" => "Maelstrom Running",
        "STOPPING" or "MAELSTROM STOPPING" => "Maelstrom Stopping",
        "STOPPED" or "MAELSTROM STOPPED" => "Maelstrom Stopped",
        _ => "Maelstrom Paused"
    };

    internal static bool ShouldShow(bool current, bool minimized, nint target, nint foreground) =>
        current && !minimized && target != 0 && target == foreground;

    internal static Rectangle CaptionBounds(Rectangle client, uint dpi)
    {
        if (client.Width <= 0 || client.Height <= 0) return Rectangle.Empty;
        var scale = Math.Clamp(dpi, 96u, 384u) / 96d;
        var width = Math.Min(client.Width, (int)Math.Ceiling(102 * scale));
        var height = Math.Min(client.Height, (int)Math.Ceiling(28 * scale));
        var inset = Math.Min(Math.Max(0, client.Height - height), (int)Math.Ceiling(2 * scale));
        return new(client.Left + (client.Width - width) / 2, client.Bottom - height - inset, width, height);
    }

    internal enum ControlButton { Play, Pause, Stop }

    internal static ControlButton ActiveControl(string? status) => Caption(status) switch
    {
        "Maelstrom Running" => ControlButton.Play,
        "Maelstrom Stopping" or "Maelstrom Stopped" => ControlButton.Stop,
        _ => ControlButton.Pause
    };

    internal static RectangleF ControlBounds(Size size, ControlButton button)
    {
        if (size.Width <= 0 || size.Height <= 0) return RectangleF.Empty;
        var cell = size.Width / 3f;
        var diameter = Math.Max(0, Math.Min(cell - 6, size.Height - 4));
        return new(cell * ((int)button + .5f) - diameter / 2, (size.Height - diameter) / 2f, diameter, diameter);
    }

    internal static ControlButton? HitControl(Size size, Point point, bool resume, bool pause, bool stop)
    {
        foreach (var button in Enum.GetValues<ControlButton>())
        {
            if (!Enabled(button, resume, pause, stop)) continue;
            var bounds = ControlBounds(size, button);
            var dx = point.X - bounds.Left - bounds.Width / 2;
            var dy = point.Y - bounds.Top - bounds.Height / 2;
            if (bounds.Width > 0 && dx * dx + dy * dy <= bounds.Width * bounds.Width / 4) return button;
        }
        return null;
    }

    private static bool Enabled(ControlButton button, bool resume, bool pause, bool stop) => button switch
    {
        ControlButton.Play => resume,
        ControlButton.Pause => pause,
        _ => stop
    };

    /// <summary>Window shape excludes all space outside the three circles, including input hit area.</summary>
    internal static Region ControlRegion(Size size)
    {
        using var path = new GraphicsPath();
        foreach (var button in Enum.GetValues<ControlButton>())
        {
            var bounds = ControlBounds(size, button);
            if (bounds.Width > 0) path.AddEllipse(bounds);
        }
        return new Region(path);
    }

    /// <summary>Opaque circle paint; the window region clips away the surrounding background.</summary>
    internal static void DrawControls(Graphics graphics, Size size, string? status, bool resume, bool pause, bool stop)
    {
        graphics.Clear(Color.FromArgb(24, 27, 36));
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var button in Enum.GetValues<ControlButton>())
        {
            var bounds = ControlBounds(size, button);
            if (bounds.Width <= 0) continue;
            using var background = new SolidBrush(Color.FromArgb(43, 47, 58));
            graphics.FillEllipse(background, bounds);
            var color = button switch { ControlButton.Play => Color.LimeGreen, ControlButton.Pause => Color.Yellow, _ => Color.Red };
            // State indication is independent of click availability: Play may be
            // non-clickable while already running but must still show green.
            if (ActiveControl(status) != button) color = Color.FromArgb(92, 97, 108);
            using var glyph = new SolidBrush(color);
            var unit = bounds.Width;
            var cx = bounds.Left + unit / 2;
            var cy = bounds.Top + unit / 2;
            if (button == ControlButton.Play)
                graphics.FillPolygon(glyph, new PointF[] { new(cx - unit * .16f, cy - unit * .24f), new(cx + unit * .24f, cy), new(cx - unit * .16f, cy + unit * .24f) });
            else if (button == ControlButton.Pause)
            {
                graphics.FillRectangle(glyph, cx - unit * .22f, cy - unit * .24f, unit * .15f, unit * .48f);
                graphics.FillRectangle(glyph, cx + unit * .07f, cy - unit * .24f, unit * .15f, unit * .48f);
            }
            else graphics.FillRectangle(glyph, cx - unit * .22f, cy - unit * .22f, unit * .44f, unit * .44f);
        }
    }

    private void Pump(GameWindow target, Func<string> status, Action? resume, Action? pause, Action? stop)
    {
        var previousDpi = SetThreadDpiAwarenessContext(new nint(-4));
        try
        {
            using var context = new ApplicationContext();
            using var window = new CaptionWindow(resume is not null, pause is not null, stop is not null)
            {
                AutoScaleMode = AutoScaleMode.None, FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false, TopMost = true, StartPosition = FormStartPosition.Manual,
                BackColor = Color.FromArgb(24, 27, 36), AccessibleName = "Maelstrom playback controls"
            };
            void Fail(Exception error)
            {
                _started.TrySetException(error); _finished.TrySetException(error);
                window.Hide(); context.ExitThread();
            }
            window.Invoked = button =>
            {
                try
                {
                    if (_stop.IsCancellationRequested || !GameWindows.IsCurrent(target) ||
                        !ShouldShow(true, GameWindows.Minimized(target), target.Handle, GetForegroundWindow())) return;
                    // Callbacks only request owner state changes. They never grant ownership.
                    var callback = button switch { ControlButton.Play => resume, ControlButton.Pause => pause, _ => stop };
                    callback?.Invoke();
                    window.Status = status(); window.Invalidate(); window.Update();
                }
                catch (Exception error) { Fail(error); }
            };
            _ = window.Handle;
            using var timer = new System.Windows.Forms.Timer { Interval = 200 };
            void Tick()
            {
                try
                {
                    if (_stop.IsCancellationRequested) { window.Hide(); context.ExitThread(); return; }
                    var current = GameWindows.IsCurrent(target);
                    if (!current) { window.Hide(); context.ExitThread(); return; }
                    if (!ShouldShow(current, GameWindows.Minimized(target), target.Handle, GetForegroundWindow()))
                    { window.Hide(); return; }
                    if (!GetClientRect(target.Handle, out var rect)) throw new Win32Exception(Marshal.GetLastWin32Error());
                    var origin = new NativePoint();
                    if (!ClientToScreen(target.Handle, ref origin)) throw new Win32Exception(Marshal.GetLastWin32Error());
                    var dpi = Math.Clamp(GetDpiForWindow(target.Handle), 96u, 384u);
                    var bounds = CaptionBounds(new(origin.X, origin.Y, rect.Right, rect.Bottom), dpi);
                    if (bounds.IsEmpty) { window.Hide(); return; }
                    window.Bounds = bounds;
                    window.Status = status();
                    // Managed Show creates normal painting state; ShowWithoutActivation and
                    // WS_EX_NOACTIVATE retain game focus. Native placement handles hidden CLI startup.
                    if (!window.Visible) window.Show();
                    if (!SetWindowPos(window.Handle, new nint(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0010 | 0x0040))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    window.Invalidate(); window.Update();
                }
                catch (Exception error) { Fail(error); }
            }
            try
            {
                timer.Tick += (_, _) => Tick();
                timer.Start(); _started.TrySetResult(); Application.Run(context);
            }
            finally { timer.Stop(); window.Hide(); }
        }
        catch (Exception error) { _started.TrySetException(error); _finished.TrySetException(error); }
        finally
        {
            if (previousDpi != 0) _ = SetThreadDpiAwarenessContext(previousDpi);
            _finished.TrySetResult();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0) return;
        _stop.Cancel();
        try { await _finished.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
        finally { if (_finished.Task.IsCompleted) _stop.Dispose(); }
    }

    private sealed class CaptionWindow : Form
    {
        private readonly bool _resume, _pause, _stop;
        private ControlButton? _pressed;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string Status { get; set; } = "Paused";
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Action<ControlButton>? Invoked { get; set; }
        internal CaptionWindow(bool resume, bool pause, bool stop)
        {
            _resume = resume; _pause = pause; _stop = stop;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override bool ShowWithoutActivation => true;
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            var previous = Region;
            Region = ControlRegion(ClientSize);
            previous?.Dispose();
        }
        protected override CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.ExStyle |= 0x08000000 | 0x80; return value; }
        }
        protected override void OnPaint(PaintEventArgs e) => DrawControls(e.Graphics, ClientSize, Status, _resume, _pause, _stop);
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) _pressed = HitControl(ClientSize, e.Location, _resume, _pause, _stop);
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            var pressed = _pressed; _pressed = null;
            if (e.Button == MouseButtons.Left && pressed is { } button && HitControl(ClientSize, e.Location, _resume, _pause, _stop) == button)
                Invoked?.Invoke(button);
            base.OnMouseUp(e);
        }
        protected override void OnVisibleChanged(EventArgs e) { _pressed = null; base.OnVisibleChanged(e); }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x84)
            {
                var bits = message.LParam.ToInt64();
                var screenPoint = new Point(unchecked((short)(bits & 0xffff)), unchecked((short)((bits >> 16) & 0xffff)));
                var hit = HitControl(ClientSize, PointToClient(screenPoint), _resume, _pause, _stop);
                message.Result = new nint(hit is null ? -1 : 1); return; // Only enabled buttons are HTCLIENT.
            }
            if (message.Msg == 0x21) { message.Result = new nint(3); return; } // MA_NOACTIVATE
            base.WndProc(ref message);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetClientRect(nint window, out NativeRect rectangle);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool ClientToScreen(nint window, ref NativePoint point);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] private static extern nint SetThreadDpiAwarenessContext(nint context);
}
