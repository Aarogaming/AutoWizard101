using System.Diagnostics;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>A game workspace for configuring one bounded Dance visit. Opening
/// the separate player does not start inputs; it retains visible Start/Stop.</summary>
internal sealed class PetDancePlayerForm : Form
{
    private readonly string _dataRoot;
    private readonly ComboBox _windows = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 770, AccessibleName = "Wizard101 window for Pet Dance" };
    private readonly ComboBox _arena = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, AccessibleName = "Pet Dance arena" };
    private readonly CheckBox _idle = new() { Text = "Return to native Idle after a completed game", AutoSize = true };
    private readonly CheckBox _audio = new() { Text = "Use selected game audio as backup for unclear arrows", AutoSize = true, Checked = false };
    private readonly Button _open = new() { Text = "Open player", AutoSize = true, Enabled = false };
    private readonly Button _refresh = new() { Text = "Refresh windows", AutoSize = true };
    private readonly Button _settings = new() { Text = "Snack and goal settings…", AutoSize = true };
    private readonly Button _save = new() { Text = "Save routine settings", AutoSize = true };
    private readonly Button _controls = new() { Text = "Game control settings…", AutoSize = true };
    private readonly Label _status = NoteLabel(770);
    private readonly Label _qualification = NoteLabel(770);
    private readonly Label _snackSummary = NoteLabel(770);
    private readonly Label _idleSummary = NoteLabel(166);
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 500 };
    private PetTrainingPreferences _preferences;
    private PetDancePreset? _preset;
    private Process? _player;
    private string? _lastReport;
    private bool _updating;

    public PetDancePlayerForm(string dataRoot, GameWindow? preferred = null)
    {
        _dataRoot = dataRoot;
        BindingContext = new BindingContext();
        _preferences = PetTrainingPreferences.Load(dataRoot);
        Text = "Maelstrom · Wizard101 · Pet Dance";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        Size = new Size(940, 760);
        MinimumSize = new Size(850, 620);
        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(24)
        };
        void Note(string text)
        {
            var label = NoteLabel(770);
            label.Text = text;
            body.Controls.Add(label);
        }
        var heading = new Label { Text = "Wizard101  /  Pets  /  Pet Dance", AutoSize = true, Font = new Font(Font.FontFamily, 17, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) };
        body.Controls.Add(heading);
        Note("One game, a clear finish. Set up the steps below, then open the player when you are ready.");

        var steps = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(790, 0), WrapContents = true, Margin = new Padding(0, 6, 0, 14) };
        steps.Controls.Add(Step("1  PLAY", "Watch fresh arrow sequences and repeat them during Go."));
        steps.Controls.Add(Step("2  REVIEW REWARD", "Keep the game result and timing evidence in the session report."));
        steps.Controls.Add(Step("3  SNACK POLICY", "Finish without feeding. Saved preferences are for review only."));
        steps.Controls.Add(Step("4  NATIVE IDLE", null, _idleSummary));
        body.Controls.Add(steps);

        Note("Game window");
        body.Controls.Add(_windows);
        body.Controls.Add(_refresh);
        Note("Use windowed 1280 × 720. Select this same arena inside Wizard101; changing this setting does not click the game's arena selector.");
        var arenaRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
        arenaRow.Controls.Add(new Label { Text = "Dance arena", AutoSize = true, Margin = new Padding(0, 6, 14, 0) });
        arenaRow.Controls.Add(_arena);
        _arena.FormattingEnabled = true;
        _arena.Format += (_, args) => { if (args.ListItem is PetDanceArena arena) args.Value = ArenaName(arena); };
        _arena.Items.AddRange(Enum.GetValues<PetDanceArena>().Cast<object>().ToArray());
        arenaRow.Controls.Add(_save);
        body.Controls.Add(arenaRow);
        body.Controls.Add(_qualification);
        body.Controls.Add(_audio);
        Note("Audio backup uses only the selected game's sound. Your microphone and volume are unchanged.");
        body.Controls.Add(_idle);
        Note("Automatic Idle runs only after a valid game end. Stop or Escape ends control immediately; it never starts a cleanup action after you stop.");
        body.Controls.Add(_snackSummary);
        body.Controls.Add(_settings);
        Note("Crown Shop purchases are locked. No purchases or automatic feeding.");
        Note("Open player gives you separate Start and Stop controls. Leave native Idle, start the player, then return to the Dance sigil and begin the game. Keep the keyboard and mouse quiet during play. Maelstrom does not bring the game forward or repeat games by itself.");
        var actions = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(790, 0), WrapContents = true, Margin = new Padding(0, 10, 0, 8) };
        var reports = new Button { Text = "Open player reports", AutoSize = true };
        var close = new Button { Text = "Close", AutoSize = true };
        actions.Controls.AddRange([_open, _controls, reports, close]);
        body.Controls.Add(actions);
        body.Controls.Add(_status);
        Controls.Add(body);
        WorkspaceTheme.Apply(this);
        heading.ForeColor = WorkspaceTheme.Palette.Accent;

        _refresh.Click += (_, _) => RefreshWindows(_windows.SelectedItem as GameWindow);
        _windows.SelectedIndexChanged += (_, _) => CheckSelection();
        _arena.SelectedIndexChanged += (_, _) => PreferencesChanged();
        _idle.CheckedChanged += (_, _) => PreferencesChanged();
        _save.Click += (_, _) => { if (SavePreferences()) _status.Text = "Routine settings saved. Nothing has started."; };
        _settings.Click += (_, _) => OpenSettings();
        _open.Click += (_, _) => OpenPlayer();
        _controls.Click += (_, _) =>
        {
            using var tools = new ToolCenterForm(checkOnShow: false);
            tools.ShowDialog(this);
            CheckSelection();
        };
        reports.Click += (_, _) =>
        {
            try
            {
                var folder = _lastReport != null && Directory.Exists(_lastReport) ? _lastReport : Path.Combine(_dataRoot, "reports", "pet-dance");
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception error) { _status.Text = error.Message; }
        };
        close.Click += (_, _) => Close();
        _poll.Tick += (_, _) =>
        {
            if (_player == null || !_player.HasExited) return;
            var code = _player.ExitCode;
            _player.Dispose(); _player = null; _poll.Stop();
            SetEditingEnabled(true);
            CheckSelection();
            _status.Text = code == 0 ? "Player closed. Review its report for the game result and whether native Idle was observed." :
                "The player stopped with an error. Open player reports for the reason before retrying.";
        };
        FormClosing += (_, args) =>
        {
            if (_player is { HasExited: false })
            {
                args.Cancel = true;
                _status.Text = "Stop or close the separate player before closing this workspace.";
            }
        };
        FormClosed += (_, _) => { _poll.Stop(); _poll.Dispose(); _player?.Dispose(); };
        Shown += (_, _) =>
        {
            try { _preset = PetDancePreset.Load(AppContext.BaseDirectory); RefreshWindows(preferred); }
            catch (Exception error) { _status.Text = error.Message; _open.Enabled = false; }
        };
        ShowPreferences();
    }

    private static Label NoteLabel(int width) => new() { AutoSize = true, MaximumSize = new Size(width, 0), Margin = new Padding(0, 4, 0, 8) };

    private static Panel Step(string title, string? text, Label? content = null)
    {
        var panel = new FlowLayoutPanel
        {
            Width = 188, Height = 126, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(10), Margin = new Padding(0, 0, 8, 4)
        };
        panel.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) });
        var label = content ?? NoteLabel(166);
        if (text != null) label.Text = text;
        panel.Controls.Add(label);
        return panel;
    }

    private void ShowPreferences()
    {
        _updating = true;
        _arena.SelectedItem = _preferences.Arena;
        _idle.Checked = _preferences.ReturnToNativeIdle;
        _updating = false;
        UpdateSummaries();
    }

    private void PreferencesChanged()
    {
        if (_updating) return;
        _preferences = CurrentPreferences();
        UpdateSummaries();
        _status.Text = "Settings changed. Save them here or open the player to save and use them.";
    }

    private PetTrainingPreferences CurrentPreferences() => _preferences with
    {
        Arena = _arena.SelectedItem is PetDanceArena arena ? arena : PetDanceArena.WizardCity,
        ReturnToNativeIdle = _idle.Checked
    };

    private void UpdateSummaries()
    {
        var firstArena = _preferences.Arena == PetDanceArena.WizardCity;
        if (!firstArena) _audio.Checked = false;
        _audio.Enabled = firstArena && _player is not { HasExited: false };
        _qualification.Text = firstArena
            ? "Wizard City: recorded perfect games at Tiny, Large and Huge across development builds. The latest changed-size qualification batch is still incomplete."
            : $"{ArenaName(_preferences.Arena)}: not yet qualified. Select it manually in the game. Audio backup is off until this arena's music and cues are tested.";
        _snackSummary.Text = "Snack policy: " + _preferences.SnackStatus;
        _idleSummary.Text = _preferences.ReturnToNativeIdle
            ? "After a valid result, finish the visit and verify native Idle."
            : "Automatic return is off. Return to native Idle yourself after play.";
    }

    private bool SavePreferences()
    {
        try
        {
            _preferences = PetTrainingPreferences.Normalize(CurrentPreferences());
            PetTrainingPreferences.Save(_dataRoot, _preferences);
            UpdateSummaries();
            return true;
        }
        catch (Exception error) { _status.Text = "Settings could not be saved: " + error.Message; return false; }
    }

    private void OpenSettings()
    {
        using var settings = new PetTrainingPreferencesForm(CurrentPreferences());
        if (settings.ShowDialog(this) != DialogResult.OK) return;
        _preferences = settings.Result;
        ShowPreferences();
        if (SavePreferences()) _status.Text = "Snack and goal preferences saved. Automatic feeding is not available.";
    }

    private void SetEditingEnabled(bool enabled)
    {
        _windows.Enabled = _refresh.Enabled = _arena.Enabled = _idle.Enabled = _settings.Enabled = _save.Enabled = _controls.Enabled = enabled;
        UpdateSummaries();
    }

    private void RefreshWindows(GameWindow? preferred)
    {
        _windows.Items.Clear();
        foreach (var window in GameWindows.List().Where(HomeMovementGate.IsWizard101)) _windows.Items.Add(window);
        _windows.SelectedItem = _windows.Items.Cast<GameWindow>().FirstOrDefault(window => preferred != null && GameWindows.SameIdentity(preferred, window));
        if (_windows.SelectedIndex < 0 && _windows.Items.Count == 1) _windows.SelectedIndex = 0;
        CheckSelection();
    }

    private static string ArenaName(PetDanceArena arena) => arena switch
    {
        PetDanceArena.WizardCity => "Wizard City",
        PetDanceArena.Mooshu => "MooShu",
        _ => arena.ToString()
    };

    private void CheckSelection()
    {
        _open.Enabled = false;
        if (_player is { HasExited: false }) return;
        if (_preset == null) { _status.Text = "The included player files could not be loaded. Repair or reinstall Maelstrom."; return; }
        if (!ExecutionPolicyManager.Current.AllowLiveAutomation)
        { _status.Text = "Game controls are off. Open Game control settings, then enable Allow game controls in Tools. Nothing has been enabled automatically."; return; }
        if (_windows.SelectedItem is not GameWindow target) { _status.Text = "Open Wizard101 and select its window. Nothing is running."; return; }
        try
        {
            if (!GameWindows.IsCurrent(target) || GameWindows.Minimized(target)) throw new InvalidOperationException("Keep the selected Wizard101 window open and visible, then refresh.");
            var client = GameWindows.ClientSize(target);
            if (client != new Size(1280, 720))
            { _status.Text = $"Selected game area: {client.Width} × {client.Height}. Set Wizard101 to windowed 1280 × 720, then refresh."; return; }
            _open.Enabled = true;
            _status.Text = "1280 × 720 game area detected. Opening the player will not start gameplay; its visible Start control is still required.";
        }
        catch (Exception error) { _status.Text = error.Message; }
    }

    private void OpenPlayer()
    {
        CheckSelection(); if (!_open.Enabled || !SavePreferences()) return;
        try
        {
            var target = (GameWindow)_windows.SelectedItem!;
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("The installed Maelstrom application path is unavailable.");
            var launch = PetDancePlayerLauncher.BuildStartInfo(executable, _preset!, target, _dataRoot, _audio.Checked, training: _preferences);
            if (!GameWindows.IsCurrent(target)) throw new InvalidOperationException("Wizard101 changed. Refresh and select its window again.");
            Directory.CreateDirectory(Path.GetDirectoryName(launch.ReportDirectory)!);
            _player = Process.Start(launch.StartInfo) ?? throw new InvalidOperationException("The Pet Dance player could not open.");
            _lastReport = launch.ReportDirectory;
            _open.Enabled = false;
            SetEditingEnabled(false);
            _status.Text = "Player opened with Start and Stop controls. Routine settings are locked until it closes. Reports remain in your data folder.";
            _poll.Start();
        }
        catch (Exception error) { _status.Text = error.Message; }
    }
}
