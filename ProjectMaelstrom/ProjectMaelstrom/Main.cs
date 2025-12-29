using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using ProjectMaelstrom.Models;
using ProjectMaelstrom.Modules.ImageRecognition;
using ProjectMaelstrom.Tests;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

public partial class Main : Form
{
    private readonly System.Windows.Forms.Timer _manaRefreshTimer;
    private readonly System.Windows.Forms.Timer _syncTimer;
    private readonly ScriptLibraryService _scriptLibraryService = ScriptLibraryService.Instance;
    private InputBridge? _inputBridge;
    private readonly PlayerController _playerController = new PlayerController();
    private SnapshotBridge? _snapshotBridge;
    private SmartPlayManager? _smartPlayManager;
    private AudioRecognizer? _audioRecognizer;
    private readonly DesignManager _designManager = new();
    private System.Threading.Timer? _designCaptureTimer;
    private string _lastDesignCapturePath = string.Empty;
    private Form? _activeChildForm;
    private GameStateSnapshot? _latestSnapshot;
    private readonly List<string> _runHistory = new();
    private bool _miniMode = true;
    private readonly Size _fullSize = new Size(1000, 760);
    private readonly Size _miniSize = new Size(900, 700);
    private readonly string[] _energyKeywords = new[] { "garden", "pet", "dance" };
    private readonly string[] _bazaarKeywords = new[] { "bazaar", "auction", "reagent", "market", "sell", "vendor" };
    private const int HealthMinThreshold = 50;
    private const int EnergyMinThreshold = 1;
    private const int PotionMinThreshold = 1;
    private int GoldMinThreshold => Properties.Settings.Default.BAZAAR_GOLD_MIN;
    private int GoldCapThreshold => Properties.Settings.Default.BAZAAR_GOLD_CAP; // close to cap; leave headroom
    private DateTime? _knowledgeLastRefreshed;
    private bool _potionRunQueued;
    private bool _healthGuardTriggered;
    private bool _goldGuardTriggered;
    private bool _autoCaptureTriggered;
    private System.Windows.Forms.Timer? _devUiTimer;
    private LearnModeService? _learnModeService;
    private bool _smartPaused;
    private readonly WizWikiDataService _wikiData = WizWikiDataService.Instance;
    private BridgeCoordinator? _bridge;
    private string _lastBanner = string.Empty;
    private string _scriptSearchTerm = string.Empty;
    private string _statusFilter = "All";
    private int _lastSortColumn = -1;
    private bool _lastSortAsc = true;
    private bool _pauseForResource;
    private bool _energyGuardTriggered;

    public Main()
    {
        InitializeComponent();
        EnableDoubleBuffer(this);
        EnableDoubleBuffer(navPanel);
        EnableDoubleBuffer(trainerListView);
        EnableDoubleBuffer(dashboardGroupBox);
        EnableDoubleBuffer(speedPanel);
        EnableDoubleBuffer(panel1);
        _manaRefreshTimer = new System.Windows.Forms.Timer
        {
            Interval = StateManager.ManaRefreshIntervalSec * 1000
        };
        _manaRefreshTimer.Tick += ManaRefreshTimer_Tick;
        _syncTimer = new System.Windows.Forms.Timer
        {
            Interval = 2000
        };
        _syncTimer.Tick += SyncTimer_Tick;
        this.FormClosing += Main_FormClosing;
        _smartPlayManager = new SmartPlayManager(_playerController);
        _learnModeService = new LearnModeService(_smartPlayManager);
        _bridge = new BridgeCoordinator(_scriptLibraryService, _smartPlayManager);
        _bridge.SetPreflight(PreflightResourceGuard);
        _bridge.OnStatus += msg => AddRunHistory(msg);
        _bridge.OnRunHistory += msg => AddRunHistory(msg);
        _bridge.OnWarning += msg =>
        {
            ShowBanner(msg);
        };
        this.Resize += Main_Resize;

        if (DevMode.IsEnabled && Properties.Settings.Default.ENABLE_DEV_UI_SNAPSHOTS)
        {
            _devUiTimer = new System.Windows.Forms.Timer
            {
                Interval = 180000 // 3 minutes
            };
            _devUiTimer.Tick += (s, e) => UiSnapshotService.TryCapture(this, "auto");
            _devUiTimer.Start();
        }

        ApplyPlayerPreview(Properties.Settings.Default.PLAYER_PREVIEW_MODE);
    }

    private void ApplyPlayerPreview(bool enabled)
    {
        // Hide dev-facing controls in player preview
        var devControls = new Control[]
        {
            captureScreenButton,
            designManagerButton,
            openDesignFolderButton,
            recordMacroButton,
            runMacroButton
        };

        foreach (var ctrl in devControls)
        {
            ctrl.Visible = !enabled;
        }

        UpdateModeLabel();
    }

    private void EnableDoubleBuffer(Control control)
    {
        try
        {
            var prop = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            prop?.SetValue(control, true, null);
        }
        catch
        {
            // ignore if we can't set it
        }
    }

    private void Main_Resize(object? sender, EventArgs e)
    {
        navPanel?.Invalidate();
        trainerListView?.Invalidate();
        dashboardGroupBox?.Invalidate();
        this.Invalidate();
    }

    private void Main_Load(object sender, EventArgs e)
    {
        ApplyPortableOverridesIfNeeded();
        _manaRefreshTimer.Start();
        _syncTimer.Start();

        _smartPlayManager?.Start();
        _scriptLibraryService.PreflightCheck = PreflightResourceGuard;
        _inputBridge = new InputBridge(_playerController);
        _inputBridge.Start(TimeSpan.FromSeconds(1));
        _smartPlayManager?.AttachInputBridge(_inputBridge);
        _smartPlayManager?.SetDesignCaptureHandler(CaptureDesignSamples);
        UpdateSmartPlayHeader(_smartPlayManager?.DescribeState() ?? "Idle");

        if (Properties.Settings.Default.ENABLE_AUDIO_RECOGNIZER)
        {
            _audioRecognizer = new AudioRecognizer(Properties.Settings.Default.AUDIO_TRANSIENT_DELTA);
            _audioRecognizer.CueDetected += cue =>
            {
            try
            {
                _smartPlayManager?.AddAudioCue(cue);
                UpdateAudioHeader("On (cue)");
            }
            catch
            {
                // non-fatal; ignore
            }
            };
            _audioRecognizer.Start();
            UpdateAudioHeader("On");
        }
        else
        {
            UpdateAudioHeader("Off");
        }

        // Periodic background design captures via SmartPlay (idle-friendly)
        _designCaptureTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                _smartPlayManager?.EnqueueDesignCapture();
            }
            catch
            {
                // ignore best effort
            }
        }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(20));

        // Speed header setup
        try
        {
            double speed = Properties.Settings.Default.SPEED_MULTIPLIER;
            speed = Math.Max(0.0, Math.Min(3.0, speed));
            speedNumeric.Value = (decimal)speed;
            UpdateSpeedLabel(speed);
            speedNumeric.ValueChanged += speedNumeric_ValueChanged;
        }
        catch
        {
            speedNumeric.Value = 1.0M;
            UpdateSpeedLabel(1.0);
        }

        ThemeManager.SetModeFromString(Properties.Settings.Default["THEME_MODE"]?.ToString());

        StateManager.Instance.SelectedResolution = "1280x720";

        // Apply system theme
        ThemeManager.ApplyTheme(this);
        ApplyCardStyles();
        ApplyMiniMode(_miniMode);
        ApplyPlayerPreview(Properties.Settings.Default.PLAYER_PREVIEW_MODE);
        var storedProfile = Properties.Settings.Default.LEARN_MODE_PROFILE;
        var initialProfile = string.IsNullOrWhiteSpace(storedProfile) ? "Mixed" : storedProfile;
        var targetProfile = Enum.TryParse<LearnModeProfile>(initialProfile, true, out var parsed) ? parsed : LearnModeProfile.Mixed;
        _learnModeService?.SetProfile(targetProfile);
        SetLearnProfileCombo(targetProfile);
        UpdateLearnProfileStatus(targetProfile.ToString());
        UpdateKnowledgeInfo();
        UpdateModeLabel();

        LoadScriptLibrary();
        PopulateTrainerList();
        UpdateScriptStatus();

        // Toggle capture button visibility based on settings
        captureScreenButton.Visible = Properties.Settings.Default.ENABLE_SCREEN_CAPTURE;

        _snapshotBridge = new SnapshotBridge();
        _snapshotBridge.Start(TimeSpan.FromSeconds(5));

        trainerListView.MouseDoubleClick += TrainerListView_MouseDoubleClick;
        trainerListView.ColumnClick += trainerListView_ColumnClick;

        if (statusFilterCombo.SelectedIndex < 0)
        {
            statusFilterCombo.SelectedIndex = 0;
            _statusFilter = "All";
        }

        _ = CheckUpdatesIfEnabledAsync();
    }

    private void Main_FormClosing(object? sender, FormClosingEventArgs e)
    {
        try
        {
            _scriptLibraryService.StopCurrentScript();
        }
        catch
        {
            // best-effort stop
        }

        if (_activeChildForm != null)
        {
            try
            {
                _activeChildForm.Close();
                _activeChildForm.Dispose();
            }
            catch
            {
                // best-effort cleanup; ignore errors on shutdown
            }
            _activeChildForm = null;
        }
        _manaRefreshTimer?.Stop();
        _manaRefreshTimer?.Dispose();
        _syncTimer?.Stop();
        _syncTimer?.Dispose();
        _snapshotBridge?.Dispose();
        _inputBridge?.Dispose();
        _audioRecognizer?.Dispose();
        _designCaptureTimer?.Dispose();
        _smartPlayManager?.Dispose();
        _learnModeService?.Dispose();
    }

    private void ApplyPortableOverridesIfNeeded()
    {
        try
        {
            var portableFlag = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable_mode.txt");
            if (File.Exists(portableFlag))
            {
                Properties.Settings.Default.UPDATE_FEED_URL = string.Empty;
                Properties.Settings.Default.AUTO_CHECK_UPDATES = false;
                Properties.Settings.Default.ENABLE_SCREEN_CAPTURE = false;
                Properties.Settings.Default.Save();
            }
        }
        catch
        {
            // ignore portable override failures
        }
    }

    private void ManaRefreshTimer_Tick(object? sender, EventArgs e)
    {
        // Mana display removed from header; keep timer for future use if needed.
    }

    private void SyncTimer_Tick(object? sender, EventArgs e)
    {
        var syncState = GameSyncService.Evaluate(StateManager.Instance.SelectedResolution);
        var launcherState = WizardLauncher.DetectState(out var launcherDesc);
        syncStatusValueLabel.Text = $"{syncState.Message} | Launcher: {launcherDesc}";
        launcherStatusLabel.Text = $"Launcher: {launcherDesc}";
        launcherStatusLabel.ForeColor = launcherState switch
        {
            WizardLauncher.LauncherState.GameRunning => Color.LightGreen,
            WizardLauncher.LauncherState.LauncherRunning => Color.Gold,
            _ => Color.Silver
        };
        switch (syncState.Health)
        {
            case GameSyncHealth.InSync:
                syncStatusValueLabel.ForeColor = Color.DarkGreen;
                break;
            case GameSyncHealth.FocusLost:
            case GameSyncHealth.ResolutionMismatch:
                syncStatusValueLabel.ForeColor = Color.DarkOrange;
                break;
            case GameSyncHealth.WindowMissing:
            case GameSyncHealth.Unknown:
            default:
                syncStatusValueLabel.ForeColor = Color.DarkRed;
                break;
        }

        var shouldPauseForFocus = Properties.Settings.Default.AUTO_PAUSE_ON_FOCUS_LOSS &&
                          (syncState.Health == GameSyncHealth.FocusLost || syncState.Health == GameSyncHealth.WindowMissing);
        var shouldPause = shouldPauseForFocus || _pauseForResource;

        if (shouldPause && !_smartPaused)
        {
            _smartPaused = true;
            _smartPlayManager?.Stop();
            var reason = shouldPauseForFocus ? "focus" : "resource";
            UpdateSmartPlayHeader($"Paused ({reason})");
        }
        else if (!shouldPause && _smartPaused)
        {
            _smartPaused = false;
            _smartPlayManager?.Start();
            UpdateSmartPlayHeader(_smartPlayManager?.DescribeState() ?? "Idle");
        }

        if (Properties.Settings.Default.ENABLE_SCREEN_CAPTURE)
        {
            if (syncState.HasWindow && !_autoCaptureTriggered)
            {
                var path = ScreenCaptureService.CaptureWizardWindow();
                if (!string.IsNullOrEmpty(path))
                {
                    _autoCaptureTriggered = true;
                    AddRunHistory($"Auto-captured screen: {Path.GetFileName(path)}");
                }
            }
            else if (!syncState.HasWindow)
            {
                _autoCaptureTriggered = false;
            }
        }

        _ = RefreshSnapshotAsync(updateDashboardOnly: true);
        TryEnforceResourceGuards();
        UpdateSmartPlayHeader(_smartPlayManager?.DescribeState() ?? "Idle");
        if (!string.IsNullOrEmpty(_lastBanner))
        {
            ShowBanner(_lastBanner);
        }
    }

    private void editSettingsBtn_Click(object sender, EventArgs e)
    {
        SettingsForm settingsForm = new SettingsForm
        {
            TopMost = true,
            Owner = this
        };
        settingsForm.Show();
    }

    private async void startConfigurationBtn_Click(object sender, EventArgs e)
    {
        string? imagePath = null;
        bool deleteAfterUse = false;

        try
        {
            // If the game is not running, allow the user to pick an existing screenshot instead.
            if (!GeneralUtils.Instance.IsGameVisible())
            {
                var useScreenshot = MessageBox.Show(
                    "Wizard101 window not detected. Would you like to select an existing screenshot instead?",
                    "Wizard101 not running",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (useScreenshot != DialogResult.Yes)
                {
                    throw new InvalidOperationException("Wizard101 is not running and no screenshot was provided.");
                }

                imagePath = SelectScreenshotFromDisk(out deleteAfterUse)
                    ?? throw new InvalidOperationException("No screenshot selected.");
            }
            else
            {
                int attempts = 0;
                const int maxAttempts = StateManager.MaxGameWindowAttempts; // 10 seconds at 500ms
                while (!GeneralUtils.Instance.IsGameVisible() && attempts < maxAttempts)
                {
                    GeneralUtils.Instance.OpenGameWindow();
                    await Task.Delay(500);
                    attempts++;
                }
                if (attempts >= maxAttempts)
                {
                    throw new Exception("Failed to open game window after 10 seconds.");
                }

                attempts = 0;
                while (!GeneralUtils.Instance.IsStatsPageVisible() && attempts < maxAttempts)
                {
                    GeneralUtils.Instance.OpenStatsWindow();
                    await Task.Delay(500);
                    attempts++;
                }
                if (attempts >= maxAttempts)
                {
                    throw new Exception("Failed to open stats page after 10 seconds.");
                }

                IntPtr windowHandle = ImageFinder.GetWindowHandle();
                if (windowHandle == IntPtr.Zero)
                {
                    throw new Exception("Window not found.");
                }

                ImageFinder.RECT rect = ImageFinder.GetWindowRect(windowHandle);

                imagePath = ImageFinder.CaptureScreen(rect);
            }

            string extractedText = await ImageHelpers.ExtractTextFromImage(imagePath);

            string[] extractedTextArray = extractedText
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            string? mana = null;

            for (int i = 0; i < extractedTextArray.Length; i++)
            {
                if (string.Equals(extractedTextArray[i], "GOLD", StringComparison.OrdinalIgnoreCase) && i > 0)
                {
                    mana = extractedTextArray[i - 1];
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(mana))
            {
                throw new InvalidOperationException("Unable to locate mana line in OCR output.");
            }

            string[] manaArray = mana.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
            if (manaArray.Length != 2 ||
                !int.TryParse(manaArray[0], out int currentMana) ||
                !int.TryParse(manaArray[1], out int maxMana))
            {
                throw new FormatException($"Unexpected mana format: '{mana}'.");
            }

            StateManager.Instance.CurrentMana = currentMana;
            StateManager.Instance.MaxMana = maxMana;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Configuration failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            // Clean up temp copy if we created one for offline configuration
            if (deleteAfterUse && !string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
            {
                try { File.Delete(imagePath); } catch { /* ignore cleanup errors */ }
            }
        }
    }

    private void manageScriptsButton_Click(object sender, EventArgs e)
    {
        using var dlg = new ManageScriptsForm(_bridge)
        {
            StartPosition = FormStartPosition.CenterParent,
            TopMost = true
        };
        dlg.ShowDialog(this);
        // After closing, refresh trainer list/status to reflect any changes
        PopulateTrainerList();
        UpdateScriptStatus();
    }

    private void searchTextBox_TextChanged(object sender, EventArgs e)
    {
        _scriptSearchTerm = searchTextBox.Text ?? string.Empty;
        PopulateTrainerList();
        UpdateFilterNote();
    }

    private void statusFilterCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        _statusFilter = statusFilterCombo.SelectedItem?.ToString() ?? "All";
        PopulateTrainerList();
        UpdateFilterNote();
    }

    private string? SelectScreenshotFromDisk(out bool isTempFile)
    {
        isTempFile = false;
        using OpenFileDialog dialog = new OpenFileDialog
        {
            Title = "Select Wizard101 screenshot",
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff",
            Multiselect = false
        };

        if (dialog.ShowDialog() == DialogResult.OK && File.Exists(dialog.FileName))
        {
            string tempCopy = Path.Combine(Path.GetTempPath(),
                $"maelstrom_config_{Guid.NewGuid():N}{Path.GetExtension(dialog.FileName)}");
            File.Copy(dialog.FileName, tempCopy, true);
            isTempFile = true;
            return tempCopy;
        }

        return null;
    }

    private void loadHalfangBotBtn_Click(object sender, EventArgs e)
    {
        ShowChildForm(new HalfangFarmingBot());
    }

    private void loadBazaarReagentBot_Click(object sender, EventArgs e)
    {
        ShowChildForm(new BazaarReagentBot());
    }

    private void runPetDanceScriptBtn_Click(object sender, EventArgs e)
    {
        ShowChildForm(new PetDanceBot());
    }

    private void runTestsBtn_Click(object sender, EventArgs e)
    {
        try
        {
            UtilityTests.RunAllTests();
            MessageBox.Show("Tests completed. Check console output for results.", "Tests Complete",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Tests failed: " + ex.Message, "Test Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowChildForm(Form childForm)
    {
        if (_activeChildForm != null)
        {
            try
            {
                _activeChildForm.Close();
                _activeChildForm.Dispose();
            }
            catch
            {
                // ignore cleanup failures, continue to load new form
            }
        }

        _activeChildForm = childForm;
        childForm.TopLevel = false;
        childForm.FormBorderStyle = FormBorderStyle.None;
        childForm.Dock = DockStyle.Fill;
        childForm.ShowInTaskbar = false;

        childHostPanel.Controls.Clear();
        childHostPanel.Visible = true;
        trainerListView.Visible = false;
        childHostPanel.Controls.Add(childForm);
        childForm.Show();
        childForm.BringToFront();
    }

    private void LoadScriptLibrary()
    {
        _scriptLibraryService.ReloadLibrary();
        PopulateTrainerList();
    }

    private void runScriptButton_Click(object sender, EventArgs e)
    {
        MessageBox.Show("Use 'Manage Scripts' to run scripts.", "Manage Scripts", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void stopScriptButton_Click(object sender, EventArgs e)
    {
        try
        {
            _scriptLibraryService.StopCurrentScript();
            UpdateScriptStatus();
            AddRunHistory($"Stopped: {DateTime.Now:T}");
            PopulateTrainerList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to stop script: {ex.Message}", "Script Stop Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void refreshScriptsButton_Click(object sender, EventArgs e)
    {
        LoadScriptLibrary();
        UpdateScriptStatus();
        PopulateTrainerList();
    }

    private void dryRunCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        // handled in Manage Scripts
    }

    private async void snapshotButton_Click(object sender, EventArgs e)
    {
        await RefreshSnapshotAsync();
    }

    private void loadLogButton_Click(object sender, EventArgs e)
    {
        MessageBox.Show("Use 'Manage Scripts' to view logs.", "Manage Scripts", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UpdateScriptStatus()
    {
        var running = _scriptLibraryService.CurrentSession;
        if (running == null)
        {
            dashboardStatusLabel.Text = "Status: Idle | Sync: -";
            PopulateTrainerList();
            return;
        }

        dashboardStatusLabel.Text = $"Running: {running.Script.Manifest.Name} | Sync: {syncStatusValueLabel.Text}";
        PopulateTrainerList();
    }

    private static string GetScriptLogPath(string scriptName)
    {
        string sanitized = SanitizeFileName(scriptName);
        return Path.Combine(AppContext.BaseDirectory, "logs", $"{sanitized}.log");
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return string.IsNullOrWhiteSpace(name) ? "script" : name;
    }

    private static string GetLastRunDisplay(string scriptName)
    {
        try
        {
            var path = GetScriptLogPath(scriptName);
            if (!File.Exists(path)) return "-";
            var ts = File.GetLastWriteTime(path);
            return ts.ToString("MM-dd HH:mm");
        }
        catch
        {
            return "-";
        }
    }

    private void openScriptFolderButton_Click(object sender, EventArgs e)
    {
        MessageBox.Show("Use 'Manage Scripts' to open script folders.", "Manage Scripts", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void openFullLogButton_Click(object sender, EventArgs e)
    {
        MessageBox.Show("Use 'Manage Scripts' to open full logs.", "Manage Scripts", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void launchWizardButton_Click(object sender, EventArgs e)
    {
        if (WizardLauncher.Launch(out var msg))
        {
            MessageBox.Show(msg, "Wizard101", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show(msg, "Wizard101", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void miniModeButton_Click(object sender, EventArgs e)
    {
        _miniMode = !_miniMode;
        ApplyMiniMode(_miniMode);
    }

    private async Task RefreshSnapshotAsync(bool updateDashboardOnly = false)
    {
        try
        {
            var snapshot = await GameStateService.CaptureSnapshotAsync(StateManager.Instance.SelectedResolution);
            _latestSnapshot = snapshot;

            UpdateDashboard(snapshot);

            if (!updateDashboardOnly)
            {
                if (snapshot.Warnings.Any())
                {
                    snapshotWarningsTextBox.Text = string.Join(Environment.NewLine, snapshot.Warnings);
                }
                else
                {
                    snapshotWarningsTextBox.Text = "None";
                }
            }

            _smartPlayManager?.UpdateSensors(new GameSensors
            {
                Snapshot = snapshot,
                AudioCues = Array.Empty<GameAudioCue>() // audio cues can be populated by an audio recognizer when available
            });
        }
        catch (Exception ex)
        {
            snapshotWarningsTextBox.Text = $"Snapshot failed: {ex.Message}";
        }
    }

    private void UpdateDashboard(GameStateSnapshot snapshot)
    {
        healthLabel.Text = snapshot.Health != null && snapshot.Health.Current.HasValue && snapshot.Health.Max.HasValue
            ? $"Health: {snapshot.Health.Current}/{snapshot.Health.Max}"
            : "Health: -";

        manaLabel.Text = snapshot.Mana != null && snapshot.Mana.Current.HasValue && snapshot.Mana.Max.HasValue
            ? $"Mana: {snapshot.Mana.Current}/{snapshot.Mana.Max}"
            : "Mana: -";

        energyLabel.Text = snapshot.Energy != null && snapshot.Energy.Current.HasValue && snapshot.Energy.Max.HasValue
            ? $"Energy: {snapshot.Energy.Current}/{snapshot.Energy.Max}"
            : "Energy: -";

        goldLabel.Text = snapshot.Gold != null && snapshot.Gold.Value.HasValue
            ? $"Gold: {snapshot.Gold.Value}"
            : "Gold: -";

        potionsLabel.Text = snapshot.Potions != null && snapshot.Potions.Value.HasValue
            ? $"Potions: {snapshot.Potions.Value}"
            : "Potions: -";

        dashboardStatsLabel.Text =
            $"Health: {(snapshot.Health?.Current?.ToString() ?? "-")}/{(snapshot.Health?.Max?.ToString() ?? "-")} | " +
            $"Mana: {(snapshot.Mana?.Current?.ToString() ?? "-")}/{(snapshot.Mana?.Max?.ToString() ?? "-")} | " +
            $"Energy: {(snapshot.Energy?.Current?.ToString() ?? "-")}/{(snapshot.Energy?.Max?.ToString() ?? "-")} | " +
            $"Gold: {(snapshot.Gold?.Value?.ToString() ?? "-")} | " +
            $"Potions: {(snapshot.Potions?.Value?.ToString() ?? "-")}";

        dashboardWarningsTextBox.Text = snapshot.Warnings.Any()
            ? string.Join(Environment.NewLine, snapshot.Warnings)
            : "None";

        if (_scriptLibraryService.CurrentSession == null)
        {
            dashboardStatusLabel.Text = $"Status: Idle | Sync: {syncStatusValueLabel.Text}";
        }
        else
        {
            dashboardStatusLabel.Text = $"Running: {_scriptLibraryService.CurrentSession.Script.Manifest.Name} | Sync: {syncStatusValueLabel.Text}";
        }

        smartPlayStatusLabel.Text = $"SmartPlay: {_smartPlayManager?.DescribeState() ?? "Idle"}";

        if (snapshot.Potions?.Value > 0)
        {
            _potionRunQueued = false;
        }
    }

    private void ApplyCardStyles()
    {
        var palette = ThemeManager.GetActivePalette();
        UIStyles.ApplyCardStyle(dashboardGroupBox, palette.Surface, palette.Border);
        UIStyles.ApplyCardStyle(panel1, palette.Back, palette.Border);
        UIStyles.ApplyCardStyle(navPanel, palette.Surface, palette.Border);
        UIStyles.ApplyCardStyle(speedPanel, palette.Surface, palette.Border);

        // Apply consistent button styling to nav buttons
        var navButtons = new[]
        {
            manageScriptsButton, startConfigurationBtn, loadHalfangBotBtn, loadBazaarReagentBot, launchWizardButton,
            miniModeButton, captureScreenButton, designManagerButton, openDesignFolderButton, recordMacroButton,
            runMacroButton, learnModeButton, openLearnLogsButton, panicStopButton
        };
        foreach (var btn in navButtons)
        {
            UIStyles.ApplyButtonStyle(btn, palette.ControlBack, palette.ControlFore, palette.Border);
        }
        foreach (var btn in new[] { goBazaarButton, goMiniGamesButton, goPetPavilionButton, potionRefillButton })
        {
            UIStyles.ApplyButtonStyle(btn, palette.ControlBack, palette.ControlFore, palette.Border);
        }
    }

    private void AddRunHistory(string entry)
    {
        _runHistory.Insert(0, entry);
        if (_runHistory.Count > 10)
        {
            _runHistory.RemoveAt(_runHistory.Count - 1);
        }

        runHistoryListBox.DataSource = null;
        runHistoryListBox.DataSource = _runHistory.ToList();
    }

    private void ApplyMiniMode(bool mini)
    {
        dashboardGroupBox.Visible = !mini;
        snapshotWarningsLabel.Visible = !mini;
        snapshotWarningsTextBox.Visible = !mini;
        this.ClientSize = mini ? _miniSize : _fullSize;
        miniModeButton.Text = mini ? "Mini Mode: On" : "Mini Mode: Off";
    }

    private void TryEnforceResourceGuards()
    {
        var snapshot = _latestSnapshot;
        if (snapshot == null)
        {
            return;
        }

        var current = _scriptLibraryService.CurrentSession;
        if (current == null)
        {
            return;
        }

        if (IsEnergyConsumer(current.Script) && GetEnergy(snapshot) <= 0)
        {
            _scriptLibraryService.StopCurrentScript();
            AddRunHistory($"Stopped {current.Script.Manifest.Name} due to zero energy at {DateTime.Now:T}");
            UpdateScriptStatus();
            PopulateTrainerList();
            var msg = "Energy depleted. Stopped energy-based task.";
            if (!_energyGuardTriggered)
            {
                MessageBox.Show(msg, "Resource guard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            _pauseForResource = true;
            _energyGuardTriggered = true;
            ShowBanner(msg);
        }
        else if (IsEnergyConsumer(current.Script) && GetEnergy(snapshot) > EnergyMinThreshold)
        {
            _energyGuardTriggered = false;
        }

        if (!_healthGuardTriggered && snapshot.Health?.Current is int hp && hp <= HealthMinThreshold)
        {
            _healthGuardTriggered = true;
            _scriptLibraryService.StopCurrentScript();
            AddRunHistory($"Stopped {_scriptLibraryService.CurrentSession?.Script.Manifest.Name ?? "task"} due to low health at {DateTime.Now:T}");
            UpdateScriptStatus();
            PopulateTrainerList();
            var msg = $"Health too low (<= {HealthMinThreshold}). Stopped current task.";
            MessageBox.Show(msg, "Resource guard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ShowBanner(msg);
            _pauseForResource = true;
        }
        else if (snapshot.Health?.Current is int hpOk && hpOk > HealthMinThreshold)
        {
            _healthGuardTriggered = false;
        }

        if (IsBazaarScript(current.Script) && snapshot.Gold?.Value is int gold)
        {
            if (gold <= GoldMinThreshold || gold >= GoldCapThreshold)
            {
                _scriptLibraryService.StopCurrentScript();
                AddRunHistory($"Stopped {current.Script.Manifest.Name} due to gold threshold at {DateTime.Now:T} (gold={gold})");
                UpdateScriptStatus();
                PopulateTrainerList();
                var msg = gold <= GoldMinThreshold
                    ? $"Gold too low (<= {GoldMinThreshold}). Bazaar tasks paused."
                    : $"Gold near cap ({gold}). Bazaar/sell tasks paused to avoid loss.";
                MessageBox.Show(msg, "Resource guard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ShowBanner(msg);
                _pauseForResource = true;
                _goldGuardTriggered = true;
            }
            else
            {
                _goldGuardTriggered = false;
            }
        }

        if (!_potionRunQueued && snapshot.Potions?.Value is int potions && potions <= PotionMinThreshold)
        {
            _smartPlayManager?.EnqueuePotionRefillRun();
            _potionRunQueued = true;
            AddRunHistory($"Queued potion refill run at {DateTime.Now:T}");
            ShowBanner("Potions low; queued potion refill run.");
        }
        else if (snapshot.Potions?.Value is int potionsOk && potionsOk > PotionMinThreshold)
        {
            _potionRunQueued = false;
        }

        if (_pauseForResource && !_healthGuardTriggered && !_energyGuardTriggered && !_potionRunQueued && !_goldGuardTriggered)
        {
            _pauseForResource = false;
            if (!_smartPaused)
            {
                _smartPlayManager?.Start();
                UpdateSmartPlayHeader(_smartPlayManager?.DescribeState() ?? "Idle");
            }
        }
        else
        {
            UpdateGuardStatusLabel();
        }
    }

    private bool ShouldBlockForResources(ScriptDefinition script, out string reason)
    {
        reason = string.Empty;
        var snapshot = _latestSnapshot;
        if (snapshot == null)
        {
            return false;
        }

        if (IsEnergyConsumer(script) && GetEnergy(snapshot) <= EnergyMinThreshold)
        {
            reason = "Not enough energy to start this task.";
            ShowBanner(reason);
            return true;
        }

        if (IsBazaarScript(script) && snapshot.Gold?.Value is int gold)
        {
            if (gold <= GoldMinThreshold)
            {
                reason = $"Gold too low for bazaar tasks (<= {GoldMinThreshold}).";
                ShowBanner(reason);
                return true;
            }
            if (gold >= GoldCapThreshold)
            {
                reason = $"Gold near cap ({gold}); pause bazaar/sell tasks to avoid loss.";
                ShowBanner(reason);
                return true;
            }
        }

        if (snapshot.Potions?.Value is int potions && potions <= PotionMinThreshold)
        {
            reason = "Not enough potions to start; auto-refill queued.";
            if (!_potionRunQueued)
            {
                _smartPlayManager?.EnqueuePotionRefillRun();
                _potionRunQueued = true;
                AddRunHistory($"Queued potion refill run before starting {script.Manifest.Name} at {DateTime.Now:T}");
            }
            ShowBanner(reason);
            return true;
        }

        if (snapshot.Health?.Current is int hp && hp <= HealthMinThreshold)
        {
            reason = $"Health too low (<= {HealthMinThreshold}). Heal before starting.";
            ShowBanner(reason);
            return true;
        }

        return false;
    }

    private void ShowBanner(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _lastBanner = message;
        dashboardWarningsTextBox.Text = message;
        snapshotWarningsTextBox.Text = message;
        ShowToast(message);
    }

    private void ShowToast(string message)
    {
        try
        {
            uiToolTip.Show(message, panel1, 2000);
        }
        catch
        {
            // non-fatal
        }
    }

    private bool IsEnergyConsumer(ScriptDefinition script)
    {
        var name = (script.Manifest.Name ?? script.DisplayName ?? string.Empty).ToLowerInvariant();
        return _energyKeywords.Any(k => name.Contains(k));
    }

    private bool IsBazaarScript(ScriptDefinition script)
    {
        var name = (script.Manifest.Name ?? script.DisplayName ?? string.Empty).ToLowerInvariant();
        return _bazaarKeywords.Any(k => name.Contains(k));
    }

    private PreflightResult PreflightResourceGuard(ScriptDefinition script)
    {
        var snapshot = _latestSnapshot;
        if (snapshot == null)
        {
            return PreflightResult.Allow();
        }

        if (IsEnergyConsumer(script) && GetEnergy(snapshot) <= EnergyMinThreshold)
        {
            return PreflightResult.Block("Not enough energy to start this task.");
        }

        if (IsBazaarScript(script) && snapshot.Gold?.Value is int gold)
        {
            if (gold <= GoldMinThreshold)
            {
                return PreflightResult.Block($"Gold too low for bazaar tasks (<= {GoldMinThreshold}).");
            }
            if (gold >= GoldCapThreshold)
            {
                return PreflightResult.Block($"Gold near cap ({gold}); pause bazaar/sell tasks to avoid loss.");
            }
        }

        if (snapshot.Health?.Current is int hp && hp <= HealthMinThreshold)
        {
            return PreflightResult.Block($"Health too low (<= {HealthMinThreshold}). Heal before starting.");
        }

        if (snapshot.Potions?.Value is int potions && potions <= PotionMinThreshold)
        {
            if (!_potionRunQueued)
            {
                _smartPlayManager?.EnqueuePotionRefillRun();
                _potionRunQueued = true;
                AddRunHistory($"Queued potion refill run before starting {script.Manifest.Name} at {DateTime.Now:T}");
            }
            return PreflightResult.Block("Potions low; refill queued. Retry after refill.", autoQueued: true);
        }

        return PreflightResult.Allow();
    }

    private static int GetEnergy(GameStateSnapshot snapshot)
    {
        return snapshot.Energy?.Current ?? int.MaxValue;
    }

    private void goBazaarButton_Click(object sender, EventArgs e)
    {
        if (_bridge != null)
        {
            _bridge.EnqueueNavigationToBazaar();
        }
        else
        {
            _smartPlayManager?.EnqueueNavigationToBazaar();
            AddRunHistory($"Queued travel: Bazaar at {DateTime.Now:T}");
        }
    }

    private void goMiniGamesButton_Click(object sender, EventArgs e)
    {
        if (_bridge != null)
        {
            _bridge.EnqueueNavigationToMiniGames();
        }
        else
        {
            _smartPlayManager?.EnqueueNavigationToMiniGames();
            AddRunHistory($"Queued travel: Mini Games at {DateTime.Now:T}");
        }
    }

    private void goPetPavilionButton_Click(object sender, EventArgs e)
    {
        if (_bridge != null)
        {
            _bridge.EnqueueNavigationToPetPavilion();
        }
        else
        {
            _smartPlayManager?.EnqueueNavigationToPetPavilion();
            AddRunHistory($"Queued travel: Pet Pavilion at {DateTime.Now:T}");
        }
    }

    private void potionRefillButton_Click(object sender, EventArgs e)
    {
        if (_bridge != null)
        {
            _bridge.EnqueuePotionRefill();
        }
        else
        {
            _smartPlayManager?.EnqueuePotionRefillRun();
            AddRunHistory($"Queued potion refill run at {DateTime.Now:T}");
        }
    }

    private void captureScreenButton_Click(object sender, EventArgs e)
    {
        var path = ScreenCaptureService.CaptureWizardWindow();
        if (!string.IsNullOrEmpty(path))
        {
            AddRunHistory($"Captured screen: {Path.GetFileName(path)}");
            MessageBox.Show($"Saved screenshot to:\n{path}", "Capture Complete", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show("Wizard101 window not found or capture failed.", "Capture Failed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void designManagerButton_Click(object sender, EventArgs e)
    {
        try
        {
            var dest = CaptureDesignSamples();
            AddRunHistory($"Design capture saved to {dest}");
            MessageBox.Show($"Captured UI snapshots to:\n{dest}", "Design Manager", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Design capture failed: {ex.Message}", "Design Manager", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void openDesignFolderButton_Click(object sender, EventArgs e)
    {
        try
        {
            var path = string.IsNullOrEmpty(_lastDesignCapturePath)
                ? StorageUtils.GetDesignSamplesPath()
                : _lastDesignCapturePath;
            if (!Directory.Exists(path))
            {
                path = StorageUtils.GetDesignSamplesPath();
            }
            System.Diagnostics.Process.Start("explorer.exe", path);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open design folder: {ex.Message}", "Design Manager", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void recordMacroButton_Click(object sender, EventArgs e)
    {
        using var dlg = new MacroRecorderForm
        {
            StartPosition = FormStartPosition.CenterParent,
            TopMost = true
        };
        dlg.ShowDialog(this);
    }

    private void runMacroButton_Click(object sender, EventArgs e)
    {
        void RunMacro(string path)
        {
            MacroPlayer.Play(path, _inputBridge, () => _latestSnapshot, StateManager.Instance.SelectedResolution);
        }

        using var dlg = new MacroRunnerForm(RunMacro)
        {
            StartPosition = FormStartPosition.CenterParent,
            TopMost = true
        };
        dlg.ShowDialog(this);
    }

    private void resetTuningButton_Click(object sender, EventArgs e)
    {
        try
        {
            _smartPlayManager?.ResetTuning();
            AddRunHistory("SmartPlay tuning reset");
            MessageBox.Show("SmartPlay tuning data cleared.", "Reset Tuning", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to reset tuning: {ex.Message}", "Reset Tuning", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void openLearnLogsButton_Click(object sender, EventArgs e)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "logs", "learn_mode");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open learn logs: {ex.Message}", "Learn Logs", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string CaptureDesignSamples()
    {
        string path = string.Empty;
        if (InvokeRequired)
        {
            Invoke(new Action(() =>
            {
                path = _designManager.CaptureAppWindows(this);
            }));
        }
        else
        {
            path = _designManager.CaptureAppWindows(this);
        }
        _lastDesignCapturePath = path;
        return path;
    }

    private void learnModeButton_Click(object sender, EventArgs e)
    {
        if (_learnModeService == null) return;
        if (_learnModeService.IsRunning)
        {
            _learnModeService.Stop();
            learnModeButton.Text = "Learn Mode";
            learnProfileStatusLabel.Text = "Learn Mode: Stopped";
        }
        else
        {
            _learnModeService.Start();
            learnModeButton.Text = "Stop Learn Mode";
            learnProfileStatusLabel.Text = "Learn Mode: Running";
        }
    }

    private void learnProfileCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_learnModeService == null) return;
        var selected = learnProfileCombo.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selected)) return;
        if (Enum.TryParse<LearnModeProfile>(selected, true, out var profile))
        {
            _learnModeService.SetProfile(profile);
            AddRunHistory($"Learn profile set to {profile}");
            UpdateLearnProfileStatus(profile.ToString());
            Properties.Settings.Default.LEARN_MODE_PROFILE = profile.ToString();
            Properties.Settings.Default.Save();
        }
    }

    private void SetLearnProfileCombo(LearnModeProfile profile)
    {
        var name = profile.ToString();
        for (int i = 0; i < learnProfileCombo.Items.Count; i++)
        {
            if (string.Equals(learnProfileCombo.Items[i]?.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                learnProfileCombo.SelectedIndex = i;
                return;
            }
        }
        learnProfileCombo.SelectedIndex = 0;
    }

    private void UpdateSmartPlayHeader(string state)
    {
        var q = _smartPlayManager?.QueueLength ?? 0;
        smartPlayHeaderLabel.Text = $"SmartPlay: {state} | Queue: {q}";
        smartPlayStatusLabel.Text = $"SmartPlay: {state} | Queue: {q}";
        UpdateGuardStatusLabel();
    }

    private void UpdateLearnProfileStatus(string profile)
    {
        learnProfileStatusLabel.Text = $"Learn Profile: {profile}";
    }

    private void UpdateAudioHeader(string state)
    {
        audioHeaderLabel.Text = $"Audio: {state}";
    }

    private void UpdateGuardStatusLabel()
    {
        if (guardStatusLabel == null) return;
        var pieces = new List<string>();
        if (_pauseForResource) pieces.Add("paused");
        if (_healthGuardTriggered) pieces.Add("health");
        if (_energyGuardTriggered) pieces.Add("energy");
        if (_goldGuardTriggered) pieces.Add("gold");
        if (_potionRunQueued) pieces.Add("potions");

        if (pieces.Count == 0)
        {
            guardStatusLabel.Text = "Guards: OK";
            guardStatusLabel.ForeColor = Color.LightGreen;
        }
        else
        {
            guardStatusLabel.Text = $"Guards: {string.Join(", ", pieces)}";
            guardStatusLabel.ForeColor = Color.Gold;
        }
    }

    private void UpdateKnowledgeInfo()
    {
        string info;
        if (_wikiData.HasData)
        {
            var topZones = _wikiData.GetZones().Take(3).ToArray();
            var zoneSnippet = topZones.Length > 0 ? $" | Top zones: {string.Join(", ", topZones)}" : string.Empty;
            info = $"WizWiki data loaded ({_wikiData.MobCount} mobs){zoneSnippet}";
            var topMobs = _wikiData.GetTopMobs(5).ToArray();
            var detailsParts = new List<string>();
            if (topZones.Length > 0)
            {
                detailsParts.Add($"Top zones: {string.Join(", ", topZones)}");
            }
            if (topMobs.Length > 0)
            {
                detailsParts.Add($"Top mobs: {string.Join(", ", topMobs)}");
            }
            knowledgeDetailsLabel.Text = detailsParts.Count > 0 ? string.Join(" | ", detailsParts) : string.Empty;
            _knowledgeLastRefreshed ??= DateTime.Now;
            knowledgeTimestampLabel.Text = $"Refreshed: {_knowledgeLastRefreshed:HH:mm:ss on MMM dd}";
            if (!string.IsNullOrWhiteSpace(knowledgeDetailsLabel.Text))
            {
                uiToolTip.SetToolTip(knowledgeDetailsLabel, knowledgeDetailsLabel.Text);
            }
            uiToolTip.SetToolTip(knowledgeTimestampLabel, knowledgeTimestampLabel.Text);
            uiToolTip.SetToolTip(refreshKnowledgeButton, "Reload WizWiki cache (top zones/mobs)");
        }
        else
        {
            info = "WizWiki data not found (optional cache for seek/avoid).";
            knowledgeDetailsLabel.Text = string.Empty;
            knowledgeTimestampLabel.Text = string.Empty;
            uiToolTip.SetToolTip(knowledgeDetailsLabel, string.Empty);
            uiToolTip.SetToolTip(knowledgeTimestampLabel, string.Empty);
            uiToolTip.SetToolTip(refreshKnowledgeButton, "Reload WizWiki cache (top zones/mobs)");
        }
        knowledgeStatusLabel.Text = $"Knowledge: {info}";

        if (!dashboardWarningsTextBox.Text.Contains(info, StringComparison.OrdinalIgnoreCase))
        {
            var prefix = string.IsNullOrWhiteSpace(dashboardWarningsTextBox.Text)
                ? info
                : info + Environment.NewLine + dashboardWarningsTextBox.Text;
            dashboardWarningsTextBox.Text = prefix;
        }

        snapshotWarningsTextBox.Text = $"Knowledge refreshed at {_knowledgeLastRefreshed:HH:mm:ss on MMM dd}";
    }

    private void refreshKnowledgeButton_Click(object sender, EventArgs e)
    {
        WizWikiDataService.Instance.Refresh();
        _knowledgeLastRefreshed = DateTime.Now;
        UpdateKnowledgeInfo();
        AddRunHistory("Knowledge refreshed");
        ShowKnowledgeToast("Knowledge refreshed");
    }

    private void speedNumeric_ValueChanged(object? sender, EventArgs e)
    {
        double speed = (double)speedNumeric.Value;
        Properties.Settings.Default.SPEED_MULTIPLIER = speed;
        Properties.Settings.Default.Save();
        UpdateSpeedLabel(speed);
        Logger.Log($"[Speed] Multiplier set to {speed:0.0}x");
    }

    private void UpdateSpeedLabel(double speed)
    {
        speedLabel.Text = $"Speed Multiplier [x {speed:0.0}]";
    }

    private void panicStopButton_Click(object sender, EventArgs e)
    {
        try
        {
            _scriptLibraryService.StopCurrentScript();
            _inputBridge?.Stop();
            _smartPlayManager?.ClearAllTasks();
            AddRunHistory($"Panic stop at {DateTime.Now:T}");
            UpdateScriptStatus();
            PopulateTrainerList();
            MessageBox.Show("All tasks stopped.", "Panic Stop", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Panic stop failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PopulateTrainerList()
    {
        trainerListView.BeginUpdate();
        trainerListView.Items.Clear();
        var scripts = _scriptLibraryService.Scripts.ToList();

        if (!string.IsNullOrWhiteSpace(_scriptSearchTerm))
        {
            var term = _scriptSearchTerm.Trim();
            scripts = scripts.Where(s =>
                (!string.IsNullOrWhiteSpace(s.Manifest.Name) && s.Manifest.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(s.DisplayName) && s.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(s.Manifest.Author) && s.Manifest.Author.Contains(term, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        for (int i = 0; i < scripts.Count; i++)
        {
            var script = scripts[i];
            var kind = GetScriptKind(script);
            var badge = string.IsNullOrWhiteSpace(kind) ? "native" : kind.ToLowerInvariant();

            if (Properties.Settings.Default.PLAYER_PREVIEW_MODE && IsHiddenInPlayerPreview(kind))
            {
                continue;
            }

            var kindLabel = badge switch
            {
                "external" => "External",
                "reference" => "Reference",
                "deprecated" => "Deprecated",
                _ => "Native"
            };

            string status;
            if (_scriptLibraryService.CurrentSession?.Script == script)
            {
                status = "Running";
            }
            else if (script.ValidationErrors.Any())
            {
                status = "Needs setup";
            }
            else
            {
                status = "Ready";
            }

            if (!string.Equals(_statusFilter, "All", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, _statusFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string issues = script.ValidationErrors.Any()
                ? string.Join("; ", script.ValidationErrors.Take(3)) + (script.ValidationErrors.Length > 3 ? " ..." : string.Empty)
                : "-";

            var item = new ListViewItem(script.Manifest.Name ?? script.DisplayName ?? "Script")
            {
                Tag = script
            };
            item.SubItems.Add(kindLabel);
            item.SubItems.Add(status);
            item.SubItems.Add(issues);
            item.SubItems.Add(GetLastRunDisplay(script.Manifest.Name ?? script.DisplayName ?? "script"));
            var author = script.Manifest.Author ?? "-";
            item.SubItems.Add(author);
            item.SubItems.Add(script.PackageInfo?.SourceUrl ?? script.Manifest.SourceUrl ?? "-");
            item.UseItemStyleForSubItems = false;
            ApplyTypeStyling(item, badge);
            if (i % 2 == 1)
            {
                item.BackColor = Color.FromArgb(26, 34, 58);
            }
            trainerListView.Items.Add(item);
        }

        trainerListView.EndUpdate();

        if (!trainerListView.Visible && _activeChildForm == null)
        {
            // If no child form is active, ensure the list is visible.
            childHostPanel.Visible = false;
            trainerListView.Visible = true;
        }

        UpdateFilterNote();
    }

    private void trainerListView_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (trainerListView.SelectedItems.Count == 0)
        {
            if (_activeChildForm == null)
            {
                trainerListView.Visible = true;
                childHostPanel.Visible = false;
            }
            return;
        }

        var selected = trainerListView.SelectedItems[0].Tag as ScriptDefinition;
        if (selected == null)
        {
            return;
        }
    }

    private void trainerListView_ColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (e.Column == _lastSortColumn)
        {
            _lastSortAsc = !_lastSortAsc;
        }
        else
        {
            _lastSortColumn = e.Column;
            _lastSortAsc = true;
        }

        trainerListView.ListViewItemSorter = new ListViewItemComparer(e.Column, _lastSortAsc);
        trainerListView.Sort();
    }

    private void TrainerListView_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (trainerListView.SelectedItems.Count == 0) return;
        if (trainerListView.SelectedItems[0].Tag is not ScriptDefinition selected) return;

        var url = selected.PackageInfo?.SourceUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show("No source URL recorded for this script. Open it via Manage Scripts to see more details.", "Source", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open source: {ex.Message}", "Source Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CheckUpdatesIfEnabledAsync()
    {
        try
        {
            if (!Properties.Settings.Default.AUTO_CHECK_UPDATES)
            {
                return;
            }

            var feed = Properties.Settings.Default.UPDATE_FEED_URL;
            if (string.IsNullOrWhiteSpace(feed))
            {
                return;
            }

            var manifest = await UpdaterService.Instance.CheckForUpdateAsync(feed);
            if (manifest?.Version == null)
            {
                return;
            }

            if (!Version.TryParse(Application.ProductVersion, out var current) ||
                !Version.TryParse(manifest.Version, out var latest))
            {
                return;
            }

            if (latest > current)
            {
                var msg = $"A new version is available.\nInstalled: {current}\nLatest: {latest}\n\nOpen Project Manager/Installer to update, or go to Settings > Updates.";
                MessageBox.Show(msg, "Update Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch
        {
            // silent failure; no blocking of startup
        }
    }

    private static string GetScriptKind(ScriptDefinition script)
    {
        if (!string.IsNullOrWhiteSpace(script.Manifest.Status))
        {
            return script.Manifest.Status;
        }

        if (IsReferenceName(script.Manifest.Name))
        {
            return "reference";
        }

        if (script.PackageInfo?.SourceUrl != null || !string.IsNullOrWhiteSpace(script.Manifest.SourceUrl))
        {
            return "external";
        }

        return "native";
    }

    private static bool IsReferenceName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var n = name.ToLowerInvariant();
        return n.Contains("wizwalker") ||
               n.Contains("wizsdk") ||
               n.Contains("wizproxy") ||
               n.Contains("wizwiki") ||
               n.Contains("wizwad") ||
               n.Contains("wiz-packet") ||
               n.Contains("wad-reader") ||
               n.Contains("proto") ||
               n.Contains("sample");
    }

    private static bool IsHiddenInPlayerPreview(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return false;
        var k = kind.ToLowerInvariant();
        return k.Contains("deprecated") || k.Contains("reference");
    }

    private void UpdateModeLabel()
    {
        var mode = Properties.Settings.Default.PLAYER_PREVIEW_MODE ? "Player" : (DevMode.IsEnabled ? "Dev" : "Standard");
        modeLabel.Text = $"Mode: {mode}";
    }

    private void UpdateFilterNote()
    {
        var chips = new List<string>();

        if (Properties.Settings.Default.PLAYER_PREVIEW_MODE)
        {
            chips.Add("Player mode");
        }

        if (!string.Equals(_statusFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            chips.Add($"Status: {_statusFilter}");
        }

        if (!string.IsNullOrWhiteSpace(_scriptSearchTerm))
        {
            chips.Add($"Search: \"{_scriptSearchTerm.Trim()}\"");
        }

        if (chips.Count > 0)
        {
            var details = string.Join(" · ", chips);
            filterNoteMainLabel.Text = $"{details} — showing {trainerListView.Items.Count} item(s)";
            filterNoteMainLabel.Visible = true;
            filterChipLabel.Text = details;
            filterChipLabel.Visible = true;
        }
        else
        {
            filterNoteMainLabel.Text = string.Empty;
            filterNoteMainLabel.Visible = false;
            filterChipLabel.Text = string.Empty;
            filterChipLabel.Visible = false;
        }
    }

    private void ApplyTypeStyling(ListViewItem item, string badge)
    {
        // Badge is already lowercase; apply subtle type color to the Type column.
        var (back, fore) = GetTypeColors(badge);

        if (item.SubItems.Count > 1)
        {
            item.SubItems[1].BackColor = back;
            item.SubItems[1].ForeColor = fore;
            item.SubItems[1].Font = new Font(item.Font, FontStyle.Bold);
        }
    }

    private static (Color back, Color fore) GetTypeColors(string badge)
    {
        return badge switch
        {
            "external" => (Color.FromArgb(24, 92, 96), Color.FromArgb(180, 255, 255)),
            "reference" => (Color.FromArgb(54, 54, 54), Color.Silver),
            "deprecated" => (Color.FromArgb(104, 52, 52), Color.MistyRose),
            _ => (Color.FromArgb(62, 74, 110), Color.Gold)
        };
    }

    private void ShowKnowledgeToast(string message)
    {
        try
        {
            uiToolTip.Show(message, refreshKnowledgeButton, 2000);
        }
        catch
        {
            // ToolTip failures are non-fatal; ignore.
        }
    }

}
