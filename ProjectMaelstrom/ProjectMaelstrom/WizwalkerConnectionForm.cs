using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Optional, one-shot client observation. No automatic connection or polling.</summary>
internal sealed class WizwalkerConnectionForm : Form
{
    private readonly ComboBox _windows = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 540 };
    private readonly CheckBox _enabled = new() { Text = "Enable client observation for this check", AutoSize = true };
    private readonly Button _check = new() { Text = "Check loading state", AutoSize = true, Enabled = false };
    private readonly Button _stop = new() { Text = "Stop", AutoSize = true, Enabled = false };
    private readonly Label _status = new() { Text = "Off. No client is connected.", AutoSize = true, MaximumSize = new Size(560, 0) };
    private OperationTicket? _ticket;
    private bool _closing;
    private string? _workspace;

    internal WizwalkerConnectionForm(string dataRoot, GameWindow? selected = null)
    {
        Text = "Maelstrom · Wizard101 client observation";
        Size = new Size(640, 420); MinimumSize = new Size(620, 380); StartPosition = FormStartPosition.CenterParent;
        using var tools = new ToolIntegrationService(); _workspace = tools.Workspace;
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20) };
        body.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(560, 0), Text = "Optional client observation · experimental\nRead one loading-state snapshot from the selected Wizard101 client, then disconnect. No movement, gameplay hooks or network operations are used. Visual Observe remains available." });
        body.Controls.Add(_windows);
        var refresh = new Button { Text = "Refresh windows", AutoSize = true };
        var locate = new Button { Text = "Locate adapter files…", AutoSize = true };
        body.Controls.Add(refresh); body.Controls.Add(locate); body.Controls.Add(_enabled);
        var actions = new FlowLayoutPanel { AutoSize = true }; actions.Controls.AddRange([_check, _stop]); body.Controls.Add(actions); body.Controls.Add(_status);
        Controls.Add(body); WorkspaceTheme.Apply(this);
        void RefreshWindows()
        {
            if (_ticket is not null) return;
            _windows.Items.Clear();
            foreach (var window in GameWindows.List().Where(w => w.ProcessName == "WizardGraphicalClient")) _windows.Items.Add(window);
            if (selected is not null)
                foreach (GameWindow window in _windows.Items)
                    if (GameWindows.SameIdentity(selected, window)) { _windows.SelectedItem = window; break; }
            // No automatic selection/attachment when several clients are present.
        }
        Shown += (_, _) => RefreshWindows(); refresh.Click += (_, _) => RefreshWindows();
        locate.Click += (_, _) =>
        {
            if (_ticket is not null) return;
            using var picker = new FolderBrowserDialog { Description = "Choose the AutoWizard101 folder containing the optional adapter." };
            if (picker.ShowDialog(this) == DialogResult.OK)
            {
                if (!ToolIntegrationService.IsWorkspace(picker.SelectedPath)) { _status.Text = "That folder does not contain the adapter tools."; return; }
                _workspace = picker.SelectedPath; _status.Text = "Adapter folder selected. Nothing is connected.";
            }
        };
        _enabled.CheckedChanged += (_, _) => { if (!_enabled.Checked) _ticket?.Cancel(); _check.Enabled = _enabled.Checked && _ticket is null; };
        _stop.Click += (_, _) => { _ticket?.Cancel(); _status.Text = "Stopping and waiting for disconnect…"; };
        _check.Click += async (_, _) =>
        {
            if (_ticket is not null || !_enabled.Checked) return;
            _check.Enabled = false;
            try
            {
                var selectedWindow = _windows.SelectedItem as GameWindow ?? throw new InvalidOperationException("Select a Wizard101 window first.");
                var workspace = _workspace ?? throw new InvalidOperationException("Locate the optional adapter files first.");
                // Snapshot target and preferences before queuing; later UI changes cannot retarget it.
                var target = WizwalkerTarget.FromWindow(selectedWindow);
                _ticket = WizwalkerOperation.Submit(ApplicationOperations.For(dataRoot), new(target, Enabled: true), WizwalkerLaunch.FromWorkspace(workspace));
                _stop.Enabled = true; refresh.Enabled = false; locate.Enabled = false; _windows.Enabled = false;
                _status.Text = "Queued for one client check. Other routines retain their control ownership.";
                var result = await _ticket.Completion;
                if (!IsDisposed) _status.Text = result.State + " · " + result.Message + " Evidence is available in Routine runs.";
            }
            catch (Exception error) { if (!IsDisposed) _status.Text = error.Message; }
            finally
            {
                _ticket = null;
                if (!IsDisposed) { _stop.Enabled = false; _enabled.Checked = false; refresh.Enabled = true; locate.Enabled = true; _windows.Enabled = true; }
            }
        };
        FormClosing += async (_, args) =>
        {
            if (_closing || _ticket is null) return;
            args.Cancel = true; _closing = true;
            var ticket = _ticket; ticket.Cancel();
            _status.Text = "Waiting for the client observer to disconnect…";
            try { await ticket.Completion; }
            catch { /* The scheduler records failures and retains recovery state. */ }
            if (!IsDisposed) Close();
        };
    }
}
