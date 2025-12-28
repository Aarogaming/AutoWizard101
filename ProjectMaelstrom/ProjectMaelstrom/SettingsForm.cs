using System.Linq;
using ProjectMaelstrom.Utilities;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace ProjectMaelstrom;

public partial class SettingsForm : Form
{
    public SettingsForm()
    {
        InitializeComponent();
    }

    private void SettingsForm_Load(object sender, EventArgs e)
    {
        ocrSpaceApiKey.Text = Properties.Settings.Default["OCR_SPACE_APIKEY"].ToString();
        string storedTheme = Properties.Settings.Default["THEME_MODE"]?.ToString() ?? "System";
        themeModeCombo.SelectedItem = themeModeCombo.Items.Cast<string>().FirstOrDefault(i =>
            i.Equals(storedTheme, StringComparison.OrdinalIgnoreCase)) ?? "System";
        captureToggle.Checked = Properties.Settings.Default.ENABLE_SCREEN_CAPTURE;
        audioToggle.Checked = Properties.Settings.Default.ENABLE_AUDIO_RECOGNIZER;
        tuningToggle.Checked = Properties.Settings.Default.ENABLE_SELF_TUNING;
        double delta = Properties.Settings.Default.AUDIO_TRANSIENT_DELTA;
        if (delta < 0.05 || delta > 0.5) delta = 0.12;
        audioDeltaNumeric.Value = (decimal)delta;
        feedUrlText.Text = Properties.Settings.Default.UPDATE_FEED_URL;
        autoCheckUpdatesToggle.Checked = Properties.Settings.Default.AUTO_CHECK_UPDATES;
        updaterStatusLabel.Text = "Status: Idle";

        // Apply system theme
        ThemeManager.ApplyTheme(this);
    }

    private void saveSettingsBtn_Click(object sender, EventArgs e)
    {
        Properties.Settings.Default["OCR_SPACE_APIKEY"] = ocrSpaceApiKey.Text;
        Properties.Settings.Default["GAME_RESOLUTION"] = selectedGameResolution.Text;
        Properties.Settings.Default["THEME_MODE"] = themeModeCombo.SelectedItem?.ToString() ?? "System";
        Properties.Settings.Default.ENABLE_SCREEN_CAPTURE = captureToggle.Checked;
        Properties.Settings.Default.ENABLE_AUDIO_RECOGNIZER = audioToggle.Checked;
        Properties.Settings.Default.ENABLE_SELF_TUNING = tuningToggle.Checked;
        Properties.Settings.Default.AUDIO_TRANSIENT_DELTA = (double)audioDeltaNumeric.Value;
        Properties.Settings.Default.UPDATE_FEED_URL = feedUrlText.Text;
        Properties.Settings.Default.AUTO_CHECK_UPDATES = autoCheckUpdatesToggle.Checked;

        Properties.Settings.Default.Save();

        if (!StateManager.Instance.SetResolution(selectedGameResolution.Text))
        {
            MessageBox.Show($"Invalid resolution: {selectedGameResolution.Text}. Valid resolutions are: 1024x768, 1280x720, 2256x1504",
                "Invalid Resolution", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ThemeManager.SetModeFromString(Properties.Settings.Default["THEME_MODE"]?.ToString());
        ThemeManager.ApplyTheme(Owner ?? this);

        this.Close();
    }

    private async void checkUpdatesButton_Click(object sender, EventArgs e)
    {
        updaterStatusLabel.Text = "Status: Checking...";
        var manifest = await UpdaterService.Instance.CheckForUpdateAsync(feedUrlText.Text);
        if (manifest == null)
        {
            updaterStatusLabel.Text = "Status: No update or invalid feed.";
            return;
        }

        var change = string.IsNullOrWhiteSpace(manifest.Changelog) ? "No changelog." : manifest.Changelog;
        updaterStatusLabel.Text = $"Status: Found {manifest.Version}";
        MessageBox.Show($"Latest version: {manifest.Version}\n{change}", "Update Check",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void downloadUpdateButton_Click(object sender, EventArgs e)
    {
        updaterStatusLabel.Text = "Status: Downloading...";
        var path = await UpdaterService.Instance.DownloadPackageAsync();
        if (path == null)
        {
            updaterStatusLabel.Text = "Status: Download failed.";
            return;
        }
        updaterStatusLabel.Text = $"Status: Downloaded to temp";
    }

    private void applyUpdateButton_Click(object sender, EventArgs e)
    {
        var staged = UpdaterService.Instance.StagePackage();
        if (staged == null)
        {
            updaterStatusLabel.Text = "Status: Stage failed.";
            return;
        }
        if (UpdaterService.Instance.MarkApplyPending())
        {
            updaterStatusLabel.Text = "Status: Pending apply on restart.";
            PromptForRestartLoop();
        }
        else
        {
            updaterStatusLabel.Text = "Status: Apply mark failed.";
        }
    }

    private async void PromptForRestartLoop()
    {
        while (true)
        {
            var result = MessageBox.Show(
                "An update is ready. Apply and restart now?",
                "Update Ready",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);

            if (result == DialogResult.Yes)
            {
                Application.Restart();
                return;
            }

            if (result == DialogResult.Cancel)
            {
                updaterStatusLabel.Text = "Status: Update deferred until next restart.";
                return;
            }

            // Delay option selected
            string input = Interaction.InputBox("Enter delay in minutes before re-prompting:", "Delay Restart", "5");
            if (!int.TryParse(input, out int minutes) || minutes <= 0) minutes = 5;
            updaterStatusLabel.Text = $"Status: Will re-prompt in {minutes} minutes.";
            await Task.Delay(TimeSpan.FromMinutes(minutes));
            // loop to re-prompt
        }
    }
}
