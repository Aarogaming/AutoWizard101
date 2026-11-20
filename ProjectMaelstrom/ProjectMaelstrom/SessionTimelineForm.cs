using System.Diagnostics;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Model-free recording and manual activity authoring. Replay uses the existing
/// evidence runner; annotations alone never count as successful game actions.</summary>
internal sealed class SessionTimelineForm : Form
{
    private readonly GameWindow? _target;
    private readonly string _root;
    private TeachingSession? _session;
    private string? _path;
    private CancellationTokenSource? _stop;
    private readonly NumericUpDown _minutes = new() { Minimum = 1, Maximum = 60, Value = 30, Width = 65 };
    private readonly CheckBox _inputs = new() { Text = "Record movement keys / mouse (focused game only)", AutoSize = true };
    private readonly CheckBox _audio = new() { Text = "Game audio", AutoSize = true };
    private readonly Button _record = new() { Text = "Start session", AutoSize = true };
    private readonly Label _status = new() { Text = "Record, mark activity ranges, then edit a card and test its evidence.", AutoSize = true, MaximumSize = new Size(950, 0) };
    private readonly ListBox _chunks = new() { Dock = DockStyle.Fill };
    private readonly ListBox _activities = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _start = new() { Maximum = 7200, DecimalPlaces = 2, Width = 90 };
    private readonly NumericUpDown _end = new() { Maximum = 7200, DecimalPlaces = 2, Width = 90 };
    private readonly TextBox _label = new() { Text = "New activity", Width = 170, MaxLength = 200 };
    private readonly TextBox _game = new() { Text = "my-game", Width = 100 };
    private readonly TextBox _trigger = new() { Text = "ready", Width = 100 };
    private readonly TextBox _action = new() { Text = "interact", Width = 100 };
    private readonly TextBox _outcome = new() { Text = "completed", Width = 100 };
    private readonly TextBox _commentary = new() { Multiline = true, Width = 700, Height = 55, MaxLength = 4000, ScrollBars = ScrollBars.Vertical, AccessibleName = "Commentary on the selected activity; human interpretation, not verified evidence" };
    private readonly TrackBar _cursor = new() { Minimum = 0, Maximum = 1, TickStyle = TickStyle.None, Dock = DockStyle.Top, AccessibleName = "Session timeline in tenths of a second" };
    private readonly Label _position = new() { Text = "Timeline: 0.0 seconds", Dock = DockStyle.Top, AutoSize = true };
    private readonly TextBox _state = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
        Dock = DockStyle.Right, Width = 360, AccessibleName = "State readings at timeline cursor, with source and age" };
    private TeachingStateSample[] _samples = [];
    private string? _telemetryWorkspace;
    private GameplayInputProfile _inputProfile = GameplayInputProfile.Movement;
    internal string? LastSessionPath => _path;
    internal string? RecordingFailure { get; private set; }

    internal async Task RecordHeadlessAsync(int minutes, bool inputs, string? telemetryWorkspace, string stopFile, GameplayInputProfile inputProfile = GameplayInputProfile.Movement)
    {
        _minutes.Value = minutes; _inputs.Checked = inputs; _telemetryWorkspace = telemetryWorkspace;
        _inputProfile = inputProfile;
        using var timer = new System.Windows.Forms.Timer { Interval = 250 };
        timer.Tick += (_, _) => { if (File.Exists(stopFile)) _stop?.Cancel(); };
        timer.Start();
        await RecordAsync();
    }

    public SessionTimelineForm(string root, GameWindow? target)
    {
        _root = root; _target = target;
        Text = "Maelstrom · Session timeline and cards";
        Size = new Size(1050, 700); MinimumSize = new Size(900, 600);
        BackColor = Color.FromArgb(30, 32, 37); ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(15), RowCount = 6, ColumnCount = 1 };
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.Percent, 50)); layout.RowStyles.Add(new(SizeType.Percent, 50));
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.AutoSize));
        var top = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        top.Controls.AddRange([_record, new Label { Text = "minutes", AutoSize = true }, _minutes, _inputs, _audio]);
        var open = new Button { Text = "Open session…", AutoSize = true };
        top.Controls.Add(open); layout.Controls.Add(top); layout.Controls.Add(_status);
        var timeline = new Panel { Dock = DockStyle.Fill };
        timeline.Controls.Add(_chunks); timeline.Controls.Add(_state); timeline.Controls.Add(_position); timeline.Controls.Add(_cursor);
        layout.Controls.Add(timeline); layout.Controls.Add(_activities);
        var edit = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        foreach (var pair in new (string, Control)[] { ("Activity", _label), ("Start seconds", _start), ("End seconds", _end),
            ("Game ID", _game), ("When scene", _trigger), ("Action ID", _action), ("Expected scene", _outcome) })
        { edit.Controls.Add(new Label { Text = pair.Item1, AutoSize = true }); edit.Controls.Add(pair.Item2); }
        edit.Controls.Add(new Label { Text = "Commentary", AutoSize = true }); edit.Controls.Add(_commentary);
        layout.Controls.Add(edit);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        Add(buttons, "Add activity", () => SaveActivity(false));
        Add(buttons, "Mark start", () => _start.Value = _cursor.Value / 10m);
        Add(buttons, "Mark end", () => _end.Value = _cursor.Value / 10m);
        Add(buttons, "Update selected", () => SaveActivity(true));
        Add(buttons, "Open selected video", OpenVideo);
        Add(buttons, "Export routine card…", Export);
        Add(buttons, "Replay against evidence…", Replay);
        Add(buttons, "Attach state track…", () => AttachState(false));
        Add(buttons, "Import W101 observer…", () => AttachState(true));
        Add(buttons, "Review card proposals", ReviewProposals);
        layout.Controls.Add(buttons); Controls.Add(layout);
        _record.Enabled = target != null;
        _record.Click += async (_, _) => { if (_stop != null) _stop.Cancel(); else await RecordAsync(); };
        open.Click += (_, _) => Guard(() =>
        {
            if (_stop != null) throw new InvalidOperationException("Stop recording before opening another session.");
            using var picker = new OpenFileDialog { Filter = "Teaching session|teaching-session.json" };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            var session = LongTeachingSession.Read(picker.FileName);
            _session = session; _path = picker.FileName; _game.Text = session.Game; LoadState(); RefreshTimeline();
            _status.Text = $"Session status: {session.Status}. Completed chunks survive an interrupted recording. Gaps are not gameplay evidence.";
        });
        _cursor.ValueChanged += (_, _) =>
        {
            var time = _cursor.Value * 100.0;
            var chunk = _session?.Chunks.FirstOrDefault(c => c.StartMs <= time && c.EndMs >= time);
            _position.Text = $"Timeline: {time / 1000:0.0}s · " + (chunk == null ? "capture gap / no evidence" : chunk.Directory + " · " + chunk.Status);
            ShowState(time);
        };
        _chunks.SelectedIndexChanged += (_, _) =>
        {
            if (_session == null || _chunks.SelectedIndex < 0) return;
            var c = _session.Chunks[_chunks.SelectedIndex];
            _start.Value = (decimal)(c.StartMs / 1000); _end.Value = (decimal)(c.EndMs / 1000);
        };
        _activities.SelectedIndexChanged += (_, _) =>
        {
            if (_session == null || _activities.SelectedIndex < 0) return;
            var a = _session.Activities[_activities.SelectedIndex];
            _label.Text = a.Label; _start.Value = (decimal)(a.StartMs / 1000); _end.Value = (decimal)(a.EndMs / 1000);
            _trigger.Text = a.Trigger; _action.Text = a.Action; _outcome.Text = a.Outcome;
            _commentary.Text = a.Commentary;
        };
        FormClosing += (_, e) => { if (_stop != null) { e.Cancel = true; _stop.Cancel(); _status.Text = "Finishing the current chunk. Close again when recording has stopped."; } };
    }

    private void Add(Control parent, string text, Action action)
    { var button = new Button { Text = text, AutoSize = true }; button.Click += (_, _) => Guard(action); parent.Controls.Add(button); }
    private void Guard(Action action) { try { action(); } catch (Exception ex) { _status.Text = ex.Message; } }

    private async Task RecordAsync()
    {
        if (_target == null) return;
        var folder = Path.Combine(_root, "teaching-sessions", Guid.NewGuid().ToString("N"));
        var includeInput = _inputs.Checked; var includeAudio = _audio.Checked;
        using var stop = new CancellationTokenSource(); _stop = stop;
        using var telemetryStop = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        Task<string[]>? telemetry = null;
        RecordingFailure = null;
        _record.Text = "Stop and save"; _minutes.Enabled = _inputs.Enabled = _audio.Enabled = false;
        try
        {
            Directory.CreateDirectory(folder); _path = Path.Combine(folder, "teaching-session.json");
            _samples = []; _state.Clear();
            _session = new(1, Guid.NewGuid().ToString("N"), _game.Text, FrameTimeline.TimestampMs(), "recording", [], [],
                new(_target.ProcessId, _target.StartedUtcTicks, (long)_target.Handle, _target.ProcessName), DateTime.UtcNow.Ticks);
            LongTeachingSession.Save(_path, _session);
            if (_telemetryWorkspace != null)
                telemetry = Task.Run(async () =>
                {
                    try { return await TeachingTelemetryLoop.CollectW101Async(_telemetryWorkspace, _root, _target, telemetryStop.Token); }
                    catch { stop.Cancel(); throw; }
                });
            foreach (var duration in LongTeachingSession.Plan((int)_minutes.Value))
            {
                if (stop.IsCancellationRequested) break;
                if (!GameWindows.IsCurrent(_target)) throw new InvalidOperationException("Selected game closed or changed. Previous chunks remain saved.");
                if (new DriveInfo(Path.GetPathRoot(folder)!).AvailableFreeSpace < 2L * 1024 * 1024 * 1024)
                    throw new IOException("Less than 2 GB free. Recording stopped to protect saved work.");
                if (Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length) >= 20L * 1024 * 1024 * 1024)
                    throw new IOException("Session reached the 20 GB checkpoint limit. Start a new session after reviewing disk space.");
                var name = $"chunk-{_session.Chunks.Length + 1:000}";
                var directory = Path.Combine(folder, name);
                var start = FrameTimeline.TimestampMs() - _session.ClockOriginMs;
                var chunk = new TeachingChunk(name, start, start, "recording", null);
                _session = _session with { Chunks = [.. _session.Chunks, chunk] };
                LongTeachingSession.Save(_path, _session);
                using var inputs = includeInput ? GameplayInputRecorder.Install(_target, _inputProfile) : null;
                double? origin = null; var verified = false;
                try
                {
                    _status.Text = $"Recording {name} · selected window only · two-minute recoverable chunks. No game controls are sent.";
                    await Task.Run(() => GpuVideoRecording.RunAsync(_target, directory, duration, stop.Token,
                        () => GameExperiments.WriteAtomic(Path.Combine(folder, "recording-ready.json"), new
                        { target = new TeachingTarget(_target.ProcessId, _target.StartedUtcTicks, _target.Handle.ToInt64(), _target.ProcessName), chunk = name, sends_input = false }),
                        (capture, callback) => { origin = capture; inputs?.Start(capture, callback); },
                        end => inputs?.Stop(end), allowRateConvertedOutput: true, includeGameAudio: includeAudio));
                    verified = true;
                }
                finally
                {
                    inputs?.Stop(FrameTimeline.TimestampMs());
                    var updated = chunk with { EndMs = FrameTimeline.TimestampMs() - _session.ClockOriginMs,
                        Status = verified ? "complete" : "incomplete", VideoOriginMs = origin };
                    _session = _session with { Chunks = [.. _session.Chunks[..^1], updated] };
                    LongTeachingSession.Save(_path, _session);
                    if (Directory.Exists(directory)) inputs?.Save(directory, verified);
                    RefreshTimeline();
                }
            }
            _session = _session with { Status = stop.IsCancellationRequested ? "stopped" : "complete" };
            _status.Text = "Session saved. Select a chunk to open its video; mark activity ranges in seconds and edit their cards below.";
        }
        catch (Exception ex)
        {
            RecordingFailure = ex.Message;
            if (_session != null) _session = _session with { Status = "interrupted" };
            _status.Text = "Recording stopped: " + ex.Message + " · Saved chunks remain in " + folder;
        }
        finally
        {
            telemetryStop.Cancel();
            if (telemetry != null)
            {
                try
                {
                    var batches = await telemetry;
                    if (_session != null && _path != null)
                    {
                        GameExperiments.WriteAtomic(Path.Combine(folder, "telemetry-batches.json"), new { requests = batches, actor = "observed-user", sends_input = false });
                        var track = TeachingTelemetryLoop.MergeW101(batches, _path, _session);
                        if (track != null) _session = _session with { StateTrack = Path.GetFileName(track) };
                    }
                }
                catch (Exception ex)
                {
                    RecordingFailure = "Telemetry: " + ex.Message;
                    if (_session != null) _session = _session with { Status = "interrupted" };
                    _status.Text = RecordingFailure + " · Video and raw observation receipts remain available.";
                }
            }
            if (_session != null && _path != null) Guard(() => LongTeachingSession.Save(_path, _session));
            _stop = null; _record.Text = "Start session"; _minutes.Enabled = _inputs.Enabled = _audio.Enabled = true;
        }
    }

    private void RefreshTimeline()
    {
        _chunks.Items.Clear(); _activities.Items.Clear();
        if (_session == null) return;
        _cursor.Maximum = Math.Max(1, (int)Math.Ceiling((_session.Chunks.LastOrDefault()?.EndMs ?? 0) / 100));
        foreach (var c in _session.Chunks) _chunks.Items.Add($"{c.StartMs / 1000:0.00}–{c.EndMs / 1000:0.00}s  {c.Directory}  [{c.Status}]");
        foreach (var a in _session.Activities) _activities.Items.Add($"{a.StartMs / 1000:0.00}–{a.EndMs / 1000:0.00}s  {a.Label}  · {a.Trigger} → {a.Action} → {a.Outcome}");
    }

    private void LoadState()
    {
        _samples = [];
        if (_session != null && _path != null && TeachingReviewCli.TrackPath(_path, _session) is { } track)
        {
            _ = TeachingReviewCli.Review(_path); // Verify content hash before presenting attached evidence.
            _samples = TeachingStateTrack.Read(track, _session);
        }
        ShowState(_cursor.Value * 100.0);
    }
    private void ShowState(double time)
    {
        var view = TeachingStateTrack.At(_samples, time);
        _state.Text = $"{view.Status} · age {view.AgeMs?.ToString("0.0") ?? "unknown"} ms\r\n" +
            (view.Sample == null ? "No preceding reading. Future samples are never substituted." :
            JsonSerializer.Serialize(view.Sample, LongTeachingSession.Json));
    }
    private void AttachState(bool w101)
    {
        if (_path == null || _stop != null) throw new InvalidOperationException("Open a stopped/saved session first.");
        using var picker = new OpenFileDialog { Title = w101 ? "Select the matching observer ui-request.json" : "Select a game-neutral state JSONL track",
            Filter = w101 ? "Observer request|ui-request.json" : "State track|*.jsonl" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        TeachingReviewCli.Attach(_path, picker.FileName, w101);
        _session = LongTeachingSession.Read(_path); LoadState();
        _status.Text = $"Attached {_samples.Length} timestamped readings. Drag the timeline to inspect source, age, UI tree and available facts. No observer was started.";
    }
    private void ReviewProposals()
    {
        if (_path == null || _stop != null) throw new InvalidOperationException("Open a stopped/saved session first.");
        var proposals = TeachingReviewCli.Review(_path);
        var index = _activities.SelectedIndex;
        if (index < 0 || index >= proposals.Length) throw new InvalidOperationException("Select a saved activity to review its proposed card.");
        var proposal = proposals[index];
        _trigger.Text = proposal.Activity.Trigger; _outcome.Text = proposal.Activity.Outcome;
        _state.Text = JsonSerializer.Serialize(proposal, LongTeachingSession.Json);
        _status.Text = "Proposal loaded into the editor. Review evidence and gaps; Update selected accepts your edits. No automatic execution or qualification.";
    }
    private void SaveActivity(bool update)
    {
        if (_session == null || _path == null || _stop != null) throw new InvalidOperationException("Open a saved session first.");
        var index = _activities.SelectedIndex;
        if (update && index < 0) throw new InvalidOperationException("Select an activity to update.");
        var activity = new TeachingActivity(update ? _session.Activities[index].Id : "activity-" + Guid.NewGuid().ToString("N"),
            _label.Text, (double)_start.Value * 1000, (double)_end.Value * 1000, _trigger.Text, _action.Text, _outcome.Text, _commentary.Text);
        LongTeachingSession.Compile(activity, _game.Text);
        var activities = _session.Activities.ToList();
        if (update) activities[index] = activity; else activities.Add(activity);
        var candidate = _session with { Game = _game.Text, Activities = activities.ToArray() };
        LongTeachingSession.Save(_path, candidate); _session = candidate; RefreshTimeline();
    }
    private TeachingActivity Selected() => _session != null && _activities.SelectedIndex >= 0
        ? _session.Activities[_activities.SelectedIndex] : throw new InvalidOperationException("Select a saved activity first.");
    private void OpenVideo()
    {
        if (_path == null || _session == null || _chunks.SelectedIndex < 0) return;
        var path = Path.Combine(Path.GetDirectoryName(_path)!, _session.Chunks[_chunks.SelectedIndex].Directory, "capture.mp4");
        if (!File.Exists(path)) throw new FileNotFoundException("This chunk has no finalized video.");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    private void Export()
    {
        var activity = Selected(); var graph = LongTeachingSession.Compile(activity, _game.Text);
        using var picker = new SaveFileDialog { Filter = "Routine graph|*.json", FileName = activity.Id + ".json" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllText(picker.FileName, JsonSerializer.Serialize(graph, RoutineGraphs.Json));
        var sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(_path!)));
        File.WriteAllText(picker.FileName + ".source.json", JsonSerializer.Serialize(new { session = _path, sha256_session = sourceHash,
            activity, annotation_source = "human", live_qualified = false }, LongTeachingSession.Json));
        _status.Text = "Routine exported. Action IDs still need a game-pack binding; this export grants no live input capability.";
    }
    private void Replay()
    {
        var graph = LongTeachingSession.Compile(Selected(), _game.Text);
        using var picker = new OpenFileDialog { Title = "Choose observed routine evidence (session.json), not a video annotation", Filter = "Replay evidence|session.json" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        var folder = Path.Combine(_root, "reports", "timeline-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var graphPath = Path.Combine(folder, "routine.json"); var report = Path.Combine(folder, "replay.json");
        File.WriteAllText(graphPath, JsonSerializer.Serialize(graph, RoutineGraphs.Json));
        var result = RoutineReplay.RunDetailed(graphPath, picker.FileName, report);
        _status.Text = $"Offline replay {(result.Completed ? "completed" : "stopped")}: {result.RoutineId} / {result.SessionId}. " +
            $"Reason: {result.Reason ?? "observed completion"}. {result.ProposalCount} proposals, {result.ReceiptCount} receipts. " +
            $"Graph {result.GraphSha256[..12]}, events {result.EventsSha256[..12]}. Live qualified: no. No game input sent. Report: {result.ReportPath}";
    }
}
