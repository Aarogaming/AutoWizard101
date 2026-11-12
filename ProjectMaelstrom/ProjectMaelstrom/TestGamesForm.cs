using System.Diagnostics;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Launches optional, locally prepared games for manual teaching sessions.</summary>
internal sealed class TestGamesForm : Form
{
    private string? _workspace;
    private string? _nativePack, _nativeReport;
    private CancellationTokenSource? _nativeStop;
    private Task<string>? _nativeTask;
    private bool _closing, _closeReady;
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(660, 0) };

    public TestGamesForm(bool dodgeOnly = false)
    {
        using var integrations = new ToolIntegrationService();
        try { _workspace = TestGameWorkspaceStore.Load(WorkshopHost.DataRoot(AppContext.BaseDirectory)) ?? integrations.Workspace; }
        catch (Exception) { _workspace = integrations.Workspace; }
        Text = dodgeOnly ? "Maelstrom · Dodge test controls" : "Maelstrom · Local test games";
        ClientSize = new Size(720, 400);
        MinimumSize = new Size(640, 350);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        body.Controls.Add(new Label { Text = dodgeOnly ? "Dodge · local and isolated tests" : "Local test games", AutoSize = true, Font = new Font(Font.FontFamily, 18, FontStyle.Bold) });
        body.Controls.Add(new Label { Text = dodgeOnly ? "Open the local game for recording, or choose an isolated test below.\nNothing starts until you choose Open or Run. Use stepped 25% for slow practice." : "1. Open Reflex to practice reacting to a cue.\n2. Open Dodge the Creeps to practice continuous movement.\n3. In Observe, refresh the window list, select the game, and record a teaching sample.", AutoSize = true, MaximumSize = new Size(660, 0), Margin = new Padding(0, 12, 0, 12) });
        var reflex = new Button { Text = "Open Reflex (local)", AutoSize = true };
        var dodge = new Button { Text = "Open Dodge the Creeps", AutoSize = true };
        var folder = new Button { Text = "Choose AutoWizard101 folder…", AutoSize = true };
        var native = new Button { Text = "Run headless Dodge test", AutoSize = true };
        var isolated = new Button { Text = "Run isolated visual Dodge test (WSL)", AutoSize = true };
        var speed = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 225 };
        speed.Items.AddRange(new object[] { "Real-time test", "Stepped: 25% speed (start here)", "Stepped: 50% speed", "Stepped: 100% speed" });
        speed.SelectedIndex = 1;
        var endurance = new CheckBox { Text = "Train toward score 50 (stepped test, up to 12 minutes)", AutoSize = true };
        endurance.CheckedChanged += (_, _) => { if (endurance.Checked && speed.SelectedIndex == 0) speed.SelectedIndex = 1; };
        speed.SelectedIndexChanged += (_, _) => { if (speed.SelectedIndex == 0) endurance.Checked = false; };
        var choosePack = new Button { Text = "Choose Dodge pack…", AutoSize = true };
        var openReport = new Button { Text = "Open test report folder", AutoSize = true, Enabled = false };
        reflex.Click += (_, _) => Launch(() => LocalTestGames.ReflexStartInfo(RequireWorkspace(), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe")));
        dodge.Click += (_, _) => Launch(() => LocalTestGames.DodgeStartInfo(RequireWorkspace()));
        folder.Click += (_, _) =>
        {
            using var picker = new FolderBrowserDialog { Description = "Select the AutoWizard101 folder containing .local/test-games." };
            if (picker.ShowDialog(this) == DialogResult.OK)
            {
                try { TestGameWorkspaceStore.Save(WorkshopHost.DataRoot(AppContext.BaseDirectory), picker.SelectedPath); _workspace = picker.SelectedPath; UpdateStatus(); }
                catch (Exception ex) { _status.Text = "Could not save the test-game folder: " + ex.Message; }
            }
        };
        choosePack.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Title = "Choose a Dodge schema 2 pack", Filter = "Game pack|*.json", InitialDirectory = Path.Combine(WorkshopHost.DataRoot(AppContext.BaseDirectory), "game-packs") };
            if (picker.ShowDialog(this) == DialogResult.OK) { _nativePack = picker.FileName; UpdateStatus(); }
        };
        openReport.Click += (_, _) =>
        {
            if (_nativeReport == null) return;
            try { Process.Start(new ProcessStartInfo(Path.GetDirectoryName(_nativeReport)!) { UseShellExecute = true }); }
            catch (Exception ex) { _status.Text = "Could not open report folder: " + ex.Message; }
        };
        native.Click += async (_, _) =>
        {
            if (_nativeStop != null) { _nativeStop.Cancel(); return; }
            var cancellation = new CancellationTokenSource();
            _nativeStop = cancellation;
            native.Text = "Stop headless test";
            isolated.Enabled = false;
            choosePack.Enabled = folder.Enabled = speed.Enabled = endurance.Enabled = false;
            try
            {
                var workspace = RequireWorkspace();
                var source = Path.GetDirectoryName(LocalTestGames.DodgePath(workspace))!;
                var probe = Path.Combine(AppContext.BaseDirectory, "tools", "fixtures", "godot_dodge_native_probe.gd");
                if (!File.Exists(probe)) probe = Path.Combine(workspace, "tools", "fixtures", "godot_dodge_native_probe.gd");
                var pack = _nativePack ?? Path.Combine(AppContext.BaseDirectory, "tools", "fixtures", "game_packs", "dodge", "pack.json");
                if (!File.Exists(pack) && _nativePack == null) pack = Path.Combine(workspace, "tools", "fixtures", "game_packs", "dodge", "pack.json");
                var engine = LocalTestGames.GodotPath(workspace);
                var dataRoot = WorkshopHost.DataRoot(AppContext.BaseDirectory);
                _status.Text = "Preparing a capped, headless test copy. No game window, desktop input, or AI model is used.";
                var task = Task.Run(async () =>
                {
                    var copy = DodgeNativeVerification.PrepareProject(source, probe, dataRoot, cancellation.Token);
                    return await DodgeNativeVerification.RunAsync(engine, copy, pack, dataRoot, cancellation.Token);
                });
                _nativeTask = task;
                var report = await task;
                if (!IsDisposed && !_closing)
                {
                    _nativeReport = report;
                    openReport.Enabled = true;
                    using var result = System.Text.Json.JsonDocument.Parse(File.ReadAllText(report));
                    _status.Text = $"Headless test: {result.RootElement.GetProperty("action_count").GetInt32()} action requests · {result.RootElement.GetProperty("stop_reason").GetString()}\nOpen the report for observed movement. Engine state was used; desktop and vision are not tested.";
                }
            }
            catch (OperationCanceledException) { if (!IsDisposed && !_closing) _status.Text = "Headless test stopped."; }
            catch (Exception ex) { if (!IsDisposed && !_closing) _status.Text = "Headless test failed: " + ex.Message; }
            finally
            {
                _nativeTask = null;
                _nativeStop = null;
                cancellation.Dispose();
                if (!IsDisposed) { native.Text = "Run headless Dodge test"; isolated.Enabled = true; choosePack.Enabled = folder.Enabled = speed.Enabled = endurance.Enabled = true; }
            }
        };
        isolated.Click += async (_, _) =>
        {
            if (_nativeStop != null) { _nativeStop.Cancel(); return; }
            var cancellation = new CancellationTokenSource();
            _nativeStop = cancellation;
            isolated.Text = "Stop isolated test";
            native.Enabled = choosePack.Enabled = folder.Enabled = speed.Enabled = endurance.Enabled = false;
            try
            {
                var workspace = RequireWorkspace();
                var pack = _nativePack ?? Path.Combine(workspace, "tools", "fixtures", "game_packs", endurance.Checked ? "dodge-context-v12" : "dodge-context", "pack.json");
                _status.Text = endurance.Checked ? "Training toward score 50 in a private WSL display. This can take several minutes; your Windows desktop is not controlled." : "Running a short visual Dodge test in a private WSL display. Your Windows desktop is not controlled.";
                var stepped = speed.SelectedIndex > 0;
                var trainingSpeed = speed.SelectedIndex switch { 1 => 0.25, 2 => 0.5, _ => 1.0 };
                var training = endurance.Checked;
                var task = Task.Run(async () =>
                {
                    using var studyLock = DodgeExperimentRunner.AcquireStudy(workspace);
                    return await DodgeWslVerification.RunAsync(workspace, pack, cancellation.Token, stepped: stepped, speed: trainingSpeed, endurance: training);
                });
                _nativeTask = task;
                var report = await task;
                if (!IsDisposed && !_closing)
                {
                    _nativeReport = report;
                    openReport.Enabled = true;
                    using var result = System.Text.Json.JsonDocument.Parse(File.ReadAllText(report));
                    var score = training && result.RootElement.TryGetProperty("evaluation", out var evaluation) && evaluation.ValueKind == System.Text.Json.JsonValueKind.Object && evaluation.TryGetProperty("score", out var scoreElement) && scoreElement.ValueKind == System.Text.Json.JsonValueKind.Number ? $" · score {scoreElement.GetInt32()}" : "";
                    _status.Text = $"Private WSL visual test: {result.RootElement.GetProperty("action_count").GetInt32()} actions{score} · {result.RootElement.GetProperty("stop_reason").GetString()}\nOpen the report and frames for the observed result.";
                }
            }
            catch (OperationCanceledException) { if (!IsDisposed && !_closing) _status.Text = "Isolated test stopped."; }
            catch (Exception ex) { if (!IsDisposed && !_closing) _status.Text = "Isolated test failed: " + ex.Message; }
            finally
            {
                _nativeTask = null;
                _nativeStop = null;
                cancellation.Dispose();
                if (!IsDisposed) { isolated.Text = "Run isolated visual Dodge test (WSL)"; native.Enabled = choosePack.Enabled = folder.Enabled = speed.Enabled = endurance.Enabled = true; }
            }
        };
        if (!dodgeOnly) body.Controls.Add(reflex);
        body.Controls.Add(dodge);
        body.Controls.Add(folder);
        body.Controls.Add(choosePack);
        body.Controls.Add(native);
        body.Controls.Add(speed);
        body.Controls.Add(endurance);
        body.Controls.Add(isolated);
        body.Controls.Add(openReport);
        _status.Margin = new Padding(0, 12, 0, 0);
        body.Controls.Add(_status);
        Controls.Add(body);
        FormClosing += async (_, e) =>
        {
            if (_closeReady) return;
            if (_nativeTask == null) return;
            e.Cancel = true;
            if (_closing) return;
            _closing = true;
            _nativeStop?.Cancel();
            try { await _nativeTask; } catch { }
            _closeReady = true;
            Close();
        };
        UpdateStatus();
    }

    private string RequireWorkspace() => _workspace ?? throw new InvalidOperationException("Choose the AutoWizard101 folder first.");

    private void UpdateStatus()
    {
        _status.Text = _workspace == null ? "Choose the folder where the test games were prepared." :
            $"Folder: {_workspace}\nReflex: {(File.Exists(LocalTestGames.ReflexPath(_workspace)) ? "ready" : "not found")} · Dodge: {(File.Exists(LocalTestGames.DodgePath(_workspace)) && File.Exists(LocalTestGames.GodotPath(_workspace)) ? "ready" : "not found")}\nHeadless pack: {(_nativePack == null ? "built-in Dodge sample" : Path.GetFileName(_nativePack))}.";
    }

    private void Launch(Func<ProcessStartInfo> startInfo)
    {
        try
        {
            using var process = Process.Start(startInfo()) ?? throw new InvalidOperationException("The game did not start.");
            _status.Text = "Game opened. Return to Observe, refresh the window list, and select its window.";
        }
        catch (Exception ex) { _status.Text = "Could not open the game: " + ex.Message; }
    }
}
