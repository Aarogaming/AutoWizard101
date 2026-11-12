using System.Diagnostics;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Local research workbench. Games run only after an explicit Run action.</summary>
internal sealed class ExperimentsForm : Form
{
    private readonly string _dataRoot;
    private readonly GameExperiments _store;
    private readonly ComboBox _experiments = new() { Width = 650, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Saved experiments" };
    private readonly TextBox _title = new() { Width = 650, Text = "Dodge survival comparison", AccessibleName = "Experiment title" };
    private readonly TextBox _question = new() { Width = 650, Text = "Does the candidate improve survival and average 50 in two fresh sets?", AccessibleName = "Research question" };
    private readonly ComboBox _kind = new() { Width = 650, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Experiment type" };
    private readonly TextBox _gameId = new() { Width = 650, Text = "wizard101", AccessibleName = "Game identifier" };
    private readonly TextBox _workspace = new() { Width = 650, ReadOnly = true, AccessibleName = "Isolated game folder" };
    private readonly TextBox _baseline = new() { Width = 650, ReadOnly = true, AccessibleName = "Baseline pack" };
    private readonly TextBox _candidate = new() { Width = 650, ReadOnly = true, AccessibleName = "Candidate pack" };
    private readonly TextBox _notes = new() { Width = 650, Text = "Research candidate; review score distribution before using it.", AccessibleName = "Preservation or footage notes" };
    private readonly TextBox _sourceNote = new() { Width = 650, Text = "Local gameplay footage; reviewed visual examples.", AccessibleName = "Footage source and intended use" };
    private readonly TextBox _summary = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 760, Height = 220, AccessibleName = "Comparison results" };
    private readonly Label _description = new() { AutoSize = true, MaximumSize = new Size(760, 0), Text = "Create a comparison or a visual teaching study. Saved evidence stays local." };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(760, 0), Text = "Choose packs and create an experiment. One private game runs at a time." };
    private readonly List<Button> _actions = new();
    private readonly Button _stop = new() { Text = "Stop current work", AutoSize = true, Enabled = false };
    private readonly NumericUpDown _sampleRate = new() { Minimum = 1, Maximum = 60, Value = 4, Width = 80, AccessibleName = "Video samples per second" };
    private readonly NumericUpDown _sampleStart = new() { Minimum = 0, Maximum = 86400, Width = 80, AccessibleName = "Video start in seconds" };
    private readonly ComboBox _footage = new() { Width = 650, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Attached footage" };
    private readonly TextBox _footageSummary = new() { Width = 760, Height = 170, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, AccessibleName = "Footage assessment" };
    private CancellationTokenSource? _stopSource;
    private Task? _task;
    private bool _closeReady, _closing;
    private sealed record Item(string Id, string Title) { public override string ToString() => Title; }

    public ExperimentsForm(string? dataRoot = null)
    {
        _dataRoot = dataRoot ?? WorkshopHost.DataRoot(AppContext.BaseDirectory);
        _store = new(_dataRoot);
        Text = "Maelstrom · Experiments";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(840, 760); MinimumSize = new Size(800, 600);
        AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 10);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        FlowLayoutPanel Panel(string name)
        {
            var page = new TabPage(name);
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20) };
            page.Controls.Add(panel); tabs.TabPages.Add(page); return panel;
        }
        var body = Panel("Results");
        void Label(string text) => body.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(760, 0), Text = text });
        Button Action(string text, Func<Task> action)
        {
            var button = new Button { AutoSize = true, Text = text };
            button.Click += async (_, _) => { try { await action(); } catch (Exception ex) { _status.Text = ex.Message; } };
            _actions.Add(button); body.Controls.Add(button); return button;
        }
        Label("Experiments · compare, learn, preserve");
        body.Controls.Add(_description);
        Label("Saved experiments"); body.Controls.Add(_experiments);
        _experiments.SelectedIndexChanged += (_, _) => RefreshSummary();
        Action("Refresh results", () => { Reload(); return Task.CompletedTask; });
        body.Controls.Add(_summary);
        Action("Run next game", () => RunAsync(1, 25)).Tag = "run";
        Action("Run pending games · 45 minute limit", () => RunAsync(28, 45)).Tag = "run";
        _stop.Click += (_, _) => _stopSource?.Cancel(); body.Controls.Add(_stop);
        Label("Why keep this result?"); body.Controls.Add(_notes);
        Action("Preserve result and evidence", async () =>
        {
            var id = SelectedId(); var note = _notes.Text;
            await WorkAsync(async token => { var path = await Task.Run(() => _store.Preserve(id, note), token); _status.Text = "Saved decision receipt: " + path; });
        });
        Action("Open experiment evidence folder", () => { Process.Start(new ProcessStartInfo(_store.DirectoryPath(SelectedId())) { UseShellExecute = true }); return Task.CompletedTask; });
        body = Panel("New experiment");
        Label("New experiment · changing packs or the application requires a new plan");
        _kind.Items.AddRange(new object[] { "Isolated Dodge comparison", "Footage study · Wizard101 or another game" }); _kind.SelectedIndex = 0;
        _kind.SelectedIndexChanged += (_, _) =>
        {
            _title.Text = _kind.SelectedIndex == 1 ? "Wizard101 visual teaching" : "Dodge survival comparison";
            _question.Text = _kind.SelectedIndex == 1 ? "Which cues reliably identify the game, menu, playing state and outcome?" : "Does the candidate improve survival and average 50 in two fresh sets?";
            _gameId.Enabled = _kind.SelectedIndex == 1;
            foreach (var action in _actions.Where(button => Equals(button.Tag, "dodge-setup"))) action.Enabled = _kind.SelectedIndex == 0;
            _workspace.Enabled = _baseline.Enabled = _candidate.Enabled = _kind.SelectedIndex == 0;
        };
        _gameId.Enabled = false;
        body.Controls.Add(_kind); Label("Game identifier for a footage study (lowercase, digits and hyphens)"); body.Controls.Add(_gameId);
        Label("Title"); body.Controls.Add(_title); Label("Research question"); body.Controls.Add(_question); Label("Dodge setup (used only for comparisons)"); body.Controls.Add(_workspace);
        Action("Choose isolated game folder…", () =>
        {
            using var picker = new FolderBrowserDialog { Description = "Choose AutoWizard101 with its prepared private WSL Dodge game." };
            if (picker.ShowDialog(this) == DialogResult.OK) _workspace.Text = picker.SelectedPath;
            return Task.CompletedTask;
        }).Tag = "dodge-setup";
        body.Controls.Add(_baseline); Action("Choose baseline pack…", () => { ChoosePack(_baseline); return Task.CompletedTask; }).Tag = "dodge-setup";
        body.Controls.Add(_candidate); Action("Choose candidate pack…", () => { ChoosePack(_candidate); return Task.CompletedTask; }).Tag = "dodge-setup";
        Action("Create frozen experiment", async () =>
        {
            var title = _title.Text; var question = _question.Text; var workspace = _workspace.Text; var baseline = _baseline.Text; var candidate = _candidate.Text;
            var footageStudy = _kind.SelectedIndex == 1; var gameId = _gameId.Text.Trim();
            await WorkAsync(async token =>
            {
                var id = await Task.Run(() => footageStudy ? _store.CreateFootageStudy(title, question, gameId) : _store.CreateDodge(title, question, workspace, baseline, candidate, DodgeExperimentRunner.Fingerprints(workspace)), token);
                Reload(id); tabs.SelectedIndex = 0; _status.Text = "Plan saved. Packs and test openings are frozen. Choose Run when ready.";
                if (footageStudy) { tabs.SelectedIndex = 2; _status.Text = "Visual study saved. Import and label footage for this game; gameplay controls are unavailable."; }
            });
        });
        body = Panel("Gameplay footage");
        Label("Gameplay footage · attach visual examples to the experiment selected in Results");
        Label("Describe the source, game, and intended use"); body.Controls.Add(_sourceNote);
        Label("Samples per second / start in seconds. Default: 4 fps; up to 600 samples. Higher rates shorten the clip. Video alone does not establish the player's keys or rewards.");
        var samples = new FlowLayoutPanel { AutoSize = true }; samples.Controls.Add(_sampleRate); samples.Controls.Add(_sampleStart); body.Controls.Add(samples);
        Action("Import and review local gameplay video…", ImportVideoAsync);
        Action("Review and attach existing recording…", async () =>
        {
            var id = SelectedId();
            using var picker = new OpenFileDialog { Title = "Choose a teaching recording", Filter = "Recording manifest|recording.json" };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            TeachingRecording? recording = null;
            await WorkAsync(async token => { recording = await Task.Run(() => TeachingRecording.Open(picker.FileName), token); });
            if (!_closing && recording != null) await ReviewAndAttachAsync(id, recording);
        });
        Label("Saved footage assessments"); body.Controls.Add(_footage); body.Controls.Add(_footageSummary);
        Action("Verify selected footage", async () =>
        {
            if (_footage.SelectedItem is not Item item) throw new InvalidOperationException("Select attached footage first.");
            await WorkAsync(async token =>
            {
                var quality = await Task.Run(() => FootageAssessment.Verify(item.Id), token);
                _footageSummary.Text = $"{quality.Frames} images · {quality.LabeledFrames} labeled · {quality.UniqueImages} distinct images\r\n" + string.Join("\r\n", quality.Findings);
            });
        });
        Label("W101 packs remain part of the plan. Home-session movement and minigame control need their own tested adapter and explicit controls.");
        _status.Dock = DockStyle.Bottom; _status.Padding = new Padding(12); _status.MaximumSize = new Size(800, 140);
        Controls.Add(tabs); Controls.Add(_status);
        try { _workspace.Text = TestGameWorkspaceStore.Load(_dataRoot) ?? ToolIntegrationService.FindWorkspace(AppContext.BaseDirectory) ?? ""; }
        catch (Exception ex) { _status.Text = ex.Message; }
        Reload();
        if (_experiments.Items.Count == 0) tabs.SelectedIndex = 1;
        FormClosing += async (_, e) =>
        {
            if (_closeReady || _task == null) return;
            e.Cancel = true;
            if (_closing) return;
            _closing = true; _stopSource?.Cancel();
            try { await _task; } catch { }
            _closeReady = true; Close();
        };
    }

    private string SelectedId() => (_experiments.SelectedItem as Item)?.Id ?? throw new InvalidOperationException("Create or select an experiment first.");
    private void ChoosePack(TextBox field)
    {
        using var picker = new OpenFileDialog { Title = "Choose a draft Dodge pack", Filter = "Game pack|*.json" };
        if (picker.ShowDialog(this) == DialogResult.OK) field.Text = picker.FileName;
    }
    private void Reload(string? selected = null)
    {
        selected ??= (_experiments.SelectedItem as Item)?.Id;
        _experiments.Items.Clear();
        foreach (var id in _store.ListIds())
        {
            try { _experiments.Items.Add(new Item(id, _store.Load(id).Title + " · " + id)); }
            catch { _experiments.Items.Add(new Item(id, "Needs review · " + id)); }
        }
        _experiments.SelectedIndex = _experiments.Items.Cast<Item>().ToList().FindIndex(item => item.Id == selected);
        if (_experiments.SelectedIndex < 0 && _experiments.Items.Count > 0) _experiments.SelectedIndex = 0;
    }
    private void RefreshSummary()
    {
        if (_experiments.SelectedItem == null) { _summary.Clear(); return; }
        try
        {
            var plan = _store.Load(SelectedId()); var rows = _store.Results(plan);
            _description.Text = plan.Kind == "footage-study" ? $"{plan.Title} · {plan.GameId}\nVisual teaching: attach, review and preserve gameplay examples."
                : $"{plan.Title}\nPrivate 25% Dodge: four paired development games, then two reserved sets of ten if the candidate improves and averages 50.";
            foreach (var action in _actions.Where(button => Equals(button.Tag, "run"))) action.Enabled = plan.Kind == "dodge-comparison" && _task == null;
            var lines = new List<string> { plan.Question, GameExperiments.Verdict(plan, rows), "" };
            foreach (var (stage, role) in new[] { ("development", "baseline"), ("development", "candidate"), ("validation-1", "candidate"), ("validation-2", "candidate") })
            {
                if (plan.Kind != "dodge-comparison") break;
                var stats = GameExperiments.Statistics(plan, rows, stage, role);
                lines.Add($"{stage} / {role}: {stats.Completed}/{stats.Expected} complete · mean {stats.Mean?.ToString("0.0") ?? "pending"} · median {stats.Median?.ToString("0.0") ?? "pending"} · minimum {stats.Minimum?.ToString() ?? "pending"} · ≥50: {stats.AtTarget?.ToString() ?? "pending"}");
                lines.Add("Scores: " + string.Join(", ", stats.Scores));
            }
            lines.AddRange(rows.Where(row => row.Status != "complete").Select(row => $"Trial {row.Number + 1}: {row.Status} · {row.StopReason}"));
            _summary.Lines = lines.ToArray();
            var prior = (_footage.SelectedItem as Item)?.Id;
            _footage.Items.Clear();
            var footageRoot = Path.Combine(_store.DirectoryPath(plan.Id), "footage");
            if (Directory.Exists(footageRoot))
                foreach (var path in Directory.EnumerateFiles(footageRoot, "assessment.json", SearchOption.AllDirectories).OrderDescending())
                {
                    try
                    {
                        using var metadata = System.Text.Json.JsonDocument.Parse(GameExperiments.BoundedBytes(path, 2_000_000));
                        var note = metadata.RootElement.GetProperty("sourceNote").GetString() ?? "Footage";
                        var frames = metadata.RootElement.GetProperty("quality").GetProperty("Frames").GetInt32();
                        _footage.Items.Add(new Item(path, $"{frames} images · {note[..Math.Min(note.Length, 90)]}"));
                    }
                    catch { _footage.Items.Add(new Item(path, "Needs review · " + Path.GetFileName(Path.GetDirectoryName(path)))); }
                }
            _footage.SelectedIndex = _footage.Items.Cast<Item>().ToList().FindIndex(item => item.Id == prior);
            if (_footage.SelectedIndex < 0 && _footage.Items.Count > 0) _footage.SelectedIndex = 0;
            if ((_footage.SelectedItem as Item)?.Id != prior) _footageSummary.Clear();
        }
        catch (Exception ex) { _summary.Text = "Evidence needs review: " + ex.Message; }
    }
    private Task RunAsync(int games, int minutes)
    {
        var id = SelectedId();
        var progress = new Progress<string>(message => { if (!IsDisposed) { _status.Text = message; RefreshSummary(); } });
        return WorkAsync(token => Task.Run(() => DodgeExperimentRunner.RunAsync(_store, id, games, minutes, progress, token), token));
    }
    private async Task WorkAsync(Func<CancellationToken, Task> work)
    {
        if (_task != null) return;
        using var cancellation = new CancellationTokenSource(); _stopSource = cancellation;
        foreach (var action in _actions) action.Enabled = false;
        _experiments.Enabled = _kind.Enabled = false; _stop.Enabled = true;
        try { _task = work(cancellation.Token); await _task; }
        catch (OperationCanceledException) { _status.Text = "Stopped. Saved evidence remains; an interrupted game is unranked."; }
        catch (Exception ex) { _status.Text = "Work stopped: " + ex.Message; }
        finally
        {
            _task = null; _stopSource = null;
            if (!IsDisposed)
            {
                foreach (var action in _actions) action.Enabled = !Equals(action.Tag, "dodge-setup") || _kind.SelectedIndex == 0;
                _experiments.Enabled = _kind.Enabled = true; _stop.Enabled = false; RefreshSummary();
            }
        }
    }
    private async Task ImportVideoAsync()
    {
        var id = SelectedId();
        var ffmpeg = VideoTools.FindFfmpeg(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH")) ?? throw new InvalidOperationException("Install the packaged FFmpeg tools or put FFmpeg and ffprobe on PATH.");
        using var picker = new OpenFileDialog { Title = "Choose local gameplay footage", Filter = "Video|*.mp4;*.mkv;*.webm;*.mov;*.avi" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        var rate = (int)_sampleRate.Value; var start = (int)_sampleStart.Value;
        TeachingRecording? recording = null;
        await WorkAsync(async token =>
        {
            _status.Text = "Sampling a bounded clip with one decoder thread…";
            recording = await Task.Run(() => VideoTeachingImport.RunAsync(_dataRoot, "experiment-" + id, picker.FileName, ffmpeg, token, rate, start), token);
        });
        if (!_closing && recording != null) await ReviewAndAttachAsync(id, recording);
    }
    private async Task ReviewAndAttachAsync(string id, TeachingRecording recording)
    {
        using (var review = new TeachingReviewForm(recording, _dataRoot, null)) review.ShowDialog(this);
        var note = _sourceNote.Text;
        await WorkAsync(async token =>
        {
            await Task.Run(() => FootageAssessment.Attach(_store, id, recording, note), token);
            var quality = FootageAssessment.Assess(recording.Frames);
            _status.Text = $"Attached {quality.Frames} images, {quality.LabeledFrames} labeled. " + string.Join(" ", quality.Findings);
        });
    }

    internal void RenderForVerification(string directory)
    {
        Directory.CreateDirectory(directory);
        _ = Handle; // Construct the window offscreen; never show or activate it.
        var tabs = Controls.OfType<TabControl>().Single();
        for (var i = 0; i < tabs.TabPages.Count; i++)
        {
            tabs.SelectedIndex = i; PerformLayout();
            var panel = tabs.TabPages[i].Controls.OfType<FlowLayoutPanel>().Single();
            void CreateHandles(Control control)
            {
                _ = control.Handle;
                foreach (Control child in control.Controls) CreateHandles(child);
            }
            CreateHandles(tabs); panel.PerformLayout();
            using var bitmap = new Bitmap(panel.Width, panel.Height);
            panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, panel.Size));
            bitmap.Save(Path.Combine(directory, $"experiment-tab-{i}.png"));
        }
    }
}
