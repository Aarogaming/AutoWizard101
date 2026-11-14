using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

internal sealed class GameWorkspaceForm : Form
{
    private readonly string _dataRoot;
    private readonly ComboBox _windows = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 650, DropDownWidth = 900, AccessibleName = "Game window" };
    private readonly TextBox _name = new() { Width = 260, MaxLength = 120, Text = "My game", AccessibleName = "Workspace name" };
    private readonly Button _attach = new() { Text = "Observe selected window", AutoSize = true };
    private readonly Button _pause = new() { Text = "Pause", AutoSize = true, Enabled = false };
    private readonly Button _refresh = new() { Text = "Refresh windows", AutoSize = true };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8), Text = "Choose your game's window, then select Observe. No keyboard or mouse actions are sent." };
    private readonly PictureBox _preview = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(28, 30, 34), SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = "Selected game capture preview" };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private WindowObserver? _observer;
    private bool _busy, _changing, _closing, _closeReady, _dodgeFocusPending;
    private bool _hasEverFrame;
    private int _observationGeneration;
    private DateTime _lastFrame;
    private string? _preferredProcess;
    private bool _dodgeWorkspace;
    private readonly List<Control> _wizardOnlyControls = new();
    private TeachingRecording? _recording;
    private readonly Button _record = new() { Text = "Record stills", AutoSize = true, Enabled = false };
    private readonly Button _video = new() { Text = "Record video (preview)", AutoSize = true, Enabled = false };
    private readonly CheckBox _pausePreviewForVideo = new() { Text = "Pause preview during video", AutoSize = true, AccessibleName = "Pause live preview while recording video" };
    private readonly CheckBox _recordGameInput = new() { Text = "Record game keys and mouse", AutoSize = true, AccessibleName = "Record selected game keys and mouse while the game has focus" };
    private readonly CheckBox _recordGameAudio = new() { Text = "Record game audio", AutoSize = true, AccessibleName = "Record only the selected game's audio; no microphone" };
    private CancellationTokenSource? _videoStop;
    private Task? _videoTask;
    private int _videoHasFrame;
    private bool _videoNotified;
    private string? _lastVideoDirectory;
    private readonly Button _importVideo = new() { Text = "Sample video for labels…", AutoSize = true };
    private readonly NumericUpDown _sampleRate = new() { Minimum = 1, Maximum = 60, Value = 5, Width = 55, AccessibleName = "Video samples per second" };
    private readonly NumericUpDown _sampleStart = new() { Minimum = 0, Maximum = 86400, Value = 0, Width = 80, AccessibleName = "Video sample start in seconds" };
    private CancellationTokenSource? _importStop;
    private Task<TeachingRecording>? _importTask;
    private CancellationTokenSource? _dodgeExportStop;
    private Task<string>? _dodgeExportTask;
    private CancellationTokenSource? _dodgeRunStop;
    private Task<string>? _dodgeRunTask;
    private readonly Button _review = new() { Text = "Review and label", AutoSize = true, Enabled = false };
    private readonly Button _openRecording = new() { Text = "Open recording…", AutoSize = true };
    private readonly Label _recordStatus = new() { AutoSize = true, MaximumSize = new Size(980, 0), Text = "Still recording off · up to 600 images at 1 per second · video preview is separate" };
    private readonly Action<string>? _openWorkshop;
    private readonly Action<string>? _openDodgeReplay;
    private string? _lastDodgeSession;
    private VisualStateRecognizer? _recognizer;
    private int _recognitionEpoch;
    private readonly Label _recognitionStatus = new() { AutoSize = true, MaximumSize = new Size(980, 0), Text = "Visual recognition off · Learn from a teaching pack with at least two states." };
    private readonly CheckBox _dodgeAdvice = new() { Text = "Dodge specialist (advice only)", AutoSize = true, AccessibleName = "Show experimental Dodge movement advice without sending input" };
    private readonly Label _dodgeStatus = new() { AutoSize = true, MaximumSize = new Size(980, 0), Text = "Dodge specialist off · no model or controls." };
    private readonly Button _learn = new() { Text = "Learn from teaching pack…", AutoSize = true };
    private readonly Button _evaluate = new() { Text = "Check on another recording…", AutoSize = true, Enabled = false };
    private string? _pendingPack;
    private string? _pendingCheck;
    private GameplaySamplePlan? _samplePlan;
    private GameResearch? _research;

    public GameWorkspaceForm(Action<string>? openWorkshop = null, string? dataRoot = null, Action<string>? openDodgeReplay = null)
    {
        _dataRoot = dataRoot ?? WorkshopHost.DataRoot(AppContext.BaseDirectory);
        _openWorkshop = openWorkshop;
        _openDodgeReplay = openDodgeReplay;
        Text = "Maelstrom · Game workspace · Observe";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Size = new Size(1100, 800);
        MinimumSize = new Size(850, 650);
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7, ColumnCount = 1, Padding = new Padding(18) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var heading = new Label { AutoSize = true, Text = "Game workspace  /  Observe", Font = new Font(Font.FontFamily, 20, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12) };
        layout.Controls.Add(heading);
        var selection = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        selection.Controls.Add(_windows);
        selection.Controls.Add(_refresh);
        layout.Controls.Add(selection);
        var commands = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8) };
        commands.Controls.Add(new Label { Text = "Workspace", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        commands.Controls.Add(_name);
        commands.Controls.Add(_attach);
        commands.Controls.Add(_pause);
        layout.Controls.Add(commands);
        var teaching = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        teaching.Controls.Add(_record);
        teaching.Controls.Add(_video);
        var sessionWorkshop = new Button { Text = "Long session / timeline…", AutoSize = true };
        teaching.Controls.Add(sessionWorkshop);
        sessionWorkshop.Click += async (_, _) =>
        {
            if (_videoTask != null || _recording is { Complete: false } || _importTask != null || _dodgeRunTask != null)
            { _recordStatus.Text = "Finish the current recording or experiment before opening the session timeline."; return; }
            var target = _observer?.Target ?? _windows.SelectedItem as GameWindow;
            await StopAsync("Observe paused for the session timeline. Reconnect when finished.");
            using var timeline = new SessionTimelineForm(_dataRoot, target);
            timeline.ShowDialog(this);
        };
        teaching.Controls.Add(_recordGameInput);
        teaching.Controls.Add(_recordGameAudio);
        teaching.Controls.Add(_pausePreviewForVideo);
        teaching.Controls.Add(_importVideo);
        teaching.Controls.Add(new Label { Text = "samples/s", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        teaching.Controls.Add(_sampleRate);
        teaching.Controls.Add(new Label { Text = "start at (seconds)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        teaching.Controls.Add(_sampleStart);
        teaching.Controls.Add(_review);
        teaching.Controls.Add(_openRecording);
        var danceTeacher = new Button { Text = "Pet Dance teacher…", AutoSize = true };
        danceTeacher.Click += (_, _) => { using var form = new PetDanceTeacherForm(_dataRoot, _openWorkshop); form.ShowDialog(this); };
        teaching.Controls.Add(danceTeacher);
        var dancePlayer = new Button { Text = "Pet Dance player…", AutoSize = true, AccessibleName = "Open the selected Wizard101 window in the Pet Dance player" };
        dancePlayer.Click += async (_, _) =>
        {
            if (_changing || _closing || _videoTask != null || _recording is { Complete: false } ||
                _importTask != null || _dodgeExportTask != null || _dodgeRunTask != null)
            { _status.Text = "Finish or stop the current recording or test before opening the Pet Dance player."; return; }
            var target = _observer?.Target ?? _windows.SelectedItem as GameWindow;
            if (target == null || !HomeMovementGate.IsWizard101(target))
            { _status.Text = "Select Wizard101 in the game-window list before opening its Pet Dance player."; return; }
            dancePlayer.Enabled = false;
            try
            {
                await PreparePetDancePlayerAsync();
                using var form = new PetDancePlayerForm(_dataRoot, target);
                form.ShowDialog(this);
            }
            catch (Exception error) { _status.Text = error.Message; }
            finally { if (!_closing) dancePlayer.Enabled = true; }
        };
        teaching.Controls.Add(dancePlayer);
        _wizardOnlyControls.AddRange([danceTeacher, dancePlayer]);
        var dodgeReplay = new Button { Text = "Prepare Dodge replay…", AutoSize = true, AccessibleName = "Prepare a read-only Dodge replay from a recording" };
        var openDodgeButton = new Button { Text = "Open Dodge replay in Workshop", AutoSize = true, Enabled = false };
        teaching.Controls.Add(dodgeReplay);
        teaching.Controls.Add(openDodgeButton);
        openDodgeButton.Click += (_, _) => { if (_lastDodgeSession != null) _openDodgeReplay?.Invoke(_lastDodgeSession); };
        var dodgeTest = new Button { Text = "Run Dodge test…", AutoSize = true, AccessibleName = "Run a short, target-bound Dodge test" };
        teaching.Controls.Add(dodgeTest);
        dodgeTest.Click += async (_, _) =>
        {
            if (_dodgeRunStop != null) { _dodgeRunStop.Cancel(); return; }
            if (_dodgeFocusPending) return;
            var observer = _observer;
            if (observer == null || _videoTask != null || _recording is { Complete: false } || _importTask != null || _dodgeExportTask != null)
            { _recordStatus.Text = "Start Observe and stop recording or frame analysis before the Dodge test."; return; }
            if (!ExecutionPolicyManager.Current.AllowLiveAutomation) { _recordStatus.Text = "Game controls are off. Enable them in Tools before running a bounded Dodge test."; return; }
            using var picker = new OpenFileDialog { Title = "Choose a saved Dodge schema 2 pack", Filter = "Game pack|pack.json", InitialDirectory = Path.Combine(_dataRoot, "game-packs") };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            DodgeLivePack pack;
            try { pack = DodgeLivePack.Load(picker.FileName); }
            catch (Exception ex) { _recordStatus.Text = "Cannot run this pack: " + ex.Message; return; }
            if (pack.Context != null) { _recordStatus.Text = "Use the isolated visual Dodge test for this context-aware pack. No inputs sent."; return; }
            if (_busy) { _recordStatus.Text = "A preview frame is still processing. Try the Dodge test again."; return; }
            bool focused;
            _dodgeFocusPending = true;
            try { focused = await WindowsDodgeKeySink.TryFocusAsync(observer.Target); }
            finally { _dodgeFocusPending = false; }
            if (_closing || IsDisposed) return;
            if (!focused) { _recordStatus.Text = "Game focus was unavailable or you switched apps. No inputs sent."; return; }
            if (_observer != observer || _busy || _videoTask != null || _recording is { Complete: false } || _importTask != null || _dodgeExportTask != null || !ExecutionPolicyManager.Current.AllowLiveAutomation)
            { _recordStatus.Text = "The workspace changed while activating the game. No inputs sent."; return; }
            var generation = _observationGeneration;
            var cancellation = new CancellationTokenSource();
            _dodgeRunStop = cancellation;
            _timer.Stop();
            _video.Enabled = _record.Enabled = false;
            _importVideo.Enabled = dodgeReplay.Enabled = false;
            dodgeTest.Text = "Stop Dodge test";
            _recordStatus.Text = "Running a bounded Dodge test · up to 8 seconds / 10 actions · Escape or focus change stops input.";
            try
            {
                var task = Task.Run(() => DodgeLiveRun.RunAsync(observer, pack, _dataRoot, () => ExecutionPolicyManager.Current.AllowLiveAutomation, cancellation.Token));
                _dodgeRunTask = task;
                var report = await task;
                if (!IsDisposed && !_closing) _recordStatus.Text = $"Dodge test finished. Review observed movement and stop reason: {report}";
            }
            catch (Exception ex) { if (!IsDisposed && !_closing) _recordStatus.Text = "Dodge test could not run: " + ex.Message; }
            finally
            {
                _dodgeRunTask = null;
                _dodgeRunStop = null;
                cancellation.Dispose();
                if (!IsDisposed)
                {
                    dodgeTest.Text = "Run Dodge test…";
                    _importVideo.Enabled = dodgeReplay.Enabled = true;
                    if (!_closing && generation == _observationGeneration && _observer == observer)
                    {
                        _timer.Start();
                        _video.Enabled = _record.Enabled = true;
                    }
                }
            }
        };
        dodgeReplay.Click += async (_, _) =>
        {
            if (_dodgeExportStop != null) { _dodgeExportStop.Cancel(); return; }
            using var picker = new OpenFileDialog { Title = "Choose a saved Dodge recording.json", Filter = "Recording manifest|recording.json", InitialDirectory = Path.Combine(_dataRoot, "recordings") };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            var cancellation = new CancellationTokenSource();
            _dodgeExportStop = cancellation;
            dodgeReplay.Text = "Stop Dodge analysis";
            try
            {
                var recording = TeachingRecording.Open(picker.FileName);
                _recordStatus.Text = "Preparing Dodge replay from saved frames · no model or inputs · stop at any time.";
                var task = Task.Run(() => DodgeSessionExport.RunAsync(recording, _dataRoot, cancellation.Token));
                _dodgeExportTask = task;
                var session = await task;
                if (!IsDisposed && !_closing)
                {
                    _lastDodgeSession = session;
                    openDodgeButton.Enabled = _openDodgeReplay != null;
                    _recordStatus.Text = $"Dodge replay ready: {session} · Open it in Workshop to evaluate. Predictions need review.";
                }
            }
            catch (OperationCanceledException) { if (!IsDisposed) _recordStatus.Text = "Dodge analysis stopped."; }
            catch (Exception ex) { if (!IsDisposed && !_closing) _recordStatus.Text = "Dodge analysis failed: " + ex.Message; }
            finally
            {
                _dodgeExportTask = null;
                _dodgeExportStop = null;
                cancellation.Dispose();
                if (!IsDisposed) dodgeReplay.Text = "Prepare Dodge replay…";
            }
        };
        var sampler = new Button { Text = "Gameplay Sampler…", AutoSize = true };
        var testGames = new Button { Text = "Local test games…", AutoSize = true };
        testGames.Click += (_, _) => { using var games = new TestGamesForm(); games.ShowDialog(this); RefreshWindows(); };
        teaching.Controls.Add(testGames);
        var research = new Button { Text = "Game Research…", AutoSize = true };
        teaching.Controls.Add(research);
        research.Click += (_, _) =>
        {
            using var form = new GameResearchForm(_dataRoot, _name.Text);
            if (form.ShowDialog(this) == DialogResult.OK && form.Selected != null)
            {
                _research = form.Selected;
                _recordStatus.Text = $"Research selected: {_research.Game} · Hypothesis ready for the sampler (not verified).";
            }
        };
        teaching.Controls.Add(sampler);
        sampler.Click += (_, _) =>
        {
            if (_observer == null || _recording is { Complete: false } || _videoTask != null)
            {
                MessageBox.Show(this, "Start Observe on the game window first, and stop any current recording.", "Gameplay Sampler");
                return;
            }
            using var setup = new GameplaySamplerForm(_recognizer, _research);
            if (setup.ShowDialog(this) != DialogResult.OK || setup.Plan == null) return;
            _record.PerformClick();
            if (_recording is not { Complete: false }) return;
            try
            {
                setup.Plan.Save(_recording.DirectoryPath);
                _samplePlan = setup.Plan;
                _record.Text = "Stop sample";
                _recordStatus.Text = $"Sampling {_samplePlan.Purpose} · Play manually, then stop and review. Goal: {_samplePlan.Goal}";
            }
            catch (Exception ex) { FinishRecording(); MessageBox.Show(this, ex.Message, "Could not start sampler"); }
        };
        teaching.Controls.Add(_recordStatus);
        _recordGameAudio.CheckedChanged += (_, _) => _recordStatus.Text = _recordGameAudio.Checked
            ? "Game audio is on for the next video: the selected game and its child processes only, including music. No microphone. Audio is saved separately with source timestamps for alignment."
            : "Game audio recording is off.";
        _recordGameInput.CheckedChanged += (_, _) =>
        {
            if (_recordGameInput.Checked)
                _recordStatus.Text = "Input recording is on for the next video: W/A/S/D, arrows, Space, Shift, Ctrl and mouse while the selected game is focused. These keys may also be recorded if typed in game chat.";
            else _recordStatus.Text = "Input recording is off. Video and stills can still be recorded.";
        };
        layout.Controls.Add(teaching);
        var recognition = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        recognition.Controls.Add(_learn);
        recognition.Controls.Add(_evaluate);
        var disable = new Button { Text = "Recognition off", AutoSize = true };
        recognition.Controls.Add(disable);
        recognition.Controls.Add(_recognitionStatus);
        recognition.Controls.Add(_dodgeAdvice);
        recognition.Controls.Add(_dodgeStatus);
        _dodgeAdvice.CheckedChanged += (_, _) => _dodgeStatus.Text = _dodgeAdvice.Checked
            ? "Dodge specialist enabled · waiting for a frame · advice only, no input."
            : "Dodge specialist off · no model or controls.";
        layout.Controls.Add(recognition);
        disable.Click += (_, _) => ClearRecognition();
        _windows.SelectedIndexChanged += (_, _) => ClearRecognition();
        _learn.Click += async (_, _) =>
        {
            using var picker = new OpenFileDialog { Title = "Learn from a saved teaching pack", Filter = "Game pack|pack.json", InitialDirectory = Path.Combine(_dataRoot, "game-packs") };
            var packPath = _pendingPack;
            _pendingPack = null;
            if (packPath == null)
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                packPath = picker.FileName;
            }
            ClearRecognition();
            var epoch = _recognitionEpoch;
            _learn.Enabled = false;
            _recognitionStatus.Text = "Learning labeled visual examples…";
            try
            {
                var model = await Task.Run(() => VisualStateRecognizer.Learn(packPath));
                if (_closing || IsDisposed || epoch != _recognitionEpoch) return;
                _recognizer = model;
                _evaluate.Enabled = true;
                _recognitionStatus.Text = $"Ready: {model.PackName} · {model.StateCount} states / {model.ExampleCount} images · Start Observe to recognize.";
            }
            catch (Exception ex) { if (!IsDisposed && epoch == _recognitionEpoch) _recognitionStatus.Text = "Could not learn: " + ex.Message; }
            finally { if (!IsDisposed) _learn.Enabled = true; }
        };
        _evaluate.Click += async (_, _) =>
        {
            var model = _recognizer;
            if (model == null) return;
            using var picker = new OpenFileDialog { Title = "Choose a separately recorded and labeled recording.json", Filter = "Labeled recording|recording.json", InitialDirectory = Path.Combine(_dataRoot, "recordings") };
            var recordingPath = _pendingCheck;
            _pendingCheck = null;
            if (recordingPath == null)
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                recordingPath = picker.FileName;
            }
            _evaluate.Enabled = false;
            try
            {
                var plan = GameplaySamplePlan.Load(Path.GetDirectoryName(recordingPath)!);
                if (plan?.Purpose == "check" && plan.PackHash != model.PackHash)
                    throw new InvalidOperationException("This sample checks a different pack version. Learn the original pack before checking its results.");
                var report = await Task.Run(() => model.Evaluate(TeachingRecording.Open(recordingPath)));
                var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var reportDocument = System.Text.Json.Nodes.JsonNode.Parse(json)!;
                reportDocument["sampler"] = System.Text.Json.JsonSerializer.SerializeToNode(plan);
                reportDocument["recording"] = Path.GetFullPath(recordingPath);
                json = reportDocument.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var directory = Path.Combine(_dataRoot, "reports");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "visual-check-" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(path, json);
                using var parsed = System.Text.Json.JsonDocument.Parse(json);
                var result = parsed.RootElement;
                if (!IsDisposed) MessageBox.Show(this, $"Tested: {result.GetProperty("tested")} · Correct: {result.GetProperty("correct")} · Unknown: {result.GetProperty("unknown")} · Incorrect: {result.GetProperty("incorrect")}\nExcluded training images: {result.GetProperty("excluded_training_duplicates")}\nUse a separate recording; neighboring frames can look nearly identical. This does not qualify gameplay.\n\nReport: {path}", "Visual recognition check");
            }
            catch (Exception ex) { if (!IsDisposed) MessageBox.Show(this, ex.Message, "Could not check recognition"); }
            finally { if (!IsDisposed) _evaluate.Enabled = _recognizer != null; }
        };
        _record.Click += (_, _) =>
        {
            try
            {
                if (_recording is { Complete: false }) FinishRecording();
                else if (_observer != null)
                {
                    _samplePlan = null;
                    _recording = TeachingRecording.Start(_dataRoot, _name.Text, _observer.Target.ProcessName);
                    _record.Text = "Stop stills";
                    _video.Enabled = false;
                    _importVideo.Enabled = false;
                    _review.Enabled = false;
                    _openRecording.Enabled = false;
                    _recordStatus.Text = "Recording selected game window · waiting for a frame…";
                }
            }
            catch (Exception ex) { _recordStatus.Text = "Recording error: " + ex.Message; }
        };
        _video.Click += async (_, _) =>
        {
            if (_videoTask != null) await StopVideoAsync();
            else await StartVideoAsync();
        };
        _importVideo.Click += async (_, _) => await ImportVideoAsync();
        _review.Click += (_, _) => { if (_recording is { Complete: true }) ReviewSample(_recording); };
        _openRecording.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Title = "Open recording.json", Filter = "Recording manifest|recording.json", InitialDirectory = Path.Combine(_dataRoot, "recordings") };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            try { ReviewSample(TeachingRecording.Open(picker.FileName)); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Cannot open recording"); }
        };
        layout.Controls.Add(_preview);
        layout.Controls.Add(_status);
        Controls.Add(layout);
        _refresh.Click += (_, _) => RefreshWindows();
        _attach.Click += async (_, _) => await AttachAsync();
        _pause.Click += async (_, _) => await StopAsync("Paused. Select Observe to reconnect to the selected window.");
        _timer.Tick += async (_, _) => await TickAsync();
        Shown += (_, _) =>
        {
            try
            {
                var saved = GameWorkspaceStore.Load(_dataRoot);
                if (saved != null && !_dodgeWorkspace) { _name.Text = saved.Name; _preferredProcess = saved.ProcessName; }
            }
            catch (Exception ex) { _status.Text = "Could not load saved workspace: " + ex.Message; }
            RefreshWindows();
        };
        FormClosing += async (_, e) =>
        {
            if (_closeReady) return;
            e.Cancel = true;
            if (_closing) return;
            _closing = true;
            _importStop?.Cancel();
            if (_importTask != null) { try { await _importTask; } catch { } }
            _dodgeExportStop?.Cancel();
            if (_dodgeExportTask != null) { try { await _dodgeExportTask; } catch { } }
            await StopAsync("Closing workspace…");
            _timer.Dispose();
            _closeReady = true;
            Close();
        };
    }

    internal void PreferWizard101()
    {
        if (_observer != null || _videoTask != null || _recording is { Complete: false } || _changing || _closing || _importTask != null || _dodgeExportTask != null || _dodgeRunTask != null)
        {
            _status.Text = "An Observe or review session is already open. Finish it before changing games.";
            return;
        }
        _dodgeWorkspace = false;
        _dodgeAdvice.Checked = false;
        _research = null; _samplePlan = null;
        foreach (var control in _wizardOnlyControls) control.Visible = true;
        _name.Text = "Wizard101"; _preferredProcess = "WizardGraphicalClient";
        RefreshWindows();
        _windows.SelectedItem = _windows.Items.Cast<GameWindow>().FirstOrDefault(HomeMovementGate.IsWizard101);
        _attach.Enabled = _windows.SelectedItem != null;
        _status.Text = _attach.Enabled ? "Wizard101 selected. Choose Observe to begin; no inputs are sent." : "Open Wizard101, then refresh and select its window.";
    }

    internal void PreferDodge(bool enableAdvice = false)
    {
        if (_observer != null || _videoTask != null || _recording is { Complete: false } || _changing || _closing || _importTask != null || _dodgeExportTask != null || _dodgeRunTask != null)
        {
            _status.Text = "An Observe or review session is already open. Finish it before changing games.";
            return;
        }
        _dodgeWorkspace = true;
        _research = null; _samplePlan = null;
        _name.Text = "Dodge the Creeps";
        _preferredProcess = null;
        foreach (var control in _wizardOnlyControls) control.Visible = false;
        _dodgeAdvice.Checked = enableAdvice;
        RefreshWindows();
        _status.Text = _attach.Enabled ? "Dodge selected. Choose Observe to begin. Advice never sends game input." :
            "Open the local Dodge the Creeps game, then refresh. Only matching Dodge windows are listed here.";
    }

    private void RefreshWindows()
    {
        if (_observer != null || _changing) return;
        var previous = _windows.SelectedItem as GameWindow;
        _windows.Items.Clear();
        foreach (var window in GameWindows.List().Where(window => !_dodgeWorkspace || GameWorkspaceCatalog.IsDodgeWindow(window))) _windows.Items.Add(window);
        var preferred = _windows.Items.Cast<GameWindow>().FirstOrDefault(w => previous != null && GameWindows.SameIdentity(previous, w))
            ?? _windows.Items.Cast<GameWindow>().FirstOrDefault(w => w.ProcessName == _preferredProcess);
        _windows.SelectedItem = preferred;
        if (_windows.SelectedIndex < 0 && _windows.Items.Count > 0) _windows.SelectedIndex = 0;
        _attach.Enabled = _windows.Items.Count > 0;
        if (_windows.Items.Count == 0) _status.Text = "No available windows. Open your game, then refresh this list.";
    }

    private async Task AttachAsync()
    {
        if (_changing || _closing || _windows.SelectedItem is not GameWindow selected) return;
        _changing = true;
        SetControls(false);
        try
        {
            await ReleaseAsync();
            _observer = new WindowObserver(selected);
            try { GameWorkspaceStore.Save(_dataRoot, new(_name.Text.Trim(), selected.ProcessName)); }
            catch { await ReleaseAsync(); throw; }
            _lastFrame = DateTime.UtcNow;
            _hasEverFrame = false;
            _status.Text = $"Connecting to {selected.Title} · {selected.ProcessName} · PID {selected.ProcessId}…";
            _timer.Start();
            _pause.Enabled = true;
            _record.Enabled = true;
            _video.Enabled = true;
        }
        catch (Exception ex) { _status.Text = "Unable to observe: " + ex.Message + " Try windowed or borderless mode."; SetControls(true); }
        finally { _changing = false; }
    }

    private async Task TickAsync()
    {
        if (_busy || _changing || _closing || _observer == null) return;
        _busy = true;
        var observer = _observer;
        try
        {
            var bitmap = await observer.ReadAsync();
            if (observer != _observer || _closing) { bitmap?.Dispose(); return; }
            if (bitmap == null)
            {
                if (DateTime.UtcNow - _lastFrame > TimeSpan.FromSeconds(5))
                {
                    _status.Text = _hasEverFrame
                        ? $"Waiting for a new frame from {observer.Target.Title} · last update {_lastFrame.ToLocalTime():HH:mm:ss}. The game may be static, paused, or obscured."
                        : $"Waiting for the first frame from {observer.Target.Title}. Make sure the game is visible and rendering.";
                    if (_recognizer != null) _recognitionStatus.Text = "No recent frame · the last recognition may be stale.";
                    if (_dodgeAdvice.Checked) _dodgeStatus.Text = "Dodge specialist · no recent frame; previous advice is stale.";
                }
                return;
            }
            _lastFrame = DateTime.UtcNow;
            _hasEverFrame = true;
            if (_videoTask != null && !_videoNotified && Volatile.Read(ref _videoHasFrame) != 0)
            {
                _videoNotified = true;
                _recordStatus.Text = $"Recording video{(_recordGameInput.Checked ? " and selected game inputs while focused" : "")}" +
                    $" · {(_recordGameAudio.Checked ? "selected game audio on" : "audio off")} · up to 3 minutes · timing is experimental";
            }
            if (_recording is { Complete: false } recording)
            {
                using var copy = new Bitmap(bitmap);
                try
                {
                    var active = await Task.Run(() => recording.AddFrame(copy));
                    _recordStatus.Text = $"{(active ? "Recording" : "Recording limit reached")} · {recording.Frames.Count} frames saved" + (_samplePlan == null ? "" : $" · {_samplePlan.Purpose}: {_samplePlan.Goal}");
                    if (!active)
                    {
                        FinishRecording();
                        _recordStatus.Text = $"Recording stopped: reached the 600-frame or 1 GiB limit · {recording.Frames.Count} frames saved. Review them or start another sample.";
                        MessageBox.Show(this, _recordStatus.Text, "Recording limit reached", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex) { FinishRecording(); _recordStatus.Text = "Recording stopped: " + ex.Message; }
                if (observer != _observer || _closing) { bitmap.Dispose(); return; }
            }
            if (_recognizer != null)
            {
                var prediction = _recognizer.Predict(bitmap);
                _recognitionStatus.Text = _samplePlan?.Purpose == "check"
                    ? "Gameplay Sampler · Predictions hidden while collecting and labeling an independent check."
                    : $"{_recognizer.PackName}\nState: {prediction.State ?? "Unknown"} · Similarity {prediction.Similarity:P0} (not probability) · Gap {prediction.Margin:P1} · {_lastFrame.ToLocalTime():HH:mm:ss}\n{prediction.Reason}";
            }
            if (_dodgeAdvice.Checked)
            {
                var advice = DodgeSpecialist.Analyze(bitmap);
                _dodgeStatus.Text = $"Dodge: {advice.Advice} · {advice.Detail} No inputs sent.";
            }
            var old = _preview.Image;
            _preview.Image = bitmap;
            old?.Dispose();
            _status.Text = $"Observing {observer.Target.Title} · PID {observer.Target.ProcessId} · {bitmap.Width} × {bitmap.Height} · Updated {_lastFrame.ToLocalTime():HH:mm:ss}\nObserve only · No game inputs · Manual labels teach examples; OCR and play are not enabled.";
        }
        catch (Exception ex) { if (observer == _observer) await StopAsync(ex.Message); }
        finally { _busy = false; }
    }

    internal async Task PreparePetDancePlayerAsync()
    {
        if (_changing || _closing || _videoTask != null || _recording is { Complete: false } ||
            _importTask != null || _dodgeExportTask != null || _dodgeRunTask != null)
            throw new InvalidOperationException("Finish or stop the current Observe recording or test before opening the Pet Dance player.");
        if (_observer != null) await StopAsync("Observe is paused while you set up the Pet Dance player.");
    }

    private async Task StopAsync(string message)
    {
        _observationGeneration++;
        _timer.Stop();
        await ReleaseAsync();
        _status.Text = message;
        SetControls(!_closing);
    }

    private async Task ReleaseAsync()
    {
        _dodgeRunStop?.Cancel();
        if (_dodgeRunTask != null) { try { await _dodgeRunTask; } catch { } }
        if (_dodgeAdvice.Checked) _dodgeStatus.Text = "Dodge specialist · no live advice; capture paused or disconnected.";
        if (_recognizer != null) _recognitionStatus.Text = $"{_recognizer.PackName} · No live prediction; capture paused/disconnected.";
        await StopVideoAsync();
        if (_recording is { Complete: false }) FinishRecording();
        _record.Enabled = false;
        _video.Enabled = false;
        var observer = _observer;
        _observer = null;
        var image = _preview.Image;
        _preview.Image = null;
        image?.Dispose();
        if (observer != null) await observer.DisposeAsync();
    }

    private async Task StartVideoAsync()
    {
        var observer = _observer;
        if (observer == null || _videoTask != null || _recording is { Complete: false }) return;
        var ffmpeg = VideoTools.FindFfmpeg(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH"));
        if (ffmpeg == null)
        {
            _recordStatus.Text = "Video preview needs FFmpeg and ffprobe together. Reinstall Maelstrom or make both available on PATH.";
            return;
        }
        var directory = Path.Combine(_dataRoot, "video-recordings", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
        var target = observer.Target;
        var pausePreview = _pausePreviewForVideo.Checked;
        var includeInput = _recordGameInput.Checked;
        var includeAudio = _recordGameAudio.Checked;
        GameplayInputRecorder? inputRecorder = null;
        var inputSaved = false;
        var generation = _observationGeneration;
        _lastVideoDirectory = directory;
        var cancellation = new CancellationTokenSource();
        _videoStop = cancellation;
        _videoHasFrame = 0;
        _videoNotified = false;
        _video.Text = "Stop video";
        _record.Enabled = false;
        _importVideo.Enabled = false;
        _pausePreviewForVideo.Enabled = false;
        _recordGameInput.Enabled = false;
        _recordGameAudio.Enabled = false;
        _video.Enabled = false;
        _pause.Enabled = false;
        _recordStatus.Text = "Starting video preview · waiting for the first frame…";
        Task? task = null;
        try
        {
            if (includeInput) inputRecorder = GameplayInputRecorder.Install(target);
            if (pausePreview)
            {
                _timer.Stop();
                _observer = null;
                _status.Text = "Live preview paused · preparing to record the selected window.";
                if (_recognizer != null) _recognitionStatus.Text = "Live recognition paused while recording video.";
                if (_dodgeAdvice.Checked) _dodgeStatus.Text = "Dodge specialist paused while the live preview is paused.";
                await observer.DisposeAsync();
            }
            if (_closing || generation != _observationGeneration) return;
            _pause.Enabled = true;
            task = Task.Run(() => GpuVideoRecording.RunAsync(target, directory, TimeSpan.FromMinutes(3), cancellation.Token, () =>
            {
                Interlocked.Exchange(ref _videoHasFrame, 1);
                if (pausePreview && IsHandleCreated && !IsDisposed)
                {
                    try { BeginInvoke(() => { if (!IsDisposed && _videoTask != null) { _recordStatus.Text = $"Recording video{(includeInput ? " and selected game inputs while focused" : "")} · {(includeAudio ? "selected game audio on" : "audio off")} · live preview paused."; _status.Text = "Live preview paused while the selected window is recorded."; } }); }
                    catch (InvalidOperationException) { }
                }
            }, inputRecorder == null ? null : inputRecorder.Start, inputRecorder == null ? null : inputRecorder.Stop,
                allowRateConvertedOutput: true, includeGameAudio: includeAudio));
            _videoTask = task;
            _video.Enabled = true;
            await task;
            if (inputRecorder != null) { inputRecorder.Stop(FrameTimeline.TimestampMs()); inputRecorder.Save(directory, videoVerified: true); inputSaved = true; }
            using var report = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "timing.json")));
            var root = report.RootElement;
            var fps = root.GetProperty("capture_timing").GetProperty("DeliveredFps");
            var gaps = root.GetProperty("capture_timing");
            var timing = root.GetProperty("playback_timing_error");
            var measuredFps = fps.ValueKind == System.Text.Json.JsonValueKind.Number ? $"{fps.GetDouble():0.0} fps" : "fps unavailable: too few new frames (the game may be static)";
            var drift = timing.ValueKind == System.Text.Json.JsonValueKind.Object ? $"{timing.GetProperty("P95Ms").GetDouble():0} ms timing error (95th percentile)" : "timing error unavailable";
            var p95Gap = gaps.GetProperty("GapP95Ms");
            var maxGap = gaps.GetProperty("MaximumGapMs");
            var gapReport = p95Gap.ValueKind == System.Text.Json.JsonValueKind.Number && maxGap.ValueKind == System.Text.Json.JsonValueKind.Number
                ? $"\nDelivered-frame gaps: 95% at or below {p95Gap.GetDouble():0.0} ms; longest {maxGap.GetDouble():0.0} ms (unchanged screens may be quiet)." : "";
            var uncovered = root.TryGetProperty("recording_coverage", out var coverage) && coverage.ValueKind == System.Text.Json.JsonValueKind.Object
                ? coverage.GetProperty("UnrepresentedTailMs").GetDouble() : 0;
            var coverageWarning = uncovered > 250 ? $" Last {uncovered / 1000:0.0}s not represented in the video; the window may have stopped producing frames." : "";
            var audioSummary = "";
            if (includeAudio)
            {
                using var audioReport = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "audio-timing.json")));
                audioSummary = audioReport.RootElement.GetProperty("status").GetString() == "complete"
                    ? " Game audio saved separately as game-audio.wav with source timestamps for alignment."
                    : " Game audio is incomplete; see audio-timing.json before using it for training.";
            }
            if (!IsDisposed) _recordStatus.Text = $"Video preview saved: {Path.Combine(directory, "capture.mp4")}\n{measuredFps} · {root.GetProperty("queue_rejected").GetInt64()} queue drops · {drift}.{gapReport}{coverageWarning} {(inputSaved ? "Game input observations saved alongside the video." : "")}{audioSummary} Use Sample video for labels to review selected frames.";
        }
        catch (Exception ex)
        {
            if (!IsDisposed) _recordStatus.Text = "Video preview failed: " + ex.Message + " · Any recovery files are in " + directory;
        }
        finally
        {
            if (inputRecorder != null)
            {
                inputRecorder.Stop(FrameTimeline.TimestampMs());
                inputRecorder.Dispose();
                if (!inputSaved && Directory.Exists(directory))
                {
                    try { inputRecorder.Save(directory, videoVerified: false); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { if (!IsDisposed) _recordStatus.Text += " · Could not save input observations: " + ex.Message; }
                }
            }
            if (task == null || _videoTask == task)
            {
                _videoTask = null;
                _videoStop = null;
                cancellation.Dispose();
                if (!IsDisposed)
                {
                    if (pausePreview && !_closing && generation == _observationGeneration)
                    {
                        try
                        {
                            _observer = new WindowObserver(target);
                            _lastFrame = DateTime.UtcNow;
                            _hasEverFrame = false;
                            _timer.Start();
                            _status.Text = $"Reconnecting preview to {target.Title}…";
                        }
                        catch (Exception ex) { _status.Text = "Could not reconnect preview: " + ex.Message; SetControls(true); }
                    }
                    _video.Text = "Record video (preview)";
                    _video.Enabled = _observer != null && !_closing;
                    _record.Enabled = _observer != null && !_closing;
                    _importVideo.Enabled = !_closing;
                    _pause.Enabled = _observer != null && !_closing;
                    _pausePreviewForVideo.Enabled = !_closing;
                    _recordGameInput.Enabled = !_closing;
                    _recordGameAudio.Enabled = !_closing;
                }
            }
        }
    }

    private async Task StopVideoAsync()
    {
        var task = _videoTask;
        if (task == null) return;
        _video.Enabled = false;
        _recordStatus.Text = "Finishing video preview…";
        _videoStop?.Cancel();
        try { await task; } catch { /* StartVideoAsync reports the failure. */ }
    }

    private async Task ImportVideoAsync()
    {
        if (_importTask != null || _videoTask != null || _recording is { Complete: false }) return;
        var ffmpeg = VideoTools.FindFfmpeg(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH"));
        if (ffmpeg == null) { _recordStatus.Text = "Video sampling needs FFmpeg and ffprobe together. Reinstall Maelstrom or make both available on PATH."; return; }
        using var picker = new OpenFileDialog { Title = "Choose a local video to sample for labels", Filter = "Videos|*.mp4;*.mkv;*.webm;*.mov;*.avi|All files|*.*" };
        if (_lastVideoDirectory != null && Directory.Exists(_lastVideoDirectory)) picker.InitialDirectory = _lastVideoDirectory;
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        var workspaceName = _name.Text;
        var videoPath = picker.FileName;
        var rate = (int)_sampleRate.Value;
        var start = (int)_sampleStart.Value;
        var cancellation = new CancellationTokenSource();
        _importStop = cancellation;
        _video.Enabled = _record.Enabled = _importVideo.Enabled = false;
        _recordStatus.Text = $"Sampling video from {start}s · {rate} frames per second, up to {VideoTeachingImport.MaximumSeconds(rate)} seconds · actions may fall between samples.";
        var task = Task.Run(() => VideoTeachingImport.RunAsync(_dataRoot, workspaceName, videoPath, ffmpeg, cancellation.Token, rate, start));
        _importTask = task;
        try
        {
            var recording = await task;
            if (_closing || IsDisposed) return;
            _recording = recording;
            _review.Enabled = true;
            _recordStatus.Text = $"Sampled {recording.Frames.Count} frames from {start}s · Review and label when ready. Times are approximate; videos can contain held frames.";
        }
        catch (OperationCanceledException) { if (!IsDisposed) _recordStatus.Text = "Video sampling stopped; any saved samples remain on disk."; }
        catch (Exception ex) { if (!IsDisposed) _recordStatus.Text = "Video sampling failed: " + ex.Message; }
        finally
        {
            if (_importTask == task) { _importTask = null; _importStop = null; cancellation.Dispose(); }
            if (!IsDisposed && !_closing)
            {
                _importVideo.Enabled = true;
                _video.Enabled = _observer != null && _videoTask == null;
                _record.Enabled = _observer != null && _videoTask == null;
            }
        }
    }

    internal async Task<string> VerifyVideoWorkflowAsync(GameWindow target, int recordingMilliseconds = 2500, bool prioritizeVideo = false, Action? onRecordingReady = null, bool recordInput = false)
    {
        _windows.Items.Add(target);
        _windows.SelectedItem = target;
        await AttachAsync();
        if (_observer == null || !_video.Enabled) throw new InvalidOperationException("Observe did not attach to the test window.");
        _pausePreviewForVideo.Checked = prioritizeVideo;
        _recordGameInput.Checked = recordInput;
        _video.PerformClick();
        for (var i = 0; i < 200 && Volatile.Read(ref _videoHasFrame) == 0 && !_recordStatus.Text.StartsWith("Video preview failed:", StringComparison.Ordinal); i++) await Task.Delay(100);
        if (Volatile.Read(ref _videoHasFrame) == 0) throw new InvalidOperationException("Video recording did not receive its first frame: " + _recordStatus.Text);
        onRecordingReady?.Invoke();
        await Task.Delay(recordingMilliseconds);
        if (_videoTask == null || !_video.Enabled) throw new InvalidOperationException("Video recording did not start.");
        _video.PerformClick();
        for (var i = 0; i < 600 && _videoTask != null; i++) await Task.Delay(100);
        if (_videoTask != null) throw new TimeoutException("Video recording did not finish.");
        if (!_recordStatus.Text.StartsWith("Video preview saved:", StringComparison.Ordinal)) throw new InvalidOperationException(_recordStatus.Text);
        if (prioritizeVideo && _observer == null) throw new InvalidOperationException("Observe preview did not reconnect after recording.");
        var directory = _lastVideoDirectory ?? throw new InvalidOperationException("No video folder was produced.");
        await StopAsync("Verification finished.");
        return directory;
    }

    internal async Task VerifyPrioritizedPauseAsync(GameWindow target)
    {
        _windows.Items.Add(target);
        _windows.SelectedItem = target;
        await AttachAsync();
        if (_observer == null) throw new InvalidOperationException("Observe did not attach to the test window.");
        _pausePreviewForVideo.Checked = true;
        _video.PerformClick();
        for (var i = 0; i < 200 && Volatile.Read(ref _videoHasFrame) == 0; i++) await Task.Delay(100);
        if (Volatile.Read(ref _videoHasFrame) == 0) throw new InvalidOperationException("Prioritized recording did not start.");
        await StopAsync("Verification paused.");
        for (var i = 0; i < 100 && _videoTask != null; i++) await Task.Delay(100);
        if (_observer != null || _videoTask != null || _timer.Enabled)
            throw new InvalidOperationException("Pausing Observe unexpectedly resumed the preview or recording.");
    }

    internal async Task VerifyStaticTargetAsync(GameWindow target)
    {
        _windows.Items.Add(target);
        _windows.SelectedItem = target;
        await AttachAsync();
        if (_observer == null) throw new InvalidOperationException("Observe did not attach to the static target.");
        for (var i = 0; i < 50 && _preview.Image == null; i++) await Task.Delay(100);
        if (_preview.Image == null) throw new InvalidOperationException("Static-target check was inconclusive: no initial frame arrived.");
        await Task.Delay(TimeSpan.FromSeconds(8));
        if (_observer == null) throw new InvalidOperationException("Observe disconnected from a still-open static target.");
        if (!_status.Text.StartsWith("Waiting for a new frame", StringComparison.Ordinal))
            throw new InvalidOperationException("Static-target check was inconclusive: the target continued delivering frames.");
        await StopAsync("Static-target check finished.");
    }

    internal bool ReportsMissingVideoTail => _recordStatus.Text.Contains("not represented in the video", StringComparison.Ordinal);
    internal bool HasVideoFrame => Volatile.Read(ref _videoHasFrame) != 0;

    private void ClearRecognition()
    {
        _recognitionEpoch++;
        _recognizer = null;
        _evaluate.Enabled = false;
        _recognitionStatus.Text = "Visual recognition off · Select a teaching pack for this game window.";
    }

    private void ReviewSample(TeachingRecording recording)
    {
        var plan = GameplaySamplePlan.Load(recording.DirectoryPath);
        if (plan?.Purpose == "check")
        {
            _samplePlan = plan;
            _recognitionStatus.Text = "Gameplay Sampler · Predictions hidden while labeling an independent check.";
        }
        using var review = new TeachingReviewForm(recording, _dataRoot, _openWorkshop,
            path =>
            {
                if (!_learn.Enabled) { MessageBox.Show(this, "Wait for the current learning operation to finish."); return; }
                BeginInvoke(new Action(() => { _pendingPack = path; _learn.PerformClick(); }));
            },
            sample =>
            {
                if (!_evaluate.Enabled) { MessageBox.Show(this, "Learn the pack used for this sample first, or wait for the current check to finish."); return; }
                BeginInvoke(new Action(() => { _pendingCheck = Path.Combine(sample.DirectoryPath, "recording.json"); _evaluate.PerformClick(); }));
            });
        try { review.ShowDialog(this); }
        finally { if (plan?.Purpose == "check") _samplePlan = null; }
    }

    private void FinishRecording()
    {
        if (_recording == null) return;
        _openRecording.Enabled = true;
        try { _recording.Finish(); }
        catch (Exception ex) { _recordStatus.Text = "Could not finalize recording: " + ex.Message + ". Previously saved frames remain on disk."; _record.Text = "Record stills"; _review.Enabled = _recording.Frames.Count > 0; return; }
        _record.Text = "Record stills";
        _video.Enabled = _observer != null && !_closing && _videoTask == null;
        _importVideo.Enabled = !_closing && _videoTask == null && _importTask == null;
        _review.Enabled = _recording.Frames.Count > 0;
        if (_review.Enabled) _recordStatus.Text = $"Recording stopped · {_recording.Frames.Count} frames · Ready to review and label";
    }

    private void SetControls(bool enabled)
    {
        _windows.Enabled = _refresh.Enabled = _name.Enabled = enabled;
        _attach.Enabled = enabled && _windows.Items.Count > 0;
        _pause.Enabled = false;
    }
}
