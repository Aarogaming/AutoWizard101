using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Text;
using Installer.Models;

namespace Installer;

public partial class InstallerForm : Form
{
    private const string DefaultFeedUrl = "https://raw.githubusercontent.com/aarog/AutoWizard101/main/update_manifest.json";
    private readonly string? _feedUrlFromArgs;
    private string _installPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectMaelstrom");
    private string _logPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectMaelstrom", "logs", "installer.log");
    private string _desktop => Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    private string _startMenu => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Project Maelstrom");
    private string _latestVersion = "Unknown";
    private string _installedVersion = "Unknown";

    public InstallerForm(string? feedUrl)
    {
        _feedUrlFromArgs = feedUrl;
        InitializeComponent();
    }

    private void InstallerForm_Load(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_feedUrlFromArgs))
        {
            feedText.Text = _feedUrlFromArgs;
        }
        else
        {
            feedText.Text = DefaultFeedUrl;
        }
        DetectInstalled();
    }

    private void DetectInstalled()
    {
        var exe = Path.Combine(_installPath, "ProjectMaelstrom.exe");
        if (File.Exists(exe))
        {
            var info = FileVersionInfo.GetVersionInfo(exe);
            _installedVersion = info.FileVersion ?? "Unknown";
            installedVersionLabel.Text = $"Installed: {_installedVersion}";
        }
        else
        {
            installedVersionLabel.Text = "Installed: Not found";
            _installedVersion = "Not found";
        }
    }

    private async void checkButton_Click(object sender, EventArgs e)
    {
        var url = feedText.Text;
        if (string.IsNullOrWhiteSpace(url))
        {
            statusLabel.Text = "Status: No feed provided.";
            LogError("No feed provided for update check.");
            return;
        }

        LogStep("Checking feed...");
        try
        {
            progressBar.Style = ProgressBarStyle.Marquee;
            statusLabel.Text = "Status: Checking feed...";
            using var client = new HttpClient();
            var json = await client.GetStringAsync(url);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            _latestVersion = manifest?.Version ?? "Unknown";
            latestVersionLabel.Text = $"Latest: {_latestVersion}";
            statusLabel.Text = "Status: Check complete.";
            LogStep($"Latest version: {_latestVersion}");
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Status: Check failed ({ex.Message})";
            LogStep($"Check failed: {ex.Message}");
            LogError($"Check failed: {ex}");
        }
        finally
        {
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 0;
        }
    }

    private async void installButton_Click(object sender, EventArgs e)
    {
        await InstallOrUpdateAsync(force: true);
    }

    private async void updateButton_Click(object sender, EventArgs e)
    {
        await InstallOrUpdateAsync(force: false);
    }

    private async Task InstallOrUpdateAsync(bool force)
    {
        ToggleButtons(false);
        progressBar.Style = ProgressBarStyle.Marquee;
        statusLabel.Text = force ? "Status: Fresh install..." : "Status: Checking for update...";
        LogStep(force ? "Starting fresh install..." : "Starting update...");
        try
        {
            var payload = await TryDownloadLatest(feedText.Text) ?? ExtractEmbeddedPayload();
            if (payload == null)
            {
                statusLabel.Text = "Status: No payload available.";
                LogStep("No payload available.");
                LogError("No payload available for install/update.");
                return;
            }

            if (!force && IsUpToDate())
            {
                statusLabel.Text = "Status: Already up to date.";
                LogStep("No update needed; versions match.");
                return;
            }

            if (Directory.Exists(_installPath))
            {
                Directory.Delete(_installPath, recursive: true);
            }
            Directory.CreateDirectory(_installPath);
            LogStep("Extracting package...");
            ZipFile.ExtractToDirectory(payload, _installPath);
            CreateShortcut(Path.Combine(_desktop, "Project Maelstrom.lnk"));
            Directory.CreateDirectory(_startMenu);
            CreateShortcut(Path.Combine(_startMenu, "Project Maelstrom.lnk"));
            CreateUninstallScript();
            statusLabel.Text = "Status: Install complete.";
            LogStep("Install complete.");
            DetectInstalled();
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Status: Install failed ({ex.Message})";
            LogStep($"Install failed: {ex.Message}");
            LogError($"Install failed: {ex}");
        }
        finally
        {
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 100;
            ToggleButtons(true);
        }
    }

    private async void createPortableButton_Click(object sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "Zip Files|*.zip",
            FileName = "ProjectMaelstrom-portable.zip",
            OverwritePrompt = true
        };

        if (sfd.ShowDialog() != DialogResult.OK) return;

        ToggleButtons(false);
        progressBar.Style = ProgressBarStyle.Marquee;
        statusLabel.Text = "Status: Creating portable package...";
        LogStep("Creating portable package...");
        try
        {
            var payload = await TryDownloadLatest(feedText.Text) ?? ExtractEmbeddedPayload();
            if (payload == null)
            {
                statusLabel.Text = "Status: No payload available.";
                LogStep("No payload available for portable.");
                LogError("No payload available for portable.");
                return;
            }

            File.Copy(payload, sfd.FileName, overwrite: true);
            using (var archive = ZipFile.Open(sfd.FileName, ZipArchiveMode.Update))
            {
                var entry = archive.GetEntry("portable_mode.txt");
                entry?.Delete();
                var newEntry = archive.CreateEntry("portable_mode.txt");
                using var writer = new StreamWriter(newEntry.Open());
                writer.WriteLine("portable");
            }

            statusLabel.Text = "Status: Portable package created.";
            LogStep("Portable package created.");
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Status: Portable creation failed ({ex.Message})";
            LogStep($"Portable failed: {ex.Message}");
            LogError($"Portable creation failed: {ex}");
        }
        finally
        {
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 100;
            ToggleButtons(true);
        }
    }

    private void uninstallButton_Click(object sender, EventArgs e)
    {
        ToggleButtons(false);
        progressBar.Style = ProgressBarStyle.Marquee;
        try
        {
            var desktopShortcut = Path.Combine(_desktop, "Project Maelstrom.lnk");
            var startShortcut = Path.Combine(_startMenu, "Project Maelstrom.lnk");
            var startUninstall = Path.Combine(_startMenu, "Uninstall Project Maelstrom.lnk");
            if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);
            if (File.Exists(startShortcut)) File.Delete(startShortcut);
            if (File.Exists(startUninstall)) File.Delete(startUninstall);
            if (Directory.Exists(_installPath)) Directory.Delete(_installPath, recursive: true);
            statusLabel.Text = "Status: Uninstalled.";
            installedVersionLabel.Text = "Installed: Not found";
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Status: Uninstall failed ({ex.Message})";
            LogError($"Uninstall failed: {ex}");
        }
        finally
        {
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 0;
            ToggleButtons(true);
        }
    }

    private void ToggleButtons(bool enabled)
    {
        installButton.Enabled = enabled;
        uninstallButton.Enabled = enabled;
        checkButton.Enabled = enabled;
        updateButton.Enabled = enabled;
        createPortableButton.Enabled = enabled;
    }

    private async Task<string?> TryDownloadLatest(string? feedUrl)
    {
        if (string.IsNullOrWhiteSpace(feedUrl)) return null;
        try
        {
            using var client = new HttpClient();
            var json = await client.GetStringAsync(feedUrl);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.PackageUrl))
            {
                LogStep("Manifest missing packageUrl.");
                return null;
            }
            var tempZip = Path.Combine(Path.GetTempPath(), $"ProjectMaelstrom_latest_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
            var data = await client.GetByteArrayAsync(manifest.PackageUrl);
            await File.WriteAllBytesAsync(tempZip, data);
            _latestVersion = manifest.Version ?? "Unknown";
            latestVersionLabel.Text = $"Latest: {_latestVersion}";
            LogStep($"Downloaded latest package {_latestVersion}");
            return tempZip;
        }
        catch (Exception ex)
        {
            LogError($"Download latest failed: {ex}");
            return null;
        }
    }

    private string? ExtractEmbeddedPayload()
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("ProjectMaelstrom-win-x64.zip", StringComparison.OrdinalIgnoreCase));
        if (resourceName == null) return null;
        var tempZip = Path.Combine(Path.GetTempPath(), "ProjectMaelstrom-win-x64.zip");
        using var resStream = asm.GetManifestResourceStream(resourceName);
        using var fs = File.Create(tempZip);
        resStream!.CopyTo(fs);
        _latestVersion = "Embedded";
        latestVersionLabel.Text = $"Latest: {_latestVersion}";
        LogStep("Using embedded package.");
        return tempZip;
    }

    private void CreateShortcut(string shortcutPath)
    {
        try
        {
            var exePath = Path.Combine(_installPath, "ProjectMaelstrom.exe");
            IShellLinkW shellLink = (IShellLinkW)new ShellLink();
            shellLink.SetPath(exePath);
            shellLink.SetWorkingDirectory(_installPath);
            shellLink.SetIconLocation(exePath, 0);
            IPersistFile persistFile = (IPersistFile)shellLink;
            persistFile.Save(shortcutPath, false);
        }
        catch { /* best effort */ }
    }

    private void CreateUninstallScript()
    {
        try
        {
            var scriptPath = Path.Combine(_installPath, "uninstall_maelstrom.ps1");
            var desktopShortcut = Path.Combine(_desktop, "Project Maelstrom.lnk");
            var startShortcut = Path.Combine(_startMenu, "Project Maelstrom.lnk");
            var startUninstall = Path.Combine(_startMenu, "Uninstall Project Maelstrom.lnk");
            var script = $@"
$ErrorActionPreference = 'SilentlyContinue'
Remove-Item -Path '{desktopShortcut}' -ErrorAction SilentlyContinue
Remove-Item -Path '{startShortcut}' -ErrorAction SilentlyContinue
Remove-Item -Path '{startUninstall}' -ErrorAction SilentlyContinue
if (Test-Path '{_installPath}') {{
    Remove-Item -Recurse -Force '{_installPath}'
}}
Write-Host 'Project Maelstrom removed.'
";
            File.WriteAllText(scriptPath, script);
            CreateScriptShortcut(Path.Combine(_startMenu, "Uninstall Project Maelstrom.lnk"), scriptPath, "Uninstall Project Maelstrom");
        }
        catch { /* ignore */ }
    }

    private static void CreateScriptShortcut(string shortcutPath, string scriptPath, string title)
    {
        try
        {
            IShellLinkW shellLink = (IShellLinkW)new ShellLink();
            shellLink.SetPath("powershell.exe");
            shellLink.SetArguments($"-NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"");
            shellLink.SetDescription(title);
            shellLink.SetWorkingDirectory(Path.GetDirectoryName(scriptPath)!);
            IPersistFile persistFile = (IPersistFile)shellLink;
            persistFile.Save(shortcutPath, false);
        }
        catch { /* ignore */ }
    }

    #region COM Interop
    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    private bool IsUpToDate()
    {
        if (string.IsNullOrWhiteSpace(_installedVersion) || string.IsNullOrWhiteSpace(_latestVersion))
            return false;

        if (Version.TryParse(_installedVersion, out var installed) && Version.TryParse(_latestVersion, out var latest))
        {
            return installed >= latest;
        }

        return string.Equals(_installedVersion, _latestVersion, StringComparison.OrdinalIgnoreCase);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
    #endregion

    private void LogStep(string message)
    {
        if (activityList.Items.Count > 200) activityList.Items.Clear();
        activityList.Items.Add($"{DateTime.Now:T} {message}");
        activityList.TopIndex = activityList.Items.Count - 1;
        stepLabel.Text = $"Step: {message}";
    }

    private void LogError(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(_logPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir!);
            }
            File.AppendAllText(_logPath, $"{DateTime.Now:u} {message}{Environment.NewLine}");
        }
        catch
        {
            // best effort logging
        }
    }
}
