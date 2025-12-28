using ProjectMaelstrom.Models;
using ProjectMaelstrom.Utilities;
using Microsoft.VisualBasic;
using System.Threading.Tasks;

namespace ProjectMaelstrom;

public partial class ManageScriptsForm : Form
{
    private readonly ScriptLibraryService _service = ScriptLibraryService.Instance;

    public ManageScriptsForm()
    {
        InitializeComponent();
    }

    private void ManageScriptsForm_Load(object sender, EventArgs e)
    {
        ThemeManager.ApplyTheme(this);
        scriptListBox.SelectedIndexChanged += scriptListBox_SelectedIndexChanged;
        LoadScriptLibrary();
        UpdateScriptStatus();
    }

    private void LoadScriptLibrary()
    {
        _service.ReloadLibrary();
        var scripts = _service.Scripts.ToList();
        scriptListBox.DisplayMember = nameof(ScriptDefinition.DisplayName);
        scriptListBox.ValueMember = nameof(ScriptDefinition.Manifest);
        scriptListBox.DataSource = scripts;
    }

    private void runScriptButton_Click(object sender, EventArgs e)
    {
        if (scriptListBox.SelectedItem is not ScriptDefinition selected)
        {
            MessageBox.Show("Select a script to run.", "No Script Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (selected.ValidationErrors.Any())
        {
            MessageBox.Show(string.Join(Environment.NewLine, selected.ValidationErrors), "Script validation issues",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _service.StartScript(selected);
            UpdateScriptStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start script: {ex.Message}", "Script Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void stopScriptButton_Click(object sender, EventArgs e)
    {
        try
        {
            _service.StopCurrentScript();
            UpdateScriptStatus();
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
    }

    private async void importFromGithubButton_Click(object sender, EventArgs e)
    {
        string input = Interaction.InputBox(
            "Enter a GitHub repo URL (e.g., https://github.com/user/repo) or a direct .zip URL. Branch/tag optional (default: main).",
            "Add from GitHub",
            "https://github.com/user/repo");

        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        string? branch = Interaction.InputBox(
            "Optional: specify branch or tag (leave blank for main).",
            "Branch/Tag",
            string.Empty);

        Cursor = Cursors.WaitCursor;
        try
        {
            var imported = await _service.ImportFromGitHubAsync(input, string.IsNullOrWhiteSpace(branch) ? null : branch);
            LoadScriptLibrary();
            UpdateScriptStatus();

            if (imported != null)
            {
                scriptListBox.SelectedItem = _service.Scripts.FirstOrDefault(s => s.Manifest.Name == imported.Manifest.Name);
                MessageBox.Show($"Imported {imported.Manifest.Name}", "GitHub Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Import completed, but manifest was not found. Verify the downloaded folder contains manifest.json.", "GitHub Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"GitHub import failed: {ex.Message}", "GitHub Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void dryRunCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        _service.DryRun = dryRunCheckBox.Checked;
        Logger.Log($"[ScriptLibrary] DryRun set to {dryRunCheckBox.Checked}");
    }

    private void loadLogButton_Click(object sender, EventArgs e)
    {
        if (scriptListBox.SelectedItem is not ScriptDefinition selected)
        {
            MessageBox.Show("Select a script first.", "No Script Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string logPath = GetScriptLogPath(selected.Manifest.Name);
        if (!File.Exists(logPath))
        {
            logPreviewTextBox.Text = "No log found for this script.";
            return;
        }

        try
        {
            var lines = File.ReadAllLines(logPath);
            var lastLines = lines.Reverse().Take(50).Reverse();
            logPreviewTextBox.Text = string.Join(Environment.NewLine, lastLines);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load log: {ex.Message}", "Log Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void openScriptFolderButton_Click(object sender, EventArgs e)
    {
        if (scriptListBox.SelectedItem is not ScriptDefinition selected)
        {
            MessageBox.Show("Select a script first.", "No Script Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start("explorer.exe", selected.RootPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open folder: {ex.Message}", "Open Folder Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void openLibraryRootButton_Click(object sender, EventArgs e)
    {
        try
        {
            var path = StorageUtils.GetScriptLibraryPath();
            System.Diagnostics.Process.Start("explorer.exe", path);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open library: {ex.Message}", "Open Library Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void openFullLogButton_Click(object sender, EventArgs e)
    {
        if (scriptListBox.SelectedItem is not ScriptDefinition selected)
        {
            MessageBox.Show("Select a script first.", "No Script Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string logPath = GetScriptLogPath(selected.Manifest.Name);
        if (!File.Exists(logPath))
        {
            MessageBox.Show("Log file not found for this script.", "Missing Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = logPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open log: {ex.Message}", "Open Log Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void scriptListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateScriptStatus();
    }

    private void UpdateScriptStatus()
    {
        var running = _service.CurrentSession;
        if (running == null)
        {
            scriptStatusLabel.Text = "Status: Idle";
            return;
        }

        scriptStatusLabel.Text = $"Running: {running.Script.Manifest.Name}";
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
}
