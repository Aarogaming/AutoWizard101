using System.Linq;
using System.Diagnostics;
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
        devTelemetryToggle.Checked = Properties.Settings.Default.ENABLE_DEV_TELEMETRY;
        devTelemetryToggle.Visible = DevMode.IsEnabled;
        devTelemetryToggle.Enabled = DevMode.IsEnabled;
        double delta = Properties.Settings.Default.AUDIO_TRANSIENT_DELTA;
        if (delta < 0.05 || delta > 0.5) delta = 0.12;
        audioDeltaNumeric.Value = (decimal)delta;
        feedUrlText.Text = Properties.Settings.Default.UPDATE_FEED_URL;
        autoCheckUpdatesToggle.Checked = Properties.Settings.Default.AUTO_CHECK_UPDATES;
        updaterStatusLabel.Text = "Status: Idle";
        runDiagnosticsButton.Visible = DevMode.IsEnabled;
        viewDevSuggestionsButton.Visible = DevMode.IsEnabled;
        refreshWikiButton.Visible = true;
        refreshWikiButton.Enabled = true;
        devUiSnapshotsToggle.Checked = Properties.Settings.Default.ENABLE_DEV_UI_SNAPSHOTS;
        devUiSnapshotsToggle.Visible = DevMode.IsEnabled;
        devUiSnapshotsToggle.Enabled = DevMode.IsEnabled;
        captureUiSnapshotButton.Visible = DevMode.IsEnabled;
        playerPreviewToggle.Checked = Properties.Settings.Default.PLAYER_PREVIEW_MODE;
        playerPreviewToggle.Visible = DevMode.IsEnabled;
        playerPreviewToggle.Enabled = DevMode.IsEnabled;
        autoPauseToggle.Checked = Properties.Settings.Default.AUTO_PAUSE_ON_FOCUS_LOSS;
        goldMinNumeric.Value = Properties.Settings.Default.BAZAAR_GOLD_MIN;
        goldCapNumeric.Value = Properties.Settings.Default.BAZAAR_GOLD_CAP;

        // Apply system theme
        ThemeManager.ApplyTheme(this);
        ApplyPalette();

        _ = TryAutoCheckQuietAsync();
    }

    private void saveSettingsBtn_Click(object sender, EventArgs e)
    {
        Properties.Settings.Default["OCR_SPACE_APIKEY"] = ocrSpaceApiKey.Text;
        Properties.Settings.Default["GAME_RESOLUTION"] = selectedGameResolution.Text;
        Properties.Settings.Default["THEME_MODE"] = themeModeCombo.SelectedItem?.ToString() ?? "System";
        Properties.Settings.Default.ENABLE_SCREEN_CAPTURE = captureToggle.Checked;
        Properties.Settings.Default.ENABLE_AUDIO_RECOGNIZER = audioToggle.Checked;
        Properties.Settings.Default.ENABLE_SELF_TUNING = tuningToggle.Checked;
        Properties.Settings.Default.ENABLE_DEV_TELEMETRY = devTelemetryToggle.Checked;
        Properties.Settings.Default.ENABLE_DEV_UI_SNAPSHOTS = devUiSnapshotsToggle.Checked;
        Properties.Settings.Default.PLAYER_PREVIEW_MODE = playerPreviewToggle.Checked;
        Properties.Settings.Default.AUTO_PAUSE_ON_FOCUS_LOSS = autoPauseToggle.Checked;
        Properties.Settings.Default.BAZAAR_GOLD_MIN = (int)goldMinNumeric.Value;
        Properties.Settings.Default.BAZAAR_GOLD_CAP = (int)goldCapNumeric.Value;
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

    private void ApplyPalette()
    {
        var palette = ThemeManager.GetActivePalette();
        var buttons = new[]
        {
            saveSettingsBtn, checkUpdatesButton, downloadUpdateButton, applyUpdateButton,
            launchManagerButton, openMapViewerButton, refreshWikiButton, runDiagnosticsButton, captureUiSnapshotButton, viewDevSuggestionsButton
        };
        foreach (var btn in buttons)
        {
            UIStyles.ApplyButtonStyle(btn, palette.ControlBack, palette.ControlFore, palette.Border);
        }

        var textBoxes = new[] { ocrSpaceApiKey, feedUrlText };
        foreach (var tb in textBoxes)
        {
            tb.BackColor = palette.ControlBack;
            tb.ForeColor = palette.ControlFore;
        }

        updaterStatusLabel.BackColor = palette.Surface;
        updaterStatusLabel.ForeColor = palette.Fore;
        selectedGameResolution.BackColor = palette.ControlBack;
        selectedGameResolution.ForeColor = palette.ControlFore;
        themeModeCombo.BackColor = palette.ControlBack;
        themeModeCombo.ForeColor = palette.ControlFore;
        goldMinNumeric.BackColor = palette.ControlBack;
        goldMinNumeric.ForeColor = palette.ControlFore;
        goldCapNumeric.BackColor = palette.ControlBack;
        goldCapNumeric.ForeColor = palette.ControlFore;
    }

    private async Task TryAutoCheckQuietAsync()
    {
        if (!Properties.Settings.Default.AUTO_CHECK_UPDATES)
        {
            return;
        }

        var feed = Properties.Settings.Default.UPDATE_FEED_URL;
        if (string.IsNullOrWhiteSpace(feed))
        {
            updaterStatusLabel.Text = "Status: Set update feed to enable auto-check.";
            return;
        }

        try
        {
            updaterStatusLabel.Text = "Status: Checking...";
            var manifest = await UpdaterService.Instance.CheckForUpdateAsync(feed);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                updaterStatusLabel.Text = "Status: Feed unreachable; enter URL manually.";
                feedUrlText.Focus();
                return;
            }

            if (!Version.TryParse(Application.ProductVersion, out var current) ||
                !Version.TryParse(manifest.Version, out var latest))
            {
                updaterStatusLabel.Text = $"Status: Latest {manifest.Version}";
                return;
            }

            if (latest > current)
            {
                updaterStatusLabel.Text = $"Status: Update available ({current} → {latest})";
            }
            else
            {
                updaterStatusLabel.Text = $"Status: Up to date ({current})";
            }
        }
        catch
        {
            updaterStatusLabel.Text = "Status: Auto-check failed; enter feed manually.";
            feedUrlText.Focus();
        }
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

    private void launchManagerButton_Click(object sender, EventArgs e)
    {
        try
        {
            var appRoot = StorageUtils.GetAppRoot();
            var candidates = new[]
            {
                Path.Combine(appRoot, "Installer.exe"),
                Path.Combine(appRoot, "Installer", "Installer.exe"),
                Path.Combine(appRoot, "Installer", "publish", "Installer.exe"),
                Path.GetFullPath(Path.Combine(appRoot, "..", "Installer", "publish", "Installer.exe"))
            };

            var exe = candidates.FirstOrDefault(File.Exists);
            if (exe == null)
        {
            updaterStatusLabel.Text = "Status: Project Manager not found. Reinstall or download installer.";
            MessageBox.Show("Project Manager (Installer) not found near the app. Please reinstall or download the installer package.", "Not Found",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe)!
            });
            updaterStatusLabel.Text = "Status: Project Manager launched.";
        }
        catch (Exception ex)
        {
            updaterStatusLabel.Text = "Status: Failed to launch Project Manager.";
            MessageBox.Show($"Failed to launch Project Manager: {ex.Message}", "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void openMapViewerButton_Click(object sender, EventArgs e)
    {
        try
        {
            using var viewer = new MapViewerForm
            {
                StartPosition = FormStartPosition.CenterParent,
                TopMost = true
            };
            viewer.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open map viewer: {ex.Message}", "Map Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void runDiagnosticsButton_Click(object sender, EventArgs e)
    {
        if (!DevMode.IsEnabled)
        {
            MessageBox.Show("Diagnostics are available only in dev mode.", "Dev Mode Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        runDiagnosticsButton.Enabled = false;
        updaterStatusLabel.Text = "Status: Running diagnostics...";
        try
        {
            var report = await DiagnosticsService.RunBasicAsync();
            updaterStatusLabel.Text = "Status: Diagnostics complete.";
            MessageBox.Show(report, "Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            updaterStatusLabel.Text = "Status: Diagnostics failed.";
            MessageBox.Show($"Diagnostics failed: {ex.Message}", "Diagnostics Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            runDiagnosticsButton.Enabled = true;
        }
    }

    private void captureUiSnapshotButton_Click(object sender, EventArgs e)
    {
        if (!DevMode.IsEnabled)
        {
            MessageBox.Show("UI capture is available only in dev mode.", "Dev Mode Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var target = Owner ?? this;
        UiSnapshotService.TryCapture(target, "manual");
        MessageBox.Show("UI snapshot captured to logs/dev.", "Snapshot", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void viewDevSuggestionsButton_Click(object sender, EventArgs e)
    {
        if (!DevMode.IsEnabled)
        {
            MessageBox.Show("Dev suggestions are available only in dev mode.", "Dev Mode Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "logs", "dev");
            if (!Directory.Exists(dir))
            {
                MessageBox.Show("No dev logs found yet.", "Dev Suggestions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var latest = Directory.GetFiles(dir, "ui_*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (latest == null)
            {
                MessageBox.Show("No UI suggestion logs found. Capture a UI snapshot first.", "Dev Suggestions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var json = File.ReadAllText(latest);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var issues = doc.RootElement.TryGetProperty("Issues", out var issuesEl) && issuesEl.ValueKind == System.Text.Json.JsonValueKind.Array
                ? string.Join(Environment.NewLine, issuesEl.EnumerateArray().Select(i => i.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)))
                : "No issues recorded.";

            MessageBox.Show($"Latest: {Path.GetFileName(latest)}\n\n{issues}", "Dev Suggestions", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load suggestions: {ex.Message}", "Dev Suggestions", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void refreshWikiButton_Click(object sender, EventArgs e)
    {
        try
        {
            WizWikiDataService.Instance.Refresh();
            updaterStatusLabel.Text = $"Status: Wiki cache refreshed ({WizWikiDataService.Instance.ZoneCount} zones, {WizWikiDataService.Instance.ResourceCount} resources)";
        }
        catch (Exception ex)
        {
            updaterStatusLabel.Text = "Status: Wiki refresh failed.";
            MessageBox.Show($"Failed to refresh wiki cache: {ex.Message}", "Wiki Refresh", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
