using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

internal sealed class ToolCenterForm : Form
{
    private readonly ToolIntegrationService _service;
    private readonly Func<string?> _nativeBlockReason;
    private readonly Dictionary<string, ToolReadiness> _readiness = new();
    private readonly Dictionary<string, Label> _labels = new();
    private readonly Dictionary<string, Button> _startButtons = new();
    private readonly Dictionary<string, Button> _stopButtons = new();
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(920, 0), Margin = new Padding(0, 12, 0, 8), Text = "Checking your tools…" };
    private readonly TextBox _details = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, WordWrap = false };
    private readonly ListView _inventory = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
    private readonly CheckBox _allowControls = new() { Text = "Allow game controls", AutoSize = true };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private bool _busy;
    private bool _closing;
    private bool _updatingPolicy;
    private bool _awaitingStop;
    private bool _allowClose;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };

    public ToolCenterForm(Func<string?>? nativeBlockReason = null, ToolIntegrationService? service = null, bool checkOnShow = true)
    {
        _nativeBlockReason = nativeBlockReason ?? (() => null);
        _service = service ?? new ToolIntegrationService();
        Text = "AutoWizard101 · Tools";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 680);
        Size = new Size(1040, 820);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 6 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "Your Wizard101 tools", Font = new Font(Font.FontFamily, 22, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 8) });
        layout.Controls.Add(new Label { Text = "Plan your play, train pets, and keep your tools in one place.", AutoSize = true, Margin = new Padding(0, 0, 0, 16) });
        var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0) };
        toolbar.Controls.Add(ActionButton("Refresh status", async () => await RefreshToolsAsync()));
        toolbar.Controls.Add(ActionButton("Choose tools folder", ChooseFolderAsync));
        toolbar.Controls.Add(ActionButton("Update boss library", UpdateLibraryAsync));
        toolbar.Controls.Add(ActionButton("Open Wizard101", () => { WizardLauncher.Launch(out var message); _status.Text = message; return Task.CompletedTask; }));
        toolbar.Controls.Add(ActionButton("Choose activity…", () => OpenGuidedAsync("questing")));
        toolbar.Controls.Add(ActionButton("Diego observer…", () => _service.LaunchObserverAsync()));
        var stopAll = new Button { Text = "Stop all tools", AutoSize = true, Padding = new Padding(10, 5, 10, 5) };
        stopAll.Click += async (_, _) => { await _service.StopGuidedAsync(); _service.StopAll(); _awaitingStop = true; _status.Text = "Stopping your open tools…"; UpdateRunningState(); };
        toolbar.Controls.Add(stopAll);
        layout.Controls.Add(toolbar);
        var policyRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 12) };
        _allowControls.Checked = ExecutionPolicyManager.Current.AllowLiveAutomation && ExecutionPolicyManager.Current.Profile == ExecutionProfile.ExperimentalSimulation;
        _allowControls.CheckedChanged += AllowControlsChanged;
        policyRow.Controls.Add(_allowControls);
        policyRow.Controls.Add(new Label { Text = "Native routines use visible screens and ordinary keys/clicks. This switch does not enable legacy executables or client hooks.", AutoSize = true, MaximumSize = new Size(650, 0), Margin = new Padding(12, 3, 0, 0) });
        layout.Controls.Add(policyRow);
        var tabs = _tabs;
        var play = new TabPage("Play & plan") { Padding = new Padding(12), AutoScroll = true };
        var cards = new TableLayoutPanel { Dock = DockStyle.Top, Height = 450, ColumnCount = 1, RowCount = 3 };
        foreach (var _ in Enumerable.Range(0, 3)) cards.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        cards.Controls.Add(CreateCard("companion", "Boss guides and planning", "Look up bosses, track quests, and plan your gear and decks.", "Open companion"));
        cards.Controls.Add(CreateCard("dance", "Pet training", "Choose game count, locations and snacks, then check your game settings.", "Set up pet dance"));
        cards.Controls.Add(CreateCard("deimos", "Questing and farming", "Choose questing or dungeon farming, combat preferences and run limits.", "Set up an activity"));
        play.Controls.Add(cards);
        tabs.TabPages.Add(play);
        var maintenance = new TabPage("Maintenance") { Padding = new Padding(12) };
        var tasks = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        tasks.Controls.Add(new Label { Text = "Compare saved game updates and check client data.", AutoSize = true, Margin = new Padding(0, 0, 0, 12) });
        tasks.Controls.Add(ActionButton("Compare patch files…", () => CompareFilesAsync(true)));
        tasks.Controls.Add(ActionButton("Compare saved type dumps…", () => CompareFilesAsync(false)));
        tasks.Controls.Add(ActionButton("Save client type dump…", SaveClientTypesAsync));
        tasks.Controls.Add(ActionButton("Decode a saved capture…", DecodeCaptureAsync));
        tasks.Controls.Add(new Label { Text = "These tools help investigate game updates. They do not automatically repair compatibility.", AutoSize = true, MaximumSize = new Size(700, 0), Margin = new Padding(0, 16, 0, 0) });
        maintenance.Controls.Add(tasks);
        tabs.TabPages.Add(maintenance);
        var details = new TabPage("Setup & details") { Padding = new Padding(12) };
        var detailLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        _inventory.Columns.Add("Tool", 150);
        _inventory.Columns.Add("Setup status", 530);
        detailLayout.Controls.Add(_inventory);
        detailLayout.Controls.Add(new Label { Text = "wizSDK has known movement gaps. Libraries support other tools and do not open on their own.", AutoSize = true, MaximumSize = new Size(860, 0), Margin = new Padding(0, 8, 0, 8) });
        detailLayout.Controls.Add(_details);
        details.Controls.Add(detailLayout);
        tabs.TabPages.Add(details);
        layout.Controls.Add(tabs);
        layout.Controls.Add(_status);
        Controls.Add(layout);
        ThemeManager.ApplyTheme(this);
        if (checkOnShow) Shown += async (_, _) => await RunActionAsync(RefreshToolsAsync);
        _timer.Tick += (_, _) => UpdateRunningState();
        _timer.Start();
        FormClosing += async (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            if (_closing) return;
            _closing = true;
            _timer.Stop();
            await _service.StopGuidedAsync();
            _timer.Dispose();
            _service.Dispose();
            _allowClose = true;
            BeginInvoke(Close);
        };
    }

    private Button ActionButton(string text, Func<Task> action)
    {
        var button = new ReadableActionButton { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 5, 10, 5), Margin = new Padding(0, 0, 10, 6) };
        button.Click += async (_, _) => await RunActionAsync(action);
        return button;
    }

    private Control CreateCard(string tool, string title, string description, string action)
    {
        var card = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(14), Margin = new Padding(0, 0, 0, 10) };
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(new Label { Text = description, AutoSize = true, MaximumSize = new Size(830, 0), Margin = new Padding(0, 0, 0, 6) });
        var status = new Label { Text = "Checking setup…", AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
        _labels[tool] = status;
        content.Controls.Add(status);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var start = ActionButton(action, () => LaunchAsync(tool));
        start.Enabled = false;
        var stop = ActionButton("Stop", () => { _service.Stop(tool); _awaitingStop = true; _status.Text = "Stopping the tool…"; return Task.CompletedTask; });
        stop.Enabled = false;
        _startButtons[tool] = start;
        _stopButtons[tool] = stop;
        actions.Controls.Add(start);
        actions.Controls.Add(stop);
        content.Controls.Add(actions);
        card.Controls.Add(content);
        return card;
    }

    private async Task RunActionAsync(Func<Task> action)
    {
        if (_busy || _closing) return;
        _busy = true;
        UseWaitCursor = true;
        try { await action(); }
        catch (OperationCanceledException)
        {
            if (!_closing) _status.Text = "Action stopped.";
        }
        catch (Exception ex)
        {
            if (!_closing)
            {
                _status.Text = ex.Message.Length < 220 && !ex.Message.Contains('\n') ? ex.Message : "This action couldn't finish. See Setup & details for more information.";
                _details.Text = ex.Message;
            }
        }
        finally
        {
            _busy = false;
            if (!_closing) { UseWaitCursor = false; UpdateRunningState(); }
        }
    }

    private async Task RefreshToolsAsync()
    {
        _status.Text = "Checking your installed tools…";
        var states = await _service.CheckAsync();
        if (_closing) return;
        ApplyReadiness(states);
    }

    internal void ApplyReadiness(IReadOnlyList<ToolReadiness> states)
    {
        _readiness.Clear();
        _inventory.Items.Clear();
        foreach (var state in states)
        {
            _readiness[state.Tool] = state;
            var description = state.Tool is "wizsdk" or "wizwalker" or "wizdiff"
                ? (state.Installed ? "Installed supporting library" : "Needs setup") : state.DisplayStatus;
            if (state.Tool == "wizsdk" && state.Installed) description = "Installed · movement adaptation needed";
            _inventory.Items.Add(new ListViewItem(new[] { state.Tool, description }));
        }
        _status.Text = "Setup checked. Open a tool above, or update your boss library.";
        UpdateRunningState();
    }

    internal void SelectPreviewPage(int index) => _tabs.SelectedIndex = index;

    private void UpdateRunningState()
    {
        foreach (var tool in _labels.Keys)
        {
            var running = _service.IsRunning(tool);
            var installed = _readiness.TryGetValue(tool, out var state) && state.Installed;
            var gameplay = tool is "dance" or "deimos";
            var allowed = !gameplay || _allowControls.Checked;
            var boundary = W101InteractionPolicy.ToolBlockReason(tool);
            _labels[tool].Text = running ? "Open · use Stop to close this tool"
                : boundary != null ? boundary
                : _service.LastExitCode(tool) is int code && code != 0 ? "Closed with an error · see Setup & details"
                : installed && !allowed ? "Installed · turn on Allow game controls to begin"
                : installed && tool == "companion" ? "Installed · guides available; OCR is optional"
                : state?.DisplayStatus ?? "Choose your tools folder, then Refresh status";
            _startButtons[tool].Enabled = !_busy && installed && !running && boundary == null;
            _stopButtons[tool].Enabled = running;
            _stopButtons[tool].Visible = running;
        }
        if (_service.Messages.Length > 0 && !_details.Focused) _details.Text = _service.Messages;
        if (_awaitingStop && !_service.HasOpenTools)
        {
            _awaitingStop = false;
            _status.Text = "Your tools are closed.";
        }
    }

    private async Task LaunchAsync(string tool)
    {
        if (tool is "dance" or "deimos")
        {
            await OpenGuidedAsync(tool == "dance" ? "pet_dance" : "questing");
            return;
        }
        if (!_readiness.TryGetValue(tool, out var state)) throw new InvalidOperationException("Refresh tool status first.");
        await _service.LaunchAsync(tool, state, tool is "deimos" or "dance" ? _nativeBlockReason() : null);
        _status.Text = "Opening the tool. Keep this window open so you can stop it here.";
    }

    private async Task OpenGuidedAsync(string activity)
    {
        if (_readiness.Count == 0) await RefreshToolsAsync();
        using var setup = new GuidedActivityForm(_service, _readiness, _nativeBlockReason, activity);
        setup.ShowDialog(this);
        _updatingPolicy = true;
        _allowControls.Checked = ExecutionPolicyManager.Current.AllowLiveAutomation && ExecutionPolicyManager.Current.Profile == ExecutionProfile.ExperimentalSimulation;
        _updatingPolicy = false;
        UpdateRunningState();
    }

    private async Task ChooseFolderAsync()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose your AutoWizard101 tools folder", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _service.SelectWorkspace(dialog.SelectedPath);
        await RefreshToolsAsync();
    }

    private async Task UpdateLibraryAsync()
    {
        var count = await _service.UpdateBossLibraryAsync();
        if (!_closing) _status.Text = $"Boss library updated: {count:N0} entries. Your existing primary library is preserved.";
    }

    private void AllowControlsChanged(object? sender, EventArgs e)
    {
        if (_updatingPolicy) return;
        try
        {
            if (!_allowControls.Checked) _service.StopGameplay();
            ExecutionPolicyManager.SetExternalAutomationEnabled(_allowControls.Checked);
            _status.Text = _allowControls.Checked ? "Game controls enabled. Open Wizard101 and sign in before starting a gameplay tool." : "Game controls off. Guides and offline maintenance remain available.";
            UpdateRunningState();
        }
        catch (Exception ex)
        {
            _updatingPolicy = true;
            _allowControls.Checked = ExecutionPolicyManager.Current.AllowLiveAutomation;
            _updatingPolicy = false;
            _status.Text = "Could not update game controls. See Setup & details.";
            _details.Text = ex.Message;
        }
    }

    private string? ChooseInput(string title, string filter)
    {
        using var dialog = new OpenFileDialog { Title = title, Filter = filter };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }

    private string? ChooseOutput(string title)
    {
        using var dialog = new SaveFileDialog { Title = title, Filter = "JSON file|*.json", DefaultExt = "json", OverwritePrompt = false };
        if (dialog.ShowDialog(this) != DialogResult.OK) return null;
        if (File.Exists(dialog.FileName)) throw new InvalidOperationException("Choose a new file name so the existing file is preserved.");
        return dialog.FileName;
    }

    private async Task CompareFilesAsync(bool patches)
    {
        var filter = patches ? "Patch XML|*.xml" : "Type dump JSON|*.json";
        var before = ChooseInput("Choose the older file", filter);
        if (before == null) return;
        var after = ChooseInput("Choose the newer file", filter);
        if (after == null) return;
        var output = ChooseOutput("Save the comparison report");
        if (output == null) return;
        await _service.CompareAsync(patches, before, after, output);
        _status.Text = "Comparison saved. Open the report from your chosen folder.";
    }

    private async Task SaveClientTypesAsync()
    {
        W101InteractionPolicy.RequireTool("wiztype");
        var output = ChooseOutput("Save client types");
        if (output == null) return;
        if (!_readiness.TryGetValue("wiztype", out var state)) throw new InvalidOperationException("Refresh tool status first.");
        await _service.LaunchAsync("wiztype", state, output: output);
        _status.Text = "Saving client types. Progress appears in Setup & details.";
    }

    private async Task DecodeCaptureAsync()
    {
        using var definitions = new FolderBrowserDialog { Description = "Choose the message definitions folder", UseDescriptionForTitle = true };
        if (definitions.ShowDialog(this) != DialogResult.OK) return;
        var capture = ChooseInput("Choose a saved capture", "Packet capture|*.pcap;*.pcapng");
        if (capture == null) return;
        var output = ChooseOutput("Save decoded messages");
        if (output == null) return;
        await _service.DecodeAsync(definitions.SelectedPath, capture, output);
        _status.Text = "Decoded messages saved.";
    }

    internal sealed class ReadableActionButton : Button
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Enabled) return;
            var area = ClientRectangle;
            area.Inflate(-2, -2);
            using var background = new SolidBrush(BackColor);
            e.Graphics.FillRectangle(background, area);
            var foreground = BackColor.GetBrightness() < 0.5f ? Color.FromArgb(170, 180, 200) : Color.DimGray;
            TextRenderer.DrawText(e.Graphics, Text, Font, area, foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}
