using System.Diagnostics;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Read-only run history with explicit cancellation; loading it never resumes a run.</summary>
internal sealed class OperationRunsView : UserControl
{
    private readonly DataGridView _runs = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
        BackgroundColor = SystemColors.Window,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
    };
    internal int EvidenceRows => _runs.Rows.Count;
    private readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Bottom, Padding = new(8) };
    public OperationRunsView(string dataRoot)
    {
        Dock = DockStyle.Fill; Padding = new(12);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        var refresh = new Button { Text = "Refresh runs", AutoSize = true };
        var stop = new Button { Text = "Stop selected run", AutoSize = true, Enabled = false };
        var folder = new Button { Text = "Open run evidence", AutoSize = true, Enabled = false };
        var recovery = new Button { Text = "Restore game controls…", AutoSize = true, AccessibleName = "Open game control recovery" };
        recovery.Click += (_, _) => { using var form = new InputRecoveryForm(); form.ShowDialog(this); RefreshRuns(); };
        actions.Controls.AddRange([refresh, stop, folder, recovery]);
        foreach (var column in new[] { ("UpdatedUtc", "Time", 145), ("Operation", "Routine", 190), ("State", "State", 100), ("Message", "Result", 430) })
            _runs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = column.Item1, HeaderText = column.Item2, Width = column.Item3, SortMode = DataGridViewColumnSortMode.NotSortable });
        _runs.Columns[^1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _runs.Columns[^1].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        Controls.Add(_runs); Controls.Add(actions); Controls.Add(_status);
        OperationReceipt? Selected() => _runs.SelectedRows.Count == 0 ? null : _runs.SelectedRows[0].Tag as OperationReceipt;
        void UpdateButtons() { var selected = Selected(); folder.Enabled = selected != null; stop.Enabled = selected?.State is OperationState.Queued or OperationState.Running; }
        _runs.SelectionChanged += (_, _) => UpdateButtons();
        void RefreshRuns()
        {
            try
            {
                var rows = OperationScheduler.History(dataRoot);
                _runs.Rows.Clear();
                foreach (var row in rows) _runs.Rows[_runs.Rows.Add(row.UpdatedUtc.ToLocalTime().ToString("g"), Title(row.Request.Operation), row.State.ToString(), row.Message)].Tag = row;
                UpdateButtons();
                _status.Text = $"{rows.Length} recent runs · interrupted runs never restart automatically. Refresh to see progress.";
            }
            catch (Exception error) { _status.Text = "Could not read run history: " + error.Message; }
        }
        refresh.Click += (_, _) => RefreshRuns();
        VisibleChanged += (_, _) => { if (Visible) RefreshRuns(); };
        stop.Click += (_, _) =>
        {
            try
            {
                var row = Selected(); if (row == null) return;
                _status.Text = ApplicationOperations.For(dataRoot).Cancel(row.Request.Id) ? "Stop requested. The owner is retained until cleanup finishes; refresh for the result." : "This run is no longer active in this application.";
            }
            catch (Exception error) { _status.Text = error.Message; }
        };
        folder.Click += (_, _) =>
        {
            try
            {
                var row = Selected(); if (row == null || !Guid.TryParseExact(row.Request.Id, "N", out _)) return;
                var path = Path.Combine(dataRoot, "operations", row.Request.Id);
                if (Directory.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception error) { _status.Text = error.Message; }
        };
        RefreshRuns();
    }
    private static string Title(string id) => id switch
    {
        "w101.pet-dance-review" => "Pet Dance teacher review",
        "w101.auto-idle" => "Auto-Idle",
        "legacy.dance" => "Legacy Pet Dance teacher",
        "legacy.deimos" => "Guided Wizard101 activity",
        "legacy.input-batch" => "Legacy input routine",
        "legacy.script" or "legacy.ScriptRun" => "Legacy script",
        "legacy.DesignCapture" => "Design capture",
        "legacy.Inputs" => "Legacy navigation",
        _ => id
    };
}
