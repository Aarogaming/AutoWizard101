using Microsoft.Win32;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

public class PetDanceBot : Form
{
    private readonly Button _launchButton;
    private readonly Label _statusLabel;

    public PetDanceBot()
    {
        Text = "Wizard101 DanceBot";
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        Dock = DockStyle.Fill;
        ClientSize = new Size(360, 160);
        MinimumSize = new Size(360, 160);

        _launchButton = new Button
        {
            Text = "Open pet training tools",
            Size = new Size(320, 40),
            Location = new Point(20, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _launchButton.Click += LaunchButton_Click;

        _statusLabel = new Label
        {
            AutoSize = true,
            Location = new Point(20, 80),
            Text = "Choose Pet training in the Tools window."
        };

        Controls.Add(_launchButton);
        Controls.Add(_statusLabel);

        ThemeManager.ApplyTheme(this);
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        FormClosing += PetDanceBot_FormClosing;
    }

    private void LaunchButton_Click(object? sender, EventArgs e)
    {
        using var tools = new ToolCenterForm();
        tools.ShowDialog(this);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ThemeManager.ApplyTheme(this)));
        }
        else
        {
            ThemeManager.ApplyTheme(this);
        }
    }

    private void PetDanceBot_FormClosing(object? sender, FormClosingEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        FormClosing -= PetDanceBot_FormClosing;
    }
}
