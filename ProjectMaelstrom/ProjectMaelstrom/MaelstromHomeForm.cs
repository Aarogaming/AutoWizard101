using System.Diagnostics;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Windows entry point for the player and authoring tools.</summary>
internal sealed class MaelstromHomeForm : Form
{
    private Process? _workshop;
    private OwnedProcessJob? _job;
    private GameWorkspaceForm? _workspace;
    private ExperimentsForm? _experiments;
    private HomeMovementForm? _homeMovement;
    private DodgeWorkspaceForm? _dodgeWorkspace;
    private JobDeckForm? _deck;
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(680, 0) };
    private readonly Button _open = new() { Text = "Open Game Pack Workshop", AutoSize = true, Padding = new Padding(16, 10, 16, 10) };

    public MaelstromHomeForm()
    {
        Text = "Maelstrom";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(800, 520);
        MinimumSize = new Size(740, 480);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 11);
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(32) };
        body.Controls.Add(new Label { Text = "Maelstrom", AutoSize = true, Font = new Font(Font.FontFamily, 28, FontStyle.Bold) });
        body.Controls.Add(new Label { Text = "Desktop game player · Game Pack Workshop", AutoSize = true, Margin = new Padding(0, 8, 0, 20) });
        body.Controls.Add(new Label { Text = "Build draft rules, save versions, and review offline recordings.\nStart with the included keyboard and pointer samples.", AutoSize = true, Margin = new Padding(0, 0, 0, 20) });
        _open.Click += (_, _) => OpenWorkshop();
        var workspace = new Button { Text = "Open game workspace · Observe", AutoSize = true, Padding = new Padding(16, 10, 16, 10) };
        workspace.Click += (_, _) =>
        {
            if (_workspace == null || _workspace.IsDisposed) _workspace = new GameWorkspaceForm(path => OpenWorkshop(path), openDodgeReplay: session => OpenWorkshop(dodgeSession: session));
            _workspace.Show(this);
            _workspace.Activate();
        };
        body.Controls.Add(workspace);
        body.Controls.Add(_open);
        var experiments = new Button { Text = "Experiments · compare players and review footage", AutoSize = true, Padding = new Padding(8) };
        experiments.Click += (_, _) =>
        {
            if (_experiments == null || _experiments.IsDisposed) _experiments = new ExperimentsForm();
            _experiments.Show(this);
            _experiments.Activate();
        };
        body.Controls.Add(experiments);
        var homeMovement = new Button { Text = "Wizard101 · routines and Auto-Idle", AutoSize = true, Padding = new Padding(8) };
        homeMovement.Click += (_, _) =>
        {
            if (_homeMovement == null || _homeMovement.IsDisposed) _homeMovement = new HomeMovementForm(openPack: path => OpenWorkshop(path),
                openObserve: () => { workspace.PerformClick(); _workspace?.PreferWizard101(); },
                beforeDancePlayer: () => _workspace != null && !_workspace.IsDisposed ? _workspace.PreparePetDancePlayerAsync() : Task.CompletedTask);
            _homeMovement.Show(this); _homeMovement.Activate();
        };
        body.Controls.Add(homeMovement);
        var dodgeWorkspace = new Button { Text = "Dodge the Creeps · game workspace", AutoSize = true, Padding = new Padding(8) };
        dodgeWorkspace.Click += (_, _) =>
        {
            if (_dodgeWorkspace == null || _dodgeWorkspace.IsDisposed)
                _dodgeWorkspace = new DodgeWorkspaceForm(WorkshopHost.DataRoot(AppContext.BaseDirectory),
                    openObserve: advice =>
                    {
                        workspace.PerformClick();
                        if (_workspace != null) { _workspace.PreferDodge(advice); WorkspaceTheme.Apply(_workspace); }
                    },
                    openExperiments: () => { experiments.PerformClick(); if (_experiments != null) WorkspaceTheme.Apply(_experiments); },
                    openWorkshop: () => OpenWorkshop(), openPack: path => OpenWorkshop(path));
            _dodgeWorkspace.Show(this);
            _dodgeWorkspace.Activate();
        };
        body.Controls.Add(dodgeWorkspace);
        var deck = new Button { Text = "Wizard101 · Card collection & Routine Deck", AutoSize = true, Padding = new Padding(16, 10, 16, 10) };
        deck.Click += (_, _) =>
        {
            try
            {
                if (_deck == null || _deck.IsDisposed) _deck = new JobDeckForm(ApplicationJobDeck.For(WorkshopHost.DataRoot(AppContext.BaseDirectory)));
                _deck.Show(this); _deck.Activate();
            }
            catch (Exception ex) { ShowError(ex.Message); }
        };
        body.Controls.Add(deck);
        var data = new Button { Text = "Open packs and reports folder", AutoSize = true, Padding = new Padding(8) };
        data.Click += (_, _) =>
        {
            try
            {
                var path = WorkshopHost.BuildStartInfo(AppContext.BaseDirectory).WorkingDirectory;
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { ShowError(ex.Message); }
        };
        body.Controls.Add(data);
        var settings = new Button { Text = "Settings", AutoSize = true, Padding = new Padding(8) };
        settings.Click += (_, _) => { using var form = new ResearchSettingsForm(WorkshopHost.DataRoot(AppContext.BaseDirectory)); form.ShowDialog(this); };
        body.Controls.Add(settings);
        if (ToolIntegrationService.FindWorkspace(AppContext.BaseDirectory) != null)
        {
            var tools = new Button { Text = "Development tools", AutoSize = true, Padding = new Padding(8) };
            tools.Click += (_, _) => new ToolCenterForm().Show(this);
            body.Controls.Add(tools);
        }
        _status.Text = File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.mode"))
            ? "Portable edition • your packs and reports are stored in the Data folder."
            : "Observe a game window or build packs offline. Gameplay control is still in development.";
        _status.Margin = new Padding(0, 20, 0, 0);
        body.Controls.Add(_status);
        Controls.Add(body);
        WorkspaceTheme.Apply(this);
        var closingHome = false;
        FormClosing += async (_, e) =>
        {
            if (_workshop is { HasExited: false })
            {
                e.Cancel = true;
                ShowError("Close the Workshop window first so you can save any draft changes.");
                return;
            }
            if (_homeMovement != null && !_homeMovement.IsDisposed)
            {
                e.Cancel = true; if (closingHome) return; closingHome = true;
                await _homeMovement.StopForOwnerCloseAsync(); _homeMovement.Close(); _homeMovement = null; Close();
            }
        };
        FormClosed += (_, _) => { _job?.Dispose(); _workshop?.Dispose(); };
    }

    internal void OpenWorkshop(string? packPath = null, string? dodgeSession = null)
    {
        if (_workshop is { HasExited: false })
        {
            _status.Text = dodgeSession == null
                ? "The Workshop is already open. Switch to its window to continue."
                : $"The Workshop is already open. Choose Dodge sample there and browse to this replay: {dodgeSession}";
            return;
        }
        Process? process = null;
        OwnedProcessJob? job = null;
        var started = false;
        try
        {
            process = new Process { StartInfo = WorkshopHost.BuildStartInfo(AppContext.BaseDirectory) };
            if (dodgeSession != null)
            {
                process.StartInfo.ArgumentList.Add("--dodge-session");
                process.StartInfo.ArgumentList.Add(dodgeSession);
            }
            if (packPath != null)
            {
                process.StartInfo.ArgumentList.Add("--pack");
                process.StartInfo.ArgumentList.Add(packPath);
                process.StartInfo.ArgumentList.Add("--library");
                process.StartInfo.ArgumentList.Add(Path.Combine(WorkshopHost.DataRoot(AppContext.BaseDirectory), "game-packs"));
            }
            job = new OwnedProcessJob();
            started = process.Start();
            if (!started) throw new InvalidOperationException("The Workshop could not start.");
            job.Attach(process);
            _workshop?.Dispose();
            _job?.Dispose();
            _workshop = process;
            _job = job;
            _open.Enabled = false;
            _status.Text = "Workshop is open. Your drafts stay local.";
            _ = ObserveWorkshopAsync(process, job);
        }
        catch (Exception ex)
        {
            if (started && process is { HasExited: false }) process.Kill(entireProcessTree: true);
            job?.Dispose();
            process?.Dispose();
            ShowError(ex.Message);
        }
    }

    private async Task ObserveWorkshopAsync(Process process, OwnedProcessJob job)
    {
        // Drain both streams so a helper cannot block on a full pipe.
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await output;
        var error = await errors;
        job.Dispose();
        if (IsDisposed) return;
        _open.Enabled = true;
        _status.Text = process.ExitCode == 0 ? "Workshop closed. Your saved versions are ready for next time." : "Workshop could not complete. " + error[..Math.Min(error.Length, 500)];
    }

    private void ShowError(string message)
    {
        _status.Text = message;
        MessageBox.Show(this, message, "Maelstrom", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
