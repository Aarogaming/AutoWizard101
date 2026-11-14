using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

internal sealed class RecordedCardReviewForm : Form
{
    internal RecordedCardReviewForm(JobCardSpec card, IReadOnlyList<JobFactDefinition> definitions, string gameId)
    {
        Text = "Test saved card · " + card.Title; Size = new(920, 620); StartPosition = FormStartPosition.CenterParent;
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 55 };
        var review = new Button { Text = "Open recorded teaching session…", AutoSize = true };
        var export = new Button { Text = "Save test report…", AutoSize = true, Enabled = false };
        var replay = new Button { Text = "Test action outcome separately…", AutoSize = true };
        var status = new Label { Dock = DockStyle.Bottom, Height = 70, Padding = new(10), Text = "Choose a recording to test requirements and outcomes." };
        var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        grid.Columns.Add("time", "Time (seconds)"); grid.Columns.Add("status", "Requirements"); grid.Columns.Add("reason", "Why");
        RecordedCardReport? report = null;
        review.Click += async (_, _) =>
        {
            using var file = new OpenFileDialog { Filter = "Teaching session (*.json)|*.json" };
            if (file.ShowDialog(this) != DialogResult.OK) return;
            review.Enabled = replay.Enabled = export.Enabled = false;
            try
            {
                report = await Task.Run(() => RecordedCardReview.Run(card, file.FileName, definitions, gameId));
                grid.Rows.Clear();
                foreach (var row in report.Decisions.Take(2000)) grid.Rows.Add((row.TimeMs / 1000).ToString("0.###"), row.Status, row.Reason);
                status.Text = $"{report.Decisions.Count(d => d.Status == "Eligible")} eligible / {report.Decisions.Length} sampled checks. Showing first 2,000. Full report can be saved.\nCustom · requirements replay only. No confirmed gameplay badge granted.";
                export.Enabled = true;
            }
            catch (Exception e) { report = null; status.Text = e.Message; }
            finally { review.Enabled = replay.Enabled = true; }
        };
        export.Click += (_, _) =>
        {
            using var file = new SaveFileDialog { Filter = "Test report (*.json)|*.json", FileName = "card-requirements-report.json" };
            if (report == null || file.ShowDialog(this) != DialogResult.OK) return;
            try { using var output = new FileStream(file.FileName, FileMode.CreateNew); JsonSerializer.Serialize(output, report, JobDeckRules.Json); }
            catch (Exception e) { status.Text = e.Message; }
        };
        replay.Click += async (_, _) =>
        {
            using var graph = new OpenFileDialog { Title = "Choose routine graph", Filter = "Routine (*.json)|*.json" };
            using var session = new OpenFileDialog { Title = "Choose recorded routine session", Filter = "Session (*.json)|*.json" };
            using var output = new SaveFileDialog { Title = "Save new replay report", Filter = "Report (*.json)|*.json", FileName = "card-action-replay.json" };
            if (graph.ShowDialog(this) != DialogResult.OK || session.ShowDialog(this) != DialogResult.OK || output.ShowDialog(this) != DialogResult.OK) return;
            review.Enabled = replay.Enabled = false;
            try
            {
                if (File.Exists(output.FileName)) throw new IOException("Choose a new report filename.");
                var complete = await Task.Run(() => RoutineReplay.Run(graph.FileName, session.FileName, output.FileName, card, definitions));
                status.Text = $"Recording test {(complete ? "passed" : "did not pass")}. Requirements were checked before each action. See the saved report for details.";
            }
            catch (Exception e) { status.Text = e.Message; }
            finally { review.Enabled = replay.Enabled = true; }
        };
        tools.Controls.AddRange([review, export, replay]); Controls.Add(grid); Controls.Add(tools); Controls.Add(status); WorkspaceTheme.Apply(this);
    }
}
