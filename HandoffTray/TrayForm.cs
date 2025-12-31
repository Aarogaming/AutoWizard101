using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HandoffTray;

public class TrayForm : Form
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _modeOff;
    private readonly ToolStripMenuItem _modeTo;
    private readonly ToolStripMenuItem _modeFrom;
    private readonly ToolStripMenuItem _modeBoth;
    private readonly ToolStripMenuItem _toggleApi;
    private readonly ToolStripMenuItem _statusItem;

    private readonly HandoffWatcher _watcher;
    private readonly ApiSender _apiSender = new();
    private readonly LogHelper _log;
    private readonly string _root;
    private bool _apiSendEnabled;

    public TrayForm()
    {
        _root = Directory.GetCurrentDirectory();
        _log = new LogHelper(_root);
        var toDir = Path.Combine(_root, "artifacts", "handoff", "to_codex");
        var fromDir = Path.Combine(_root, "artifacts", "handoff", "from_codex");
        Directory.CreateDirectory(toDir);
        Directory.CreateDirectory(fromDir);

        _watcher = new HandoffWatcher(toDir, fromDir);
        _watcher.PromptDetected += OnPromptDetected;
        _watcher.ResultDetected += OnResultDetected;

        _menu = new ContextMenuStrip();
        _modeOff = new ToolStripMenuItem("Mode: Off", null, (_, _) => SetMode(Mode.Off)) { Checked = true };
        _modeTo = new ToolStripMenuItem("Mode: Watch To-Codex", null, (_, _) => SetMode(Mode.To));
        _modeFrom = new ToolStripMenuItem("Mode: Watch From-Codex", null, (_, _) => SetMode(Mode.From));
        _modeBoth = new ToolStripMenuItem("Mode: Both", null, (_, _) => SetMode(Mode.Both));
        _toggleApi = new ToolStripMenuItem("Enable API send (default OFF)", null, OnToggleApi)
        {
            Checked = false,
            CheckOnClick = true
        };
        _statusItem = new ToolStripMenuItem("Status: (not checked)");

        _menu.Items.AddRange(new ToolStripItem[]
        {
            _modeOff, _modeTo, _modeFrom, _modeBoth,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Copy latest prompt", null, (_, _) => CopyLatestPrompt()),
            new ToolStripMenuItem("Run handoff import", null, async (_, _) => await RunImportAsync()),
            new ToolStripMenuItem("Send latest prompt via API", null, async (_, _) => await SendLatestPromptAsync()),
            new ToolStripMenuItem("Open reports folder", null, (_, _) => OpenReports()),
            new ToolStripSeparator(),
            _toggleApi,
            new ToolStripMenuItem("Check server status", null, async (_, _) => await CheckStatusAsync()),
            _statusItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication())
        });

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = _menu,
            Text = "HandoffTray (default OFF)"
        };

        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Visible = false;
    }

    private void ExitApplication()
    {
        _notifyIcon.Visible = false;
        _watcher.Dispose();
        Application.Exit();
    }

    private void SetMode(Mode mode)
    {
        _modeOff.Checked = mode == Mode.Off;
        _modeTo.Checked = mode == Mode.To;
        _modeFrom.Checked = mode == Mode.From;
        _modeBoth.Checked = mode == Mode.Both;

        var watchTo = mode is Mode.To or Mode.Both;
        var watchFrom = mode is Mode.From or Mode.Both;
        _watcher.SetEnabled(watchTo, watchFrom);
        _notifyIcon.Text = $"HandoffTray ({mode})";
        _log.Info($"Mode set to {mode}");
    }

    private void OnPromptDetected(object? sender, string path)
    {
        _notifyIcon.BalloonTipTitle = "HandoffTray";
        _notifyIcon.BalloonTipText = "New HANDOFF_TO_CODEX detected. Use tray menu to copy or send.";
        _notifyIcon.ShowBalloonTip(3000);
        _log.Info($"Prompt detected: {path}");
    }

    private void OnResultDetected(object? sender, string path)
    {
        _notifyIcon.BalloonTipTitle = "HandoffTray";
        _notifyIcon.BalloonTipText = "New RESULT.md detected. Run handoff import from tray menu.";
        _notifyIcon.ShowBalloonTip(3000);
        _log.Info($"Result detected: {path}");
    }

    private string ReportsDir => Path.Combine(_root, "artifacts", "handoff", "reports");

    private string? GetLatestFile(string dir)
    {
        if (!Directory.Exists(dir)) return null;
        var info = new DirectoryInfo(dir);
        return info.GetFiles("*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    private void CopyLatestPrompt()
    {
        var latest = GetLatestFile(Path.Combine(_root, "artifacts", "handoff", "to_codex"));
        if (latest == null)
        {
            MessageBox.Show("No prompt found in to_codex.", "HandoffTray", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var text = File.ReadAllText(latest, Encoding.UTF8);
        Clipboard.SetText(text);
        MessageBox.Show("Copied latest HANDOFF_TO_CODEX to clipboard.", "HandoffTray", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _log.Info($"Copied prompt: {latest}");
    }

    private async Task RunImportAsync()
    {
        try
        {
            Directory.CreateDirectory(ReportsDir);
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project MaelstromToolkit/MaelstromToolkit.csproj -- handoff import --out artifacts/handoff/reports",
                WorkingDirectory = _root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null)
            {
                MessageBox.Show("Failed to start toolkit import.", "HandoffTray", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _log.Error("Toolkit import failed to start.");
                return;
            }
            await proc.WaitForExitAsync();
            var output = await proc.StandardOutput.ReadToEndAsync();
            var err = await proc.StandardError.ReadToEndAsync();
            if (proc.ExitCode != 0)
            {
                MessageBox.Show($"Import failed (exit {proc.ExitCode}).\n{output}\n{err}", "HandoffTray", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _log.Error($"Import failed (exit {proc.ExitCode}). Output: {output} Err: {err}");
                return;
            }
            MessageBox.Show("Import completed. Check CODEX_REPORT.md in reports.", "HandoffTray", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _log.Info("Import completed successfully.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "HandoffTray", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _log.Error($"Import exception: {ex}");
        }
    }

    private async Task SendLatestPromptAsync()
    {
        if (!_apiSendEnabled)
        {
            MessageBox.Show("API send is disabled. Toggle it on first.", "HandoffTray", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var latest = GetLatestFile(Path.Combine(_root, "artifacts", "handoff", "to_codex"));
        if (latest == null)
        {
            MessageBox.Show("No prompt found in to_codex.", "HandoffTray", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var text = File.ReadAllText(latest, Encoding.UTF8);
        var confirm = MessageBox.Show("Send the latest fenced block to OpenAI API?", "HandoffTray", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        try
        {
            var result = await _apiSender.SendAsync(text, "gpt-4o-mini", ReportsDir);
            if (result.Success)
            {
                MessageBox.Show(result.Message, "HandoffTray", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _log.Info($"API send success: {result.OutputPath}");
            }
            else
            {
                MessageBox.Show(result.Message, "HandoffTray", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _log.Error($"API send failed: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "HandoffTray", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _log.Error($"API send exception: {ex}");
        }
    }

    private void OpenReports()
    {
        Directory.CreateDirectory(ReportsDir);
        Process.Start(new ProcessStartInfo
        {
            FileName = ReportsDir,
            UseShellExecute = true
        });
    }

    private void OnToggleApi(object? sender, EventArgs e)
    {
        _apiSendEnabled = _toggleApi.Checked;
        _notifyIcon.BalloonTipTitle = "HandoffTray";
        _notifyIcon.BalloonTipText = _apiSendEnabled ? "API send ENABLED (default was off)." : "API send DISABLED.";
        _notifyIcon.ShowBalloonTip(2000);
        _log.Info($"API send enabled: {_apiSendEnabled}");
    }

    private async Task CheckStatusAsync()
    {
        try
        {
            var http = new HttpClient();
            var uri = new Uri("http://127.0.0.1:9411/api/status");
            var resp = await http.GetAsync(uri);
            if (!resp.IsSuccessStatusCode)
            {
                _statusItem.Text = $"Status: {resp.StatusCode}";
                return;
            }
            var json = await resp.Content.ReadAsStringAsync();
            _statusItem.Text = $"Status: OK ({json})";
        }
        catch (Exception ex)
        {
            _statusItem.Text = $"Status error";
            _log.Error($"Status check failed: {ex}");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Dispose();
            _watcher.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal enum Mode
{
    Off,
    To,
    From,
    Both
}
