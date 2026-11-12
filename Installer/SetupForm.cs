using System.Diagnostics;
using System.Reflection;

namespace Installer;

public sealed class SetupForm : Form
{
    private readonly bool _uninstall;
    private readonly InstallEngine _engine;
    private readonly Button _action = new() { AutoSize = true, Padding = new Padding(16, 8, 16, 8) };
    private readonly CheckBox _desktop = new() { Text = "Also add a desktop shortcut", AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(660, 0) };
    private readonly ProgressBar _progress = new() { Width = 650, Height = 14, Visible = false };
    private bool _busy, _finished;
    private InstallRecord? _installed;

    public SetupForm(bool uninstall)
    {
        _uninstall = uninstall;
        _engine = new InstallEngine(InstallEngine.DefaultRoot, InstallEngine.DefaultData, new WindowsRegistration(InstallEngine.DefaultRoot));
        Text = uninstall ? "Uninstall Maelstrom" : "Maelstrom Setup";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        Font = new Font("Segoe UI", 11);
        ClientSize = new Size(740, 570);
        MinimumSize = new Size(740, 570);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(30), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        void Label(string text, int size = 11, bool bold = false) => body.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(660, 0), Font = new Font(Font.FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(0, 0, 0, 14) });
        Label(uninstall ? "Remove the app. Keep your work." : "Welcome to Maelstrom", 24, true);
        Label(uninstall ? "Uninstall removes Maelstrom's recorded application files and Windows shortcuts. Your game packs and reports stay available for reinstalling later."
            : "Install the offline Game Pack Workshop for your Windows account. Python and .NET are included; no separate setup is needed.");
        Label("Application files\n" + _engine.Root);
        Label("Your packs and reports — kept during updates and uninstall\n" + _engine.DataRoot);
        if (!uninstall)
        {
            Label("A Start Menu shortcut and Windows uninstall entry are included.");
            body.Controls.Add(_desktop);
        }
        _action.Margin = new Padding(0, 16, 0, 12);
        _action.Click += async (_, _) => await PerformAsync();
        body.Controls.Add(_action);
        body.Controls.Add(_progress);
        body.Controls.Add(_status);
        var data = new LinkLabel { Text = "Open my packs and reports folder", AutoSize = true, Margin = new Padding(0, 16, 0, 0) };
        data.LinkClicked += (_, _) =>
        {
            try { Directory.CreateDirectory(_engine.DataRoot); Process.Start(new ProcessStartInfo(_engine.DataRoot) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
        };
        body.Controls.Add(data);
        if (!uninstall)
        {
            var import = new LinkLabel { Text = "Bring packs and reports from a portable copy…", AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            import.LinkClicked += async (_, _) =>
            {
                if (_busy) return;
                using var picker = new FolderBrowserDialog { Description = "Choose the portable Data folder. Existing installed versions will be kept.", UseDescriptionForTitle = true };
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                _busy = true; _action.Enabled = false; _status.Text = "Copying portable packs and reports…";
                try
                {
                    var result = await Task.Run(() => PortableDataImport.Copy(picker.SelectedPath, _engine.DataRoot));
                    _status.Text = $"Copied {result.PackVersions} pack versions and {result.Reports} reports. Skipped {result.Skipped} existing or incomplete items. Originals were kept.";
                }
                catch (Exception ex) { _status.Text = "Import stopped: " + ex.Message + " Previously copied items and originals were kept."; }
                finally { _busy = false; _action.Enabled = true; }
            };
            body.Controls.Add(import);
        }
        Controls.Add(body);
        FormClosing += (_, e) => { if (_busy) { e.Cancel = true; _status.Text = "Please wait until this operation finishes."; } };
        try
        {
            _installed = _engine.ReadInstalled();
            _desktop.Checked = _installed?.DesktopShortcut ?? false;
            _action.Text = uninstall ? "Uninstall app and keep my data" : _installed is not { Installed: true } ? "Install Maelstrom" : "Update Maelstrom";
            if (uninstall && _installed is not { Installed: true }) { _action.Enabled = false; _status.Text = "No installation was found. Any existing user data is untouched."; }
            else _status.Text = _installed is not { Installed: true } ? "Ready to install." : "Installed version: " + _installed.Version;
        }
        catch (Exception ex) { _action.Enabled = false; _status.Text = ex.Message; }
    }

    private async Task PerformAsync()
    {
        if (_finished)
        {
            if (!_uninstall && _installed != null)
                Process.Start(new ProcessStartInfo(Path.Combine(_engine.Root, _installed.CurrentDirectory, "Maelstrom.exe")) { UseShellExecute = true });
            Close();
            return;
        }
        _busy = true;
        _action.Enabled = false;
        _desktop.Enabled = false;
        _progress.Visible = true;
        _progress.Style = ProgressBarStyle.Marquee;
        var desktop = _desktop.Checked;
        var progress = new Progress<string>(text => _status.Text = text);
        try
        {
            if (_uninstall)
            {
                var remaining = await Task.Run(() => _engine.Uninstall(text => ((IProgress<string>)progress).Report(text)));
                _status.Text = remaining.Count == 0 ? "Maelstrom was removed. Your game packs and reports were kept."
                    : "Recorded application files were removed. Modified files were left in the application folder. Your game packs and reports were kept.";
                _action.Text = "Finish";
            }
            else
            {
                _installed = await Task.Run(() =>
                {
                    using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("Maelstrom.Payload.zip")
                        ?? throw new IOException("This setup build has no application package. Use the published MaelstromSetup.exe.");
                    return _engine.Install(payload, Environment.ProcessPath!, "0.2.0-preview", desktop, text => ((IProgress<string>)progress).Report(text));
                });
                _status.Text = "Maelstrom is ready. Your existing packs and reports remain in place.";
                _action.Text = "Open Maelstrom";
            }
            _finished = true;
        }
        catch (Exception ex) { _status.Text = "Could not finish: " + ex.Message + " You can resolve the issue and retry."; }
        finally { _busy = false; _action.Enabled = true; _desktop.Enabled = !_finished; _progress.Visible = false; }
    }
}
