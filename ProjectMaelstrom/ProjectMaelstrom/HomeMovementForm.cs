using System.Diagnostics;
using System.Security.Cryptography;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Explicit session-only W101 home movement. Never requests foreground focus.</summary>
internal sealed class HomeMovementForm : Form
{
    private readonly string _dataRoot;
    private sealed record ModeItem(HomeIdleMode Mode, string Label) { public override string ToString() => Label; }
    private readonly ComboBox _method = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 440 };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly Label _routineName = new() { AutoSize = true, MaximumSize = new Size(650, 0) };
    private string[]? _routineActions;
    private bool _selectingMethod;
    private readonly NumericUpDown _pulse = new() { Minimum = 40, Maximum = 150, Value = 60, Width = 85 };
    private readonly Label _modeHelp = new() { AutoSize = true, MaximumSize = new Size(690, 0) };
    private readonly ComboBox _windows = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 650 };
    private readonly Button _refresh = new() { Text = "Refresh windows", AutoSize = true };
    private readonly Button _capture = new() { Text = "Capture area reference", AutoSize = true };
    private readonly CheckBox _enable = new() { Text = "Auto-Idle", AutoSize = true, Padding = new Padding(0, 5, 20, 0) };
    private readonly CheckBox _home = new() { Text = "I am at my W101 home, in clear space, with chat and menus closed.", AutoSize = true };
    private readonly NumericUpDown _interval = new() { Minimum = 60, Maximum = 300, Value = 90, Width = 85 };
    private readonly NumericUpDown _minutes = new() { Minimum = 1, Maximum = 30, Value = 15, Width = 85 };
    private readonly Button _start = new() { Text = "Start", AutoSize = true, Padding = new Padding(12, 5, 12, 5) };
    private readonly Button _stop = new() { Text = "Stop", AutoSize = true, Enabled = false, Padding = new Padding(12, 5, 12, 5) };
    private readonly Button _reports = new() { Text = "Open session reports", AutoSize = true };
    private readonly Label _status = new() { Text = "Stopped · Home movement is off.", AutoSize = true, MaximumSize = new Size(690, 0), Padding = new Padding(0, 8, 0, 8) };
    private readonly PictureBox _preview = new() { Width = 384, Height = 216, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
    private readonly System.Windows.Forms.Timer _heartbeat = new() { Interval = 250 };
    private HomeReference? _reference;
    private GameWindow? _referenceTarget;
    private CancellationTokenSource? _cancellation;
    private Task? _operation;
    private TaskCompletionSource? _finished;
    private DesktopActivityMonitor? _activity;
    private volatile bool _sessionEnabled;
    private double _lastHeartbeat;
    private bool _closing, _closedAfterStop;

    public HomeMovementForm(string? dataRoot = null, Action<string>? openPack = null, Action? openObserve = null,
        Func<Task>? beforeDancePlayer = null)
    {
        _dataRoot = dataRoot ?? WorkshopHost.DataRoot(AppContext.BaseDirectory);
        Text = "Maelstrom · Wizard101";
        AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 10);
        Size = new Size(790, 820); MinimumSize = new Size(760, 600);
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(22) };
        void Note(string text) => body.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(690, 0), Margin = new Padding(0, 6, 0, 8) });
        Note("Auto-Idle options · automation is off until you Start");
        _mode.Items.AddRange([new ModeItem(HomeIdleMode.NativeIdle, "W101 native Idle · game-controlled (recommended)"),
            new ModeItem(HomeIdleMode.SpinLeft, "Spin left · A pulses in one direction"), new ModeItem(HomeIdleMode.SpinRight, "Spin right · D pulses in one direction"),
            new ModeItem(HomeIdleMode.OpposingTurns, "Opposing turns · A then D (optional)"), new ModeItem(HomeIdleMode.WalkPairs, "Home walking · W then S (optional)"),
            new ModeItem(HomeIdleMode.Routine, "Saved motion sequence · configure in Routine builder")]);
        var loaded = new HomeMovementOptions(); string? settingsError = null;
        try { loaded = HomeMovementOptions.Load(_dataRoot); } catch (Exception ex) { settingsError = "Settings could not load: " + ex.Message; }
        _mode.SelectedIndex = _mode.Items.Cast<ModeItem>().ToList().FindIndex(m => m.Mode == loaded.Mode);
        _routineActions = loaded.RoutineActions?.ToArray();
        _interval.Value = loaded.IntervalSeconds; _minutes.Value = loaded.SessionMinutes; _pulse.Value = loaded.PulseMs;
        _method.Items.AddRange(["Spin", "Step", "Routine", "Native Idle"]);
        var methodRow = new FlowLayoutPanel { AutoSize = true }; methodRow.Controls.AddRange([_enable, _method]); body.Controls.Add(methodRow);
        body.Controls.Add(_mode); body.Controls.Add(_routineName); body.Controls.Add(_modeHelp);
        Note("Select Wizard101, capture the clear test area, then check Auto-Idle and Start. Switch to the game and leave input quiet for five seconds. Maelstrom never brings the game to the front.");
        body.Controls.Add(_windows);
        var selection = new FlowLayoutPanel { AutoSize = true }; selection.Controls.AddRange([_refresh, _capture]); body.Controls.Add(selection);
        body.Controls.Add(_preview);
        var limits = new FlowLayoutPanel { AutoSize = true };
        limits.Controls.AddRange([new Label { Text = "Interval (seconds)", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, _interval,
            new Label { Text = "Minutes", AutoSize = true, Padding = new Padding(10, 5, 0, 0) }, _minutes,
            new Label { Text = "Pulse (ms)", AutoSize = true, Padding = new Padding(10, 5, 0, 0) }, _pulse]);
        body.Controls.Add(limits); body.Controls.Add(_home);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(22, 5, 22, 10) };
        var actions = new FlowLayoutPanel { AutoSize = true }; actions.Controls.AddRange([_start, _stop, _reports]); footer.Controls.Add(actions);
        footer.Controls.Add(_status);
        Note("Any keyboard/mouse activity (including remote input), Escape, loss of focus, or a changed game view pauses the routine. Resume is explicit. It cannot keep moving while you use another app. Home identity is your confirmation; image comparison cannot prove location or guarantee that logout is prevented.");
        var idleTab = new TabPage("Auto-Idle"); idleTab.Controls.Add(body); idleTab.Controls.Add(footer);
        var builderTab = new TabPage("Routine builder");
        builderTab.Controls.Add(new W101RoutineBuilder(_dataRoot, routine =>
        {
            if (_operation != null) { _status.Text = "Stop the current session before selecting a different routine."; return; }
            _routineActions = routine.Actions.ToArray(); SetMode(HomeIdleMode.Routine); _enable.Checked = false; ClearReference(); ApplyMode(); SaveOptions(); _tabs.SelectedTab = idleTab;
        }, openPack));
        var observeTab = new TabPage("Observe & teach");
        var teach = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), FlowDirection = FlowDirection.TopDown };
        teach.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(650, 0), Text = "Attach Wizard101 in Observe, record a demonstration, label game states, then build and test a pack. The Pet Dance player can run one game with visible Start and Stop controls. Other routine categories remain drafts." });
        var observe = new Button { Text = "Open Observe workspace", AutoSize = true, Enabled = openObserve != null }; observe.Click += (_, _) => openObserve?.Invoke(); teach.Controls.Add(observe); observeTab.Controls.Add(teach);
        var clientObservation = new Button { Text = "Optional client observation…", AutoSize = true };
        clientObservation.Click += (_, _) =>
        {
            using var connection = new WizwalkerConnectionForm(_dataRoot, _windows.SelectedItem as GameWindow);
            connection.ShowDialog(this);
        };
        teach.Controls.Add(clientObservation);
        var dancePlayer = new Button { Text = "Pet Dance player…", AutoSize = true };
        dancePlayer.Click += async (_, _) =>
        {
            if (_operation != null)
            { MessageBox.Show(this, "Stop the Auto-Idle session before opening the Pet Dance player.", "Pet Dance player", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            dancePlayer.Enabled = false;
            try
            {
                if (beforeDancePlayer != null) await beforeDancePlayer();
                using var player = new PetDancePlayerForm(_dataRoot, _windows.SelectedItem as GameWindow);
                player.ShowDialog(this);
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Pet Dance player", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            finally { if (!IsDisposed) dancePlayer.Enabled = true; }
        };
        teach.Controls.Add(dancePlayer);
        var danceTeacher = new Button { Text = "Pet Dance teacher…", AutoSize = true };
        danceTeacher.Click += (_, _) => { using var teacher = new PetDanceTeacherForm(_dataRoot, openPack); teacher.ShowDialog(this); };
        teach.Controls.Add(danceTeacher);
        teach.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(650, 0), Text = "The Pet Dance teacher imports local legacy arrow images, calibrates their scale, and reviews recorded Watch/Go rounds. It saves action proposals in a draft pack without sending game inputs." });
        var legacyTab = new TabPage("Legacy library"); legacyTab.Controls.Add(new W101LegacyLibrary());
        var runsTab = new TabPage("Routine runs"); runsTab.Controls.Add(new OperationRunsView(_dataRoot));
        _tabs.TabPages.AddRange([builderTab, idleTab, observeTab, runsTab, legacyTab]); Controls.Add(_tabs);
        _refresh.Click += (_, _) => RefreshWindows();
        _windows.SelectedIndexChanged += (_, _) => ClearReference();
        _home.CheckedChanged += (_, _) => RefreshControls();
        _enable.CheckedChanged += (_, _) => { _sessionEnabled = _enable.Checked; if (!_sessionEnabled) _cancellation?.Cancel(); RefreshControls(); };
        _capture.Click += async (_, _) => await WorkAsync(CaptureAsync);
        _start.Click += async (_, _) =>
        {
            if (!SaveOptions()) return;
            if (SelectedMode == HomeIdleMode.NativeIdle) { _status.Text = "In W101, press Escape and choose Idle. Look for YOU ARE IDLE. Maelstrom is stopped; no keys or hooks are active."; return; }
            await WorkAsync(StartAsync);
        };
        _mode.SelectedIndexChanged += (_, _) => { if (_selectingMethod) return; _enable.Checked = false; ClearReference(); ApplyMode(); SaveOptions(); };
        _method.SelectedIndexChanged += (_, _) =>
        {
            if (_selectingMethod) return;
            SetMode(_method.SelectedIndex switch { 0 => HomeIdleMode.SpinLeft, 1 => HomeIdleMode.WalkPairs, 2 => HomeIdleMode.Routine, _ => HomeIdleMode.NativeIdle });
            _enable.Checked = false; ClearReference(); ApplyMode(); SaveOptions();
        };
        foreach (var number in new[] { _interval, _minutes, _pulse }) number.ValueChanged += (_, _) => SaveOptions();
        _stop.Click += (_, _) => { _sessionEnabled = false; _cancellation?.Cancel(); _status.Text = "Stopping · releasing inputs and saving the report…"; };
        _reports.Click += (_, _) =>
        {
            try { var folder = Path.Combine(_dataRoot, "home-movement"); Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); }
            catch (Exception ex) { _status.Text = ex.Message; }
        };
        _heartbeat.Tick += (_, _) => Volatile.Write(ref _lastHeartbeat, FrameTimeline.TimestampMs());
        FormClosing += async (_, e) =>
        {
            if (_closedAfterStop || _operation == null) return;
            e.Cancel = true; if (_closing) return; _closing = true;
            await StopForOwnerCloseAsync(); _closedAfterStop = true; Close();
        };
        FormClosed += (_, _) => { _heartbeat.Dispose(); _activity?.Dispose(); _preview.Image?.Dispose(); };
        RefreshWindows();
        ApplyMode();
        if (settingsError != null) _status.Text = settingsError;
    }

    private HomeIdleMode SelectedMode => (_mode.SelectedItem as ModeItem)?.Mode ?? HomeIdleMode.NativeIdle;
    private HomeMovementOptions Options() => new((int)_interval.Value, (int)_minutes.Value, (int)_pulse.Value, SelectedMode, _routineActions?.ToArray());
    private void SetMode(HomeIdleMode mode)
    {
        _selectingMethod = true;
        try { _mode.SelectedIndex = _mode.Items.Cast<ModeItem>().ToList().FindIndex(item => item.Mode == mode); }
        finally { _selectingMethod = false; }
    }
    private bool SaveOptions()
    {
        try { Options().Save(_dataRoot); return true; }
        catch (Exception ex) { _status.Text = "Could not save Auto-Idle choices: " + ex.Message; return false; }
    }
    private void ApplyMode()
    {
        _selectingMethod = true;
        _method.SelectedIndex = SelectedMode switch { HomeIdleMode.NativeIdle => 3, HomeIdleMode.WalkPairs => 1, HomeIdleMode.Routine => 2, _ => 0 };
        _selectingMethod = false;
        _routineName.Visible = SelectedMode == HomeIdleMode.Routine;
        _routineName.Text = _routineActions is { Length: > 0 } ? "Sequence: " + string.Join(" → ", _routineActions.Select(id => W101Routine.Resolve(id).Title)) : "Choose and save a sequence in Routine builder, then select Use for Auto-Idle.";
        _modeHelp.Text = SelectedMode switch
        {
            HomeIdleMode.NativeIdle => "Use W101's Escape → Idle button. The game owns this mode; Maelstrom provides instructions and sends no inputs. This supports keeping the game open while you use other apps.",
            HomeIdleMode.SpinLeft => "Brief A pulses turn left in place. Maelstrom checks the game before and after each turn.",
            HomeIdleMode.SpinRight => "Brief D pulses turn right in place. Maelstrom checks the game before and after each turn.",
            HomeIdleMode.OpposingTurns => "Optional A then D pulses. This alternates direction and may not restore exactly the same heading.",
            HomeIdleMode.Routine => "Run the selected motion actions in order. Each key requires a fresh observation and verified scene change before the next key. Unknown outcomes pause the chain. This is not a route or a complete activity player.",
            _ => "Optional W then S steps at your home only. This can drift or meet an obstacle; keep ample clear space."
        };
        _home.Text = Options().RequiresHome ? "I am at my W101 home, in clear space, with chat and menus closed." : "I am in a clear W101 test area, with chat and menus closed.";
        _start.Text = SelectedMode == HomeIdleMode.NativeIdle ? "Native Idle instructions" : "Start";
        RefreshControls();
    }

    private void RefreshWindows()
    {
        _windows.Items.Clear();
        foreach (var window in GameWindows.List().Where(HomeMovementGate.IsWizard101)) _windows.Items.Add(window);
        if (_windows.Items.Count > 0) _windows.SelectedIndex = 0;
        ClearReference();
        _status.Text = _windows.Items.Count == 0 ? "Stopped · Open Wizard101 to select its window. Nothing is running." : "Stopped · Capture a reference in your clear test area.";
    }
    private void ClearReference()
    {
        _reference = null; _referenceTarget = null; _home.Checked = false;
        _preview.Image?.Dispose(); _preview.Image = null; _start.Text = "Start"; RefreshControls();
    }
    private void RefreshControls()
    {
        if (IsDisposed) return;
        var idle = _operation == null;
        var native = SelectedMode == HomeIdleMode.NativeIdle;
        _method.Enabled = _mode.Enabled = idle;
        _windows.Enabled = _refresh.Enabled = _home.Enabled = _interval.Enabled = _minutes.Enabled = _pulse.Enabled = idle && !native;
        _enable.Enabled = !native;
        _preview.Visible = !native && _preview.Image != null;
        _capture.Enabled = idle && !native && _windows.SelectedItem is GameWindow;
        _start.Enabled = idle && (native || (_enable.Checked && _home.Checked && _reference is { Usable: true } && _referenceTarget == (_windows.SelectedItem as GameWindow)));
        _stop.Enabled = !idle;
    }
    private async Task CaptureAsync(CancellationToken token)
    {
        var target = _windows.SelectedItem as GameWindow ?? throw new InvalidOperationException("Select Wizard101 first.");
        var clientSize = GameWindows.ClientSize(target);
        ClearReference(); _status.Text = "Capturing only the selected game window…";
        using var sample = await Task.Run(() => WindowsHomeMovementRuntime.CaptureAsync(target, null, token), token);
        token.ThrowIfCancellationRequested();
        if (sample == null || !GameWindows.IsCurrent(target) || GameWindows.ClientSize(target) != clientSize) throw new InvalidOperationException("No stable fresh game image is available. Leave Wizard101 visible and try again.");
        var reference = new HomeReference(HomeView.From(sample.Image), clientSize);
        _preview.Image = new Bitmap(sample.Image);
        if (!reference.Usable) throw new InvalidOperationException("The reference needs a visible scene and both lower HUD corners. Use 1280 × 720 windowed mode, close menus, and recapture.");
        _reference = reference; _referenceTarget = target;
        _status.Text = "Reference captured · confirm the clear test area, check Auto-Idle, then Start.";
    }
    private async Task StartAsync(CancellationToken token)
    {
        var target = _windows.SelectedItem as GameWindow;
        if (!_home.Checked || !_enable.Checked || _reference == null || target == null || target != _referenceTarget || !GameWindows.IsCurrent(target))
            throw new InvalidOperationException("Select the current game, capture its clear test area and confirm this session first.");
        if (GameWindows.ClientSize(target) != _reference.ClientSize) throw new InvalidOperationException("Wizard101 changed size. Capture a new home reference.");
        var options = Options();
        var tag = (nuint)(BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8)) | 1UL);
        _activity = DesktopActivityMonitor.Install(tag);
        _sessionEnabled = true; Volatile.Write(ref _lastHeartbeat, FrameTimeline.TimestampMs()); _heartbeat.Start();
        var activity = _activity;
        WindowsHomeMovementRuntime? runtime = null;
        var progress = new Progress<string>(message => { if (!IsDisposed && !_closing && _operation != null) _status.Text = message; });
        var reference = _reference;
        HomeMovementRunResult? movement = null;
        var job = HomeMovementOperation.Prepare(target, options, async (context, cancellation) =>
        {
            runtime = new WindowsHomeMovementRuntime(target, reference.ClientSize, activity, tag,
                () => _sessionEnabled && HomeMovementOperation.Owned(context, target), () => Volatile.Read(ref _lastHeartbeat));
            movement = await HomeMovementRun.RunAsync(runtime, target, reference, options, _dataRoot, true, progress, cancellation).ConfigureAwait(false);
            return movement;
        }, () => runtime!.Signal,
            (context, baseline, cancellation) => PetDanceNativeIdle.RunWithFailsafeAsync(context, target,
                reference.ClientSize, context.Directory, activity, baseline, tag,
                () => FrameTimeline.TimestampMs() - Volatile.Read(ref _lastHeartbeat) < 1500,
                () => _sessionEnabled, cancellation));
        var ticket = ApplicationOperations.For(_dataRoot).Submit(job.Request, job.Execute);
        using var registration = token.Register(ticket.Cancel);
        var receipt = await ticket.Completion;
        var result = movement;
        if (result == null) { _status.Text = receipt.State + " · " + receipt.Message; return; }
        _status.Text = $"{result.State} · {HomeMovementMessages.Explain(result.Reason)}. Completed cycles: {result.CompletedCycles}. {receipt.Message} " +
            (result.State == HomeMovementState.Paused ? "Check the game before choosing Resume. Recapture if your view changed." : "Choose Start to begin another bounded session.");
        _start.Text = result.State == HomeMovementState.Paused ? "Resume" : "Start";
    }
    private async Task WorkAsync(Func<CancellationToken, Task> work)
    {
        if (_operation != null) return;
        using var cancellation = new CancellationTokenSource(); _cancellation = cancellation;
        _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _operation = Task.CompletedTask; RefreshControls();
        try { _operation = work(cancellation.Token); await _operation; }
        catch (OperationCanceledException) { _status.Text = "Stopped · no further movement is scheduled."; }
        catch (Exception ex) { _status.Text = "Paused · " + ex.Message; }
        finally
        {
            _sessionEnabled = false; _heartbeat.Stop(); _activity?.Dispose(); _activity = null;
            _cancellation = null; _operation = null; RefreshControls();
            _finished.TrySetResult();
        }
    }
    internal async Task StopForOwnerCloseAsync()
    {
        _sessionEnabled = false; _cancellation?.Cancel();
        if (_operation != null && _finished != null) await _finished.Task;
    }
    internal void RenderForVerification(string directory)
    {
        Directory.CreateDirectory(directory); _ = Handle; PerformLayout();
        void CreateHandles(Control control)
        {
            _ = control.Handle;
            foreach (Control child in control.Controls) CreateHandles(child);
        }
        CreateHandles(this); PerformLayout();
        using var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
        foreach (Control child in Controls) child.DrawToBitmap(bitmap, child.Bounds);
        bitmap.Save(Path.Combine(directory, "home-movement.png"));
        _tabs.SelectedIndex = 1; PerformLayout();
        foreach (Control child in Controls) child.DrawToBitmap(bitmap, child.Bounds);
        bitmap.Save(Path.Combine(directory, "auto-idle.png"));
        _tabs.SelectedIndex = 3; PerformLayout();
        foreach (Control child in Controls) child.DrawToBitmap(bitmap, child.Bounds);
        bitmap.Save(Path.Combine(directory, "routine-runs.png"));
        _tabs.SelectedIndex = 4; PerformLayout();
        foreach (Control child in Controls) child.DrawToBitmap(bitmap, child.Bounds);
        bitmap.Save(Path.Combine(directory, "legacy-library.png"));
        GameExperiments.WriteAtomic(Path.Combine(directory, "home-movement-ui.json"), new
        {
            enabled_by_default = _enable.Checked,
            home_confirmed_by_default = _home.Checked,
            start_enabled = _start.Enabled,
            stop_enabled = _stop.Enabled,
            active = _operation != null,
            legacy_project_count = W101LegacyCatalog.Load().Entries.Length,
            operation_receipts = _tabs.TabPages[3].Controls.OfType<OperationRunsView>().Single().EvidenceRows,
            window_count = _windows.Items.Count,
            selected_method = _method.SelectedItem?.ToString(),
            selected_mode = SelectedMode.ToString(),
            methods = _method.Items.Cast<string>().ToArray(),
            tabs = _tabs.TabPages.Cast<TabPage>().Select(tab => tab.Text).ToArray()
        });
    }
}
