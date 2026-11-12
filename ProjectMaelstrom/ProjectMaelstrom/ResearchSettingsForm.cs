using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

internal sealed class ResearchSettingsForm : Form
{
    public ResearchSettingsForm(string dataRoot)
    {
        Text = "Maelstrom · Settings";
        ClientSize = new Size(620, 390);
        MinimumSize = new Size(570, 370);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        body.Controls.Add(new Label { Text = "Settings", Font = new Font(Font.FontFamily, 20, FontStyle.Bold), AutoSize = true });
        body.Controls.Add(new Label { Text = "Choose which research suggestions appear. Changes apply when you next open Game Research.", AutoSize = true, MaximumSize = new Size(550, 0), Margin = new Padding(0, 12, 0, 12) });
        var controls = new CheckBox { Text = "Suggest controls and hotkeys from guides", AutoSize = true };
        var tips = new CheckBox { Text = "Suggest tips and hidden-item clues from guides", AutoSize = true };
        var sound = new CheckBox { Text = "Play a sound when the game-action countdown starts", AutoSize = true };
        try { var current = AppPreferences.Load(dataRoot); controls.Checked = current.ControlSuggestions; tips.Checked = current.TipsAndHiddenItems; sound.Checked = current.ActionCountdownSound; }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not load settings: " + ex.Message + "\nYour existing settings were not changed.", "Settings");
            controls.Enabled = tips.Enabled = sound.Enabled = false;
        }
        body.Controls.Add(controls);
        body.Controls.Add(tips);
        body.Controls.Add(sound);
        body.Controls.Add(new Label { Text = "W101 shared motion and Idle routines show a five-second countdown. Mouse or keyboard activity cancels the start. Sound does not change your volume.", AutoSize = true, MaximumSize = new Size(550, 0) });
        body.Controls.Add(new Label { Text = "Suggestions are unverified source excerpts. These options do not enable game control or automatic research.", AutoSize = true, MaximumSize = new Size(550, 0), Margin = new Padding(0, 14, 0, 14) });
        var save = new Button { Text = "Save settings", AutoSize = true, Enabled = controls.Enabled };
        save.Click += (_, _) =>
        {
            try { (AppPreferences.Load(dataRoot) with { ControlSuggestions = controls.Checked, TipsAndHiddenItems = tips.Checked, ActionCountdownSound = sound.Checked }).Save(dataRoot); DialogResult = DialogResult.OK; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save settings"); }
        };
        body.Controls.Add(save);
        var signIn = new Button { Text = "Game sign-in…", AutoSize = true, AccessibleName = "Manage saved game sign-in", Margin = new Padding(0, 18, 0, 8) };
        signIn.Click += (_, _) => { using var form = new GameSignInForm(); form.ShowDialog(this); };
        body.Controls.Add(signIn);
        var recovery = new Button { Text = "Restore game controls…", AutoSize = true, AccessibleName = "Open game control recovery", Margin = new Padding(0, 18, 0, 8) };
        recovery.Click += (_, _) => { using var form = new InputRecoveryForm(); form.ShowDialog(this); };
        body.Controls.Add(recovery);
        Controls.Add(body);
    }
}
