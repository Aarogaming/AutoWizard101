using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ProjectMaelstrom.Utilities;

/// <summary>Persistent, nonactivating manual-capture overlay with explicit hotkeys.
/// The host never dispatches game input, never activates a window and owns no input
/// lease. All callbacks only request state changes from <see cref="ManualCaptureController"/>.
/// A hidden/dormant game window is not a target: identity and ownership are checked
/// before the gate opens and on every refresh.</summary>
internal sealed class ManualCaptureHost : IAsyncDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkP = 0x50;   // pause
    private const uint VkS = 0x53;   // stop
    private const uint VkR = 0x52;   // play / explicit resume
    private const uint VkQ = 0x51;   // hard stop

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly CancellationTokenSource _stop;
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ManualCapturePanel _panel;

    private ManualCaptureHost(ManualCapturePanel panel, CancellationToken token)
    {
        _panel = panel;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
    }

    internal ManualCaptureSnapshot Snapshot => _panel.Controller.Snapshot(_panel.NowMs);
    internal int RegisteredHotkeys => _panel.RegisteredHotkeys;

    internal static async Task<ManualCaptureHost> StartAsync(GameWindow target, ManualCaptureMode mode, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(target);
        token.ThrowIfCancellationRequested();
        var panel = new ManualCapturePanel(target, mode);
        var host = new ManualCaptureHost(panel, token);
        panel.StopRequested = () => host._stop.Cancel();
        var thread = new Thread(() => host.Pump(target)) { IsBackground = true, Name = "Maelstrom manual-capture overlay" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            await host._started.Task.WaitAsync(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
            return host;
        }
        catch
        {
            await host.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private void Pump(GameWindow target)
    {
        try
        {
            _panel.CreateControl();
            var handle = _panel.Handle;
            if (!RegisterHotKeys(handle)) throw new InvalidOperationException("Manual-capture hotkeys could not be registered.");
            _started.TrySetResult();
            Application.Run(_panel);
        }
        catch (Exception error)
        {
            _started.TrySetException(error);
            _finished.TrySetException(error);
        }
        finally
        {
            _finished.TrySetResult();
        }
        _ = target;
    }

    private bool RegisterHotKeys(IntPtr handle)
    {
        var ok = RegisterHotKey(handle, 1, ModAlt | ModControl, VkP)
              & RegisterHotKey(handle, 2, ModAlt | ModControl, VkS)
              & RegisterHotKey(handle, 3, ModAlt | ModControl, VkR)
              & RegisterHotKey(handle, 4, ModAlt | ModControl, VkQ);
        if (ok) _panel.RegisteredHotkeys = 4;
        return ok;
    }

    internal void UnregisterHotKeys()
    {
        if (!_panel.IsHandleCreated || _panel.IsDisposed) return;
        var handle = _panel.Handle;
        for (var id = 1; id <= 4; id++) UnregisterHotKey(handle, id);
        _panel.RegisteredHotkeys = 0;
    }

    public async ValueTask DisposeAsync()
    {
        if (_stop.IsCancellationRequested) return;
        _stop.Cancel();
        if (_panel.IsHandleCreated && !_panel.IsDisposed) _panel.BeginInvoke(new Action(() => Application.ExitThread()));
        await _finished.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        UnregisterHotKeys();
        _panel.Dispose();
        _stop.Dispose();
    }

    /// <summary>Nonactivating overlay. Buttons and hotkeys only request controller
    /// transitions; the controller still refuses dispatch unless its gates pass.</summary>
    private sealed class ManualCapturePanel : Form
    {
        private readonly GameWindow _target;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Label _status;
        private readonly Button _play, _pause, _stop;
        private DesktopActivityMonitor? _activity;
        private long _lastRevision = -1;
        private long _startedTicks = Environment.TickCount64;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal ManualCaptureController Controller { get; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal int RegisteredHotkeys { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal Action? StopRequested { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal double NowMs => Environment.TickCount64 - _startedTicks;

        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get
            {
                var value = base.CreateParams;
                value.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE - clicks cannot steal focus from W101
                return value;
            }
        }

        internal ManualCapturePanel(GameWindow target, ManualCaptureMode mode)
        {
            _target = target;
            Controller = new ManualCaptureController(mode,
                () => GameWindows.Read(_target.Handle) is { } current && GameWindows.SameIdentity(_target, current) &&
                      GameInputLease.Current?.RecoveryRequired != true,
                () => GameInputLease.Current?.RecoveryRequired == true);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = System.Drawing.Color.FromArgb(18, 24, 38);
            ForeColor = System.Drawing.Color.Gold;
            StartPosition = FormStartPosition.Manual;
            Location = new System.Drawing.Point(40, 40);
            Size = new System.Drawing.Size(320, 96);
            AccessibleName = "Maelstrom manual capture controls";

            _status = new Label { Location = new System.Drawing.Point(10, 8), AutoSize = true, Text = "Idle" };
            _play = new Button { Text = "Play", Location = new System.Drawing.Point(10, 40), Size = new System.Drawing.Size(90, 40) };
            _pause = new Button { Text = "Pause", Location = new System.Drawing.Point(112, 40), Size = new System.Drawing.Size(90, 40) };
            _stop = new Button { Text = "Stop", Location = new System.Drawing.Point(214, 40), Size = new System.Drawing.Size(90, 40) };
            _play.Click += (_, _) => RequestPlay();
            _pause.Click += (_, _) => Controller.ExplicitPause(NowMs);
            _stop.Click += (_, _) => Controller.Stop(NowMs);
            Controls.AddRange(new Control[] { _status, _play, _pause, _stop });

            _timer = new System.Windows.Forms.Timer { Interval = 100 };
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { _activity = DesktopActivityMonitor.Install(0xC0FFEE, keyboardOnly: false, activityTarget: _target.Handle); }
            catch { _activity = null; }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey)
            {
                switch (m.WParam.ToInt32())
                {
                    case 1: Controller.ExplicitPause(NowMs); break;
                    case 2: Controller.Stop(NowMs); break;
                    case 3: RequestPlay(); break;
                    case 4: Controller.Stop(NowMs); StopRequested?.Invoke(); break;
                }
                return;
            }
            base.WndProc(ref m);
        }

        private void RequestPlay()
        {
            var now = NowMs;
            if (Controller.Snapshot(now).State == ManualCaptureState.Stopped) Controller.Start(now);
            else Controller.Resume(now);
        }

        private void Tick()
        {
            try
            {
                var now = NowMs;
                if (!GameWindows.IsCurrent(_target)) Controller.ImplicitPause(ManualPauseTrigger.FocusLost, now);
                if (_activity != null)
                {
                    var revision = _activity.Revision;
                    if (_lastRevision >= 0 && revision != _lastRevision) Controller.ImplicitPause(ManualPauseTrigger.MouseActivity, now);
                    _lastRevision = revision;
                }
                Controller.Observe(now);
                var s = Controller.Snapshot(now);
                _status.Text = $"{s.Mode} · {s.State} · {(s.MayDispatch ? "ARMED" : "BLOCKED")} · {s.Reason}";
                _play.Enabled = s.State is ManualCaptureState.Stopped or ManualCaptureState.ExplicitlyPaused or ManualCaptureState.ImplicitlyPaused;
                _pause.Enabled = s.State is ManualCaptureState.Running or ManualCaptureState.ResumePending;
            }
            catch { Controller.Stop(NowMs); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _activity?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
