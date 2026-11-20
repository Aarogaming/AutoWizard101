using ProjectMaelstrom.Models;
using ProjectMaelstrom.Utilities;
using System.ComponentModel;

namespace ProjectMaelstrom;

internal sealed class GuidedActivityForm : Form
{
    private readonly ToolIntegrationService _service;
    private readonly IReadOnlyDictionary<string, ToolReadiness> _tools;
    private readonly Func<string?> _nativeBlock;
    private readonly Panel _page = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly Label _heading = new() { AutoSize = true, Font = new Font("Segoe UI", 19, FontStyle.Bold) };
    private readonly Label _message = new() { AutoSize = true, MaximumSize = new Size(820, 0) };
    private readonly RadioChoices _activity = new();
    private readonly RadioChoices _combat = new();
    private readonly RadioChoices _resolution = new();
    private readonly TextBox _custom = new() { Multiline = true, Width = 730, Height = 70, MaxLength = 16000, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _label = new() { Width = 360, MaxLength = 100 };
    private readonly NumericUpDown _minutes = Number(1, 180, 15);
    private readonly NumericUpDown _health = Number(0, 100, 20);
    private readonly NumericUpDown _mana = Number(0, 100, 10);
    private readonly NumericUpDown _games = Number(1, 100, 1);
    private readonly CheckBox _potions = Check("Use available potions", true);
    private readonly CheckBox _buy = Check("Allow buying potions with gold");
    private readonly CheckBox _team = Check("Use Team Up at this entrance");
    private readonly CheckedListBox _locations = new() { CheckOnClick = true, Width = 250, Height = 125 };
    private readonly CheckedListBox _snacks = new() { CheckOnClick = true, Width = 250, Height = 125 };
    private readonly CheckBox _signedIn = Check("I am signed in on the wizard I want to use.");
    private readonly CheckBox _prepared = Check("");
    private readonly CheckBox _tracked = Check("");
    private readonly CheckBox _petResources = Check("I have checked my pet's energy and the snacks in the selected positions.");
    private readonly CheckBox _allow = Check("Allow game controls for this app");
    private readonly Label _checks = new() { AutoSize = true, MaximumSize = new Size(810, 0) };
    private readonly TextBox _summary = new() { Multiline = true, ReadOnly = true, Width = 780, Height = 230, ScrollBars = ScrollBars.Vertical };
    private readonly Label _progress = new() { AutoSize = true, MaximumSize = new Size(800, 0) };
    private readonly Button _back = Button("Back");
    private readonly Button _next = Button("Next");
    private readonly Button _stop = Button("Stop activity");
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };
    private readonly List<Control> _pages = new();
    private int _step;
    private bool _busy;
    private bool _validated;
    private bool _liveChecked;
    private bool _started;
    private bool _closing;
    private bool _allowClose;
    private bool _syncPolicy;
    private bool _probing;
    private bool _closeAfterProbe;
    private readonly CancellationTokenSource _lifetime = new();

    public GuidedActivityForm(ToolIntegrationService service, IReadOnlyDictionary<string, ToolReadiness> tools, Func<string?> nativeBlock, string initialActivity = "questing")
    {
        _service = service;
        _tools = tools;
        _nativeBlock = nativeBlock;
        Text = "AutoWizard101 · Set up an activity";
        Size = new Size(940, 810);
        MinimumSize = new Size(850, 670);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        _activity.SetChoices("Follow my tracked quest", "Farm my current dungeon entrance", "Play pet dance", "Other minigames — not available yet");
        _combat.SetChoices("Damage cards", "Area damage first", "Custom combat strategy");
        _combat.SelectedIndex = 0;
        _resolution.SetChoices("1280x800", "800x600");
        _resolution.SelectedIndex = 0;
        foreach (var name in new[] { "Wizard City", "Krokotopia", "Marleybone", "Mooshu", "Dragonspyre" }) _locations.Items.Add(name);
        _locations.SetItemChecked(0, true);
        for (var index = 1; index <= 5; index++) _snacks.Items.Add("Snack position " + index);
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 4 };
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.Controls.Add(_heading);
        shell.Controls.Add(_page);
        shell.Controls.Add(_message);
        var actions = Flow();
        var load = Button("Load setup…");
        var save = Button("Save setup…");
        load.Click += (_, _) => TryAction(LoadSetup);
        save.Click += (_, _) => TryAction(SaveSetup);
        _back.Click += (_, _) => { if (!_started && !_busy) ShowStep(_step - 1); };
        _next.Click += async (_, _) => await NextAsync();
        _stop.Click += async (_, _) => await StopAsync();
        foreach (var button in new[] { load, save, _back, _next, _stop }) actions.Controls.Add(button);
        shell.Controls.Add(actions);
        Controls.Add(shell);
        BuildPages();
        _activity.SelectedIndex = initialActivity == "pet_dance" ? 2 : initialActivity == "farming" ? 1 : 0;
        _activity.SelectedIndexChanged += (_, _) => { _validated = false; _liveChecked = false; _signedIn.Checked = false; _prepared.Checked = false; _tracked.Checked = false; _petResources.Checked = false; if (_step == 0) ShowStep(0); };
        _resolution.SelectedIndexChanged += (_, _) =>
        {
            var canFeed = _resolution.SelectedValue == "1280x800";
            _snacks.Enabled = canFeed;
            if (!canFeed) for (var index = 0; index < 5; index++) _snacks.SetItemChecked(index, false);
        };
        _combat.SelectedIndexChanged += (_, _) => { _custom.Enabled = _combat.SelectedIndex == 2; _custom.Visible = _combat.SelectedIndex == 2; };
        _custom.Enabled = false;
        _custom.Visible = false;
        _allow.Checked = ExecutionPolicyManager.Current.AllowLiveAutomation && ExecutionPolicyManager.Current.Profile == ExecutionProfile.ExperimentalSimulation;
        _allow.CheckedChanged += async (_, _) =>
        {
            if (_syncPolicy) return;
            try
            {
                if (!_allow.Checked) await _service.StopGuidedAsync();
                ExecutionPolicyManager.SetExternalAutomationEnabled(_allow.Checked);
            }
            catch (Exception ex) { _message.Text = ex.Message; }
            finally
            {
                _syncPolicy = true;
                _allow.Checked = ExecutionPolicyManager.Current.AllowLiveAutomation && ExecutionPolicyManager.Current.Profile == ExecutionProfile.ExperimentalSimulation;
                _syncPolicy = false;
                UpdateState();
            }
        };
        foreach (var check in new[] { _signedIn, _prepared, _tracked, _petResources }) check.CheckedChanged += (_, _) => UpdateState();
        _timer.Tick += (_, _) => UpdateState();
        _timer.Start();
        FormClosing += async (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            if (_probing)
            {
                _closeAfterProbe = true;
                _message.Text = "Finishing the connection check and releasing the client before closing…";
                return;
            }
            if (_closing) return;
            _closing = true;
            _lifetime.Cancel();
            _timer.Stop();
            await _service.StopGuidedAsync();
            _timer.Dispose();
            _lifetime.Dispose();
            foreach (var page in _pages) page.Dispose();
            _allowClose = true;
            BeginInvoke(Close);
        };
        ThemeManager.ApplyTheme(this);
        ShowStep(0);
    }

    private static NumericUpDown Number(int min, int max, int value) => new() { Minimum = min, Maximum = max, Value = value, Width = 100 };
    private static CheckBox Check(string text, bool value = false) => new() { Text = text, AutoSize = true, Checked = value, MaximumSize = new Size(790, 0), Margin = new Padding(0, 8, 0, 8) };
    private static Button Button(string text) => new ToolCenterForm.ReadableActionButton { Text = text, AutoSize = true, Padding = new Padding(10, 5, 10, 5), Margin = new Padding(0, 8, 10, 4) };
    private static FlowLayoutPanel Flow() => new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Dock = DockStyle.Top };
    private static FlowLayoutPanel Page() => new() { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Top, Padding = new Padding(0, 18, 0, 12) };
    private static Label TextLabel(string text) => new() { Text = text, AutoSize = true, MaximumSize = new Size(790, 0), Margin = new Padding(0, 5, 0, 8) };
    private static void Row(FlowLayoutPanel page, string text, Control value)
    {
        var row = Flow();
        row.Controls.Add(new Label { Text = text, AutoSize = false, Width = 285, Height = 30, TextAlign = ContentAlignment.MiddleLeft });
        row.Controls.Add(value);
        page.Controls.Add(row);
    }

    private void BuildPages()
    {
        var choose = Page();
        choose.Controls.Add(TextLabel("Choose what you want the app to do. Guided runs use one signed-in wizard."));
        choose.Controls.Add(_activity);
        choose.Controls.Add(TextLabel("Questing follows the quest already tracked in-game. Farming starts at your current dungeon entrance. Pet dance uses visual arrow recognition."));
        choose.Controls.Add(TextLabel("Potion minigames, gardening and other games do not yet have verified activity adapters."));
        _pages.Add(choose);
        _pages.Add(Page());
        var checks = Page();
        checks.Controls.Add(_checks);
        checks.Controls.Add(_signedIn);
        checks.Controls.Add(_prepared);
        checks.Controls.Add(_tracked);
        checks.Controls.Add(_petResources);
        checks.Controls.Add(_allow);
        var open = Button("Open Wizard101");
        open.Click += (_, _) => { WizardLauncher.Launch(out var message); _message.Text = message; };
        checks.Controls.Add(open);
        var connection = Button("Check game connection");
        connection.Click += async (_, _) =>
        {
            if (_busy || _closing) return;
            _busy = true;
            _liveChecked = false;
            try
            {
                if (!_signedIn.Checked || ToolIntegrationService.GameClientCount() != 1) throw new InvalidOperationException("Sign in on one wizard and confirm it above first.");
                if (_nativeBlock() is { } block) throw new InvalidOperationException(block);
                _probing = true;
                _message.Text = "Checking the game connection. No activity routine is started by this check.";
                await _service.CheckActivityAsync(Profile(), _lifetime.Token, probe: true);
                _liveChecked = true;
                if (!_closing) _message.Text = "Connection checked. Confirm your preparation, then continue.";
            }
            catch (Exception ex) { if (!_closing) _message.Text = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message; }
            finally
            {
                _probing = false;
                _busy = false;
                UpdateState();
                if (_closeAfterProbe && !_closing) BeginInvoke(Close);
            }
        };
        checks.Controls.Add(connection);
        checks.Controls.Add(TextLabel("The setup check validates the adapter and its settings without running gameplay. Live game compatibility is confirmed only when the backend connects."));
        _pages.Add(checks);
        var review = Page();
        review.Controls.Add(_summary);
        review.Controls.Add(_progress);
        _pages.Add(review);
    }

    private void BuildOptions()
    {
        var page = (FlowLayoutPanel)_pages[1];
        page.Controls.Clear();
        Row(page, "Run label (for your log only)", _label);
        Row(page, "Stop after this many minutes", _minutes);
        if (_activity.SelectedIndex == 2)
        {
            Row(page, "Number of dance games", _games);
            Row(page, "Game resolution", _resolution);
            var choices = Flow();
            choices.Controls.Add(_locations);
            choices.Controls.Add(_snacks);
            page.Controls.Add(TextLabel("Dance locations and snack positions (no snack selection means no feeding):"));
            page.Controls.Add(choices);
            page.Controls.Add(TextLabel("Use windowed mode and HUGE UI size. At 800x600, snack feeding is unavailable. The run stops on focus loss or a stalled dance round."));
        }
        else
        {
            Row(page, "Combat preference", _combat);
            page.Controls.Add(_custom);
            Row(page, "Stop below health % outside battle", _health);
            Row(page, "Stop below mana % outside battle", _mana);
            page.Controls.Add(TextLabel("Use 0 to disable a resource threshold. Equip your own suitable deck; this setup does not build or change it."));
            page.Controls.Add(_potions);
            page.Controls.Add(_buy);
            if (_activity.SelectedIndex == 1) page.Controls.Add(_team);
        }
    }

    private ActivityProfile Profile() => new()
    {
        Activity = _activity.SelectedIndex switch { 0 => "questing", 1 => "farming", 2 => "pet_dance", _ => "other" },
        Label = _label.Text.Trim(),
        MaxMinutes = (int)_minutes.Value,
        CombatStyle = _combat.SelectedIndex switch { 0 => "damage", 1 => "aoe", _ => "custom" },
        CustomCombat = _custom.Text,
        MinHealthPercent = (int)_health.Value,
        MinManaPercent = (int)_mana.Value,
        UsePotions = _potions.Checked,
        BuyPotions = _buy.Checked,
        TeamUp = _activity.SelectedIndex == 1 && _team.Checked,
        Games = (int)_games.Value,
        Resolution = _resolution.SelectedValue,
        Locations = Enumerable.Range(0, 5).Select(_locations.GetItemChecked).ToArray(),
        Snacks = Enumerable.Range(0, 5).Select(_snacks.GetItemChecked).ToArray()
    };

    private void ShowStep(int step)
    {
        _step = Math.Clamp(step, 0, 3);
        _heading.Text = new[] { "1 · Choose an activity", "2 · Configure your run", "3 · Check prerequisites", "4 · Review and run" }[_step];
        if (_step == 1) BuildOptions();
        if (_step == 2)
        {
            _petResources.Visible = _activity.SelectedIndex == 2;
            _prepared.Text = _activity.SelectedIndex == 2 ? "The game is windowed, with HUGE UI size and the selected resolution." : "My wizard has a suitable combat deck equipped.";
            _tracked.Text = _activity.SelectedIndex switch
            {
                0 => "The quest I want to follow is tracked in-game.",
                1 => "I am standing at the intended dungeon entrance with a suitable quest tracked.",
                _ => "I am near the pet dance spiral with its menu still closed."
            };
        }
        if (_step == 3) _summary.Text = Profile().Summary();
        _page.Controls.Clear();
        _page.Controls.Add(_pages[_step]);
        ThemeManager.ApplyTheme(this);
        _message.Text = _activity.SelectedIndex == 3 ? "This activity is not available yet. Choose questing, dungeon farming, or pet dance." : "";
        UpdateState();
    }

    private async Task NextAsync()
    {
        if (_busy || _started) return;
        _busy = true;
        UpdateState();
        try
        {
            if (_step == 0) ShowStep(1);
            else if (_step == 1)
            {
                var profile = Profile();
                profile.Validate();
                _message.Text = "Checking the actual backend settings and assets…";
                await _service.CheckActivityAsync(profile, _lifetime.Token);
                if (_closing) return;
                _validated = true;
                _liveChecked = false;
                ShowStep(2);
            }
            else if (_step == 2) ShowStep(3);
            else
            {
                var profile = Profile();
                if (!_tools.TryGetValue(profile.Tool, out var readiness)) throw new InvalidOperationException("Close setup and Refresh status first.");
                await _service.LaunchGuidedAsync(profile, readiness, _nativeBlock(), _lifetime.Token);
                if (_closing) return;
                _started = true;
                _message.Text = "Your run is starting. Keep this window open for status and Stop.";
            }
        }
        catch (Exception ex) { if (!_closing) _message.Text = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message; }
        finally { _busy = false; UpdateState(); }
    }

    private bool Prepared() => _signedIn.Checked && _prepared.Checked && _tracked.Checked && (_activity.SelectedIndex != 2 || _petResources.Checked);

    private void UpdateState()
    {
        if (_closing) return;
        var count = ToolIntegrationService.GameClientCount();
        if (count != 1) _liveChecked = false;
        var policy = ExecutionPolicyManager.Current;
        var enabled = policy.AllowLiveAutomation && policy.Profile == ExecutionProfile.ExperimentalSimulation;
        _checks.Text = $"Adapter/settings: {(_validated ? "checked" : "not checked")}\nGame connection: {(_liveChecked ? "checked" : "check required")}\nWizard101 clients: {count} (exactly one required)\nGame controls: {(enabled ? "on" : "off")}\nOnly confirm the preparation items that are true in your game.";
        _back.Enabled = !_busy && !_started && _step > 0;
        _next.Text = _step == 3 ? "Start activity" : _step == 1 ? "Check setup" : "Next";
        _next.Enabled = !_busy && !_started && _activity.SelectedIndex != 3
            && (_step != 2 || (_validated && _liveChecked && Prepared()))
            && (_step != 3 || (_validated && _liveChecked && Prepared() && count == 1 && enabled));
        _stop.Enabled = _started && _service.GuidedActivity is { } session && _service.IsRunning(session.Tool);
        if (_step == 3 && !_started && (count != 1 || !enabled))
            _progress.Text = "To start: open exactly one signed-in game client and enable game controls on the previous step.";
        if (_started && _service.ReadActivityProgress() is { } progress)
        {
            var lines = new List<string> { $"{progress.Phase.ToUpperInvariant()} · {progress.Message}", $"Elapsed: {TimeSpan.FromSeconds(progress.ElapsedSeconds):mm\\:ss}" };
            if (progress.Zone != null) lines.Add("Zone: " + progress.Zone);
            if (progress.Health != null) lines.Add($"Health: {progress.Health}/{progress.MaxHealth} · Mana: {progress.Mana}/{progress.MaxMana}");
            if (progress.BattlesEnded != null) lines.Add($"Battles ended: {progress.BattlesEnded} (not a win count)");
            if (progress.GamesPlayed != null) lines.Add($"Dance games finished: {progress.GamesPlayed}/{progress.GamesRequested}");
            if (progress.DanceRound != null) lines.Add($"Dance rounds observed: {progress.DanceRound}/5");
            _progress.Text = string.Join(Environment.NewLine + Environment.NewLine, lines);
        }
    }

    private async Task StopAsync()
    {
        _message.Text = "Stopping the routine and releasing the backend…";
        await _service.StopGuidedAsync();
        _message.Text = "Stop requested. The status above shows when the backend closes.";
        UpdateState();
    }

    private void SaveSetup()
    {
        if (_started) throw new InvalidOperationException("This run already has its own saved setup. Close it before saving another setup.");
        var profile = Profile();
        profile.Validate();
        using var dialog = new SaveFileDialog { Filter = "Activity setup|*.json", DefaultExt = "json", FileName = profile.Activity + "-setup.json" };
        if (dialog.ShowDialog(this) == DialogResult.OK) { profile.Save(dialog.FileName); _message.Text = "Setup saved. Loading it later will ask you to confirm game preparation again."; }
    }

    private void LoadSetup()
    {
        if (_started) throw new InvalidOperationException("Stop and close this run before loading another setup.");
        using var dialog = new OpenFileDialog { Filter = "Activity setup|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        ApplyProfile(ActivityProfile.Load(dialog.FileName));
        ShowStep(1);
        _message.Text = "Setup loaded. Check it before starting.";
    }

    internal void ApplyProfile(ActivityProfile profile)
    {
        profile.Validate();
        _activity.SelectedIndex = profile.Activity == "pet_dance" ? 2 : profile.Activity == "farming" ? 1 : 0;
        _label.Text = profile.Label; _minutes.Value = profile.MaxMinutes;
        _combat.SelectedIndex = profile.CombatStyle == "custom" ? 2 : profile.CombatStyle == "aoe" ? 1 : 0;
        _custom.Text = profile.CustomCombat; _health.Value = profile.MinHealthPercent; _mana.Value = profile.MinManaPercent;
        _potions.Checked = profile.UsePotions; _buy.Checked = profile.BuyPotions; _team.Checked = profile.TeamUp;
        _games.Value = profile.Games; _resolution.SelectValue(profile.Resolution);
        for (var index = 0; index < 5; index++) { _locations.SetItemChecked(index, profile.Locations[index]); _snacks.SetItemChecked(index, profile.Snacks[index]); }
        _validated = false; _liveChecked = false; _signedIn.Checked = false; _prepared.Checked = false; _tracked.Checked = false; _petResources.Checked = false;
    }

    internal void PreviewStep(int step) { _validated = true; ShowStep(step); }

    private void TryAction(Action action)
    {
        if (_busy || _closing) return;
        try { action(); }
        catch (Exception ex) { _message.Text = ex.Message; }
    }

    private sealed class RadioChoices : FlowLayoutPanel
    {
        private readonly List<RadioButton> _buttons = new();
        private int _selected = -1;
        public event EventHandler? SelectedIndexChanged;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => _selected;
            set => _buttons[value].Checked = true;
        }
        public string SelectedValue => _selected >= 0 ? _buttons[_selected].Text : "";

        public RadioChoices()
        {
            AutoSize = true;
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
        }

        public void SetChoices(params string[] values)
        {
            foreach (var text in values)
            {
                var index = _buttons.Count;
                var button = new RadioButton { Text = text, AutoSize = true, Margin = new Padding(0, 2, 12, 5) };
                button.CheckedChanged += (_, _) =>
                {
                    if (!button.Checked) return;
                    _selected = index;
                    SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                };
                _buttons.Add(button);
                Controls.Add(button);
            }
        }

        public void SelectValue(string value)
        {
            SelectedIndex = _buttons.FindIndex(button => button.Text == value);
        }
    }
}
