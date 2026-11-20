using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

internal sealed class GameplaySamplerForm : Form
{
    public GameplaySamplePlan? Plan { get; private set; }
    public GameplaySamplerForm(VisualStateRecognizer? recognizer, GameResearch? research = null)
    {
        Text = "Gameplay Sampler";
        ClientSize = new Size(650, 420);
        Font = new Font("Segoe UI", 11);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        body.Controls.Add(new Label { Text = "Sample → Label → Learn → Check", AutoSize = true, Font = new Font(Font.FontFamily, 18, FontStyle.Bold) });
        body.Controls.Add(new Label { Text = "You play; Maelstrom observes the selected game window.\nCapture different states, transitions and mistakes.", AutoSize = true, Margin = new Padding(0, 12, 0, 12) });
        var teaching = new RadioButton { Text = "Teaching examples — build a draft pack", Checked = true, AutoSize = true };
        var check = new RadioButton { Text = "Independent check — test the current recognizer", Enabled = recognizer != null, AutoSize = true };
        body.Controls.Add(teaching);
        body.Controls.Add(check);
        body.Controls.Add(new Label { Text = "What are we trying to recognize?", AutoSize = true, Margin = new Padding(0, 12, 0, 4) });
        var goal = new TextBox { Width = 580, MaxLength = 240, Text = research == null ? "Distinguish tutorial, playing, and round finished" : research.Hypothesis[..Math.Min(240, research.Hypothesis.Length)] };
        body.Controls.Add(goal);
        body.Controls.Add(new Label { Text = "1 frame/second · up to 600 frames / 1 GiB · no input or audio\nFor a check, play a new round. Label what you see before viewing results.", AutoSize = true, Margin = new Padding(0, 10, 0, 10) });
        var start = new Button { Text = "Start sampling", AutoSize = true };
        start.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(goal.Text)) { goal.Focus(); return; }
            Plan = new(check.Checked ? "check" : "teaching", goal.Text.Trim(), check.Checked ? recognizer?.PackHash : null, check.Checked ? recognizer?.PackName : null, research);
            DialogResult = DialogResult.OK;
        };
        body.Controls.Add(start);
        Controls.Add(body);
        AcceptButton = start;
    }
}
