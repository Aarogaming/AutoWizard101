using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Explicit recovery workflow. Opening the dialog starts no checks or
/// input. Closing waits for pending work before releasing its admission hold.</summary>
internal sealed class InputRecoveryForm : Form
{
    private readonly IInputRecoveryWorkflow _workflow;
    private readonly CancellationTokenSource _closing = new();
    private readonly Label _heading = new() { AutoSize = true, MaximumSize = new(590, 0), Font = new("Segoe UI", 20, FontStyle.Bold) };
    private readonly Label _message = new() { AutoSize = true, MaximumSize = new(590, 0), AccessibleName = "Recovery status", Margin = new(0, 16, 0, 16) };
    private readonly Label _steps = new() { AutoSize = true, MaximumSize = new(590, 0), AccessibleName = "Recovery steps", Margin = new(0, 8, 0, 18) };
    private readonly Button _stop = new() { Text = "Stop routines", AutoSize = true, AccessibleName = "Stop routines for recovery" };
    private readonly Button _check = new() { Text = "Check cleanup", AutoSize = true, AccessibleName = "Check control cleanup" };
    private readonly Button _restore = new() { Text = "Restore controls", AutoSize = true, AccessibleName = "Restore game controls after verification" };
    private readonly Button _close = new() { Text = "Close", AutoSize = true, AccessibleName = "Close recovery" };
    private bool _busy, _closeRequested, _workflowDisposed, _failed;

    public InputRecoveryForm() : this(new InputRecoveryOperation()) { }

    internal InputRecoveryForm(IInputRecoveryWorkflow workflow)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        Text = "Maelstrom · Restore game controls";
        ClientSize = new(650, 420); MinimumSize = new(590, 390);
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi;
        Font = new("Segoe UI", 10);
        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new(24), AutoScroll = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false
        };
        body.Controls.AddRange([_heading, _message, _steps]);
        var actions = new FlowLayoutPanel { AutoSize = true, MaximumSize = new(590, 0), WrapContents = true };
        actions.Controls.AddRange([_stop, _check, _restore]); body.Controls.Add(actions);
        body.Controls.Add(new Label
        {
            Text = "Checks do not play the game. Observe and saved recordings remain available. Restoring controls does not start a routine.",
            AutoSize = true, MaximumSize = new(590, 0), Margin = new(0, 20, 0, 20)
        });
        body.Controls.Add(_close); Controls.Add(body);
        _stop.Click += async (_, _) => await StopRoutinesAsync();
        _check.Click += async (_, _) => await CheckCleanupAsync();
        _restore.Click += async (_, _) => await RestoreControlsAsync();
        _close.Click += (_, _) => Close();
        CancelButton = _close;
        WorkspaceTheme.Apply(this); _heading.ForeColor = WorkspaceTheme.Palette.Accent;
        RefreshView();
    }

    internal Task StopRoutinesAsync() => RunAsync(0);
    internal Task CheckCleanupAsync() => RunAsync(1);
    internal Task RestoreControlsAsync() => RunAsync(2);

    private async Task RunAsync(int step)
    {
        if (_busy || _closeRequested || IsDisposed) return;
        var display = InputRecoveryPresentation.From(_workflow.State, failed: _failed);
        if (!(step == 0 ? display.CanStop : step == 1 ? display.CanCheck : display.CanRestore)) return;
        _busy = true; _failed = false; RefreshView();
        try
        {
            var pending = step switch
            {
                0 => _workflow.StopAsync(_closing.Token),
                1 => _workflow.CheckAsync(_closing.Token),
                _ => _workflow.RestoreAsync(_closing.Token)
            };
            RefreshView();
            await pending;
        }
        catch (OperationCanceledException) { if (!_closeRequested) _failed = true; }
        catch (Exception) { _failed = true; }
        finally
        {
            _busy = false;
            if (_closeRequested)
            {
                DisposeWorkflow();
                if (!IsDisposed) Close();
            }
            else if (!IsDisposed) RefreshView();
        }
    }

    private void RefreshView()
    {
        var display = InputRecoveryPresentation.From(_workflow.State, _busy, _closeRequested, _failed);
        _heading.Text = display.Heading; _message.Text = display.Message; _steps.Text = display.Steps;
        _stop.Enabled = display.CanStop; _check.Enabled = display.CanCheck; _restore.Enabled = display.CanRestore;
        _close.Text = _closeRequested ? "Finishing…" : "Close"; _close.Enabled = !_closeRequested;
        UseWaitCursor = _busy;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
        {
            _closeRequested = true; e.Cancel = true;
            try { _closing.Cancel(); } catch (Exception) { _failed = true; }
            RefreshView();
        }
        base.OnFormClosing(e);
    }

    private void DisposeWorkflow()
    {
        if (_workflowDisposed) return;
        _workflowDisposed = true;
        try { _workflow.Dispose(); }
        finally { _closing.Dispose(); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _closeRequested = true;
            if (!_workflowDisposed)
            {
                try { _closing.Cancel(); } catch (Exception) { _failed = true; }
                if (!_busy) DisposeWorkflow();
            }
        }
        base.Dispose(disposing);
    }
}
