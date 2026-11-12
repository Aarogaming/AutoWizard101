using System.Diagnostics;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Game-specific navigation over the existing Observe, Workshop and
/// experiment tools. Opening this workspace starts no capture, game or test.</summary>
internal sealed class DodgeWorkspaceForm : Form
{
    private readonly string _dataRoot;
    private readonly Action<bool>? _openObserve;
    private readonly Action? _openExperiments;
    private readonly Action? _openWorkshop;
    private readonly Action<string>? _openPack;
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(770, 0), Margin = new Padding(0, 16, 0, 10) };
    private GameWorkspaceForm? _observe;
    private ExperimentsForm? _experiments;

    public DodgeWorkspaceForm(string dataRoot, Action<bool>? openObserve = null,
        Action? openExperiments = null, Action? openWorkshop = null, Action<string>? openPack = null)
    {
        _dataRoot = Path.GetFullPath(dataRoot);
        _openObserve = openObserve;
        _openExperiments = openExperiments;
        _openWorkshop = openWorkshop;
        _openPack = openPack;
        Text = "Maelstrom · Dodge the Creeps";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        Size = new Size(960, 770);
        MinimumSize = new Size(860, 620);
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(24) };
        var title = new Label { Text = "Dodge the Creeps", AutoSize = true, Font = new Font(Font.FontFamily, 22, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) };
        body.Controls.Add(title);
        body.Controls.Add(Note("Game workspace  /  Observe → Build → Compare → Preserve\nChoose a module below. Each module keeps its own explicit Start and Stop controls."));

        FlowLayoutPanel Module(string heading, string description)
        {
            var section = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(820, 0), MinimumSize = new Size(790, 0), FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12), Margin = new Padding(0, 8, 0, 8), BorderStyle = BorderStyle.FixedSingle };
            section.Controls.Add(new Label { Text = heading, AutoSize = true, Font = new Font(Font.FontFamily, 12, FontStyle.Bold) });
            section.Controls.Add(Note(description));
            var buttons = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(760, 0), WrapContents = true };
            section.Controls.Add(buttons);
            body.Controls.Add(section);
            return buttons;
        }
        void Action(FlowLayoutPanel row, string label, Action action, bool enabled = true)
        {
            var button = new Button { Text = label, AutoSize = true, Enabled = enabled, Margin = new Padding(0, 4, 8, 4) };
            button.Click += (_, _) => { try { action(); } catch (Exception error) { _status.Text = error.Message; } };
            row.Controls.Add(button);
        }
        var observe = Module("Observe and record", "Select the local Dodge window, then start Observe. Record video, optional game inputs, and labeled examples using the existing sampler. Opening Observe does not begin capture.");
        Action(observe, "Open Observe and recording", () => OpenObserve(false));
        Action(observe, "Open recordings folder", () => OpenFolder(Path.Combine(_dataRoot, "recordings")));
        Action(observe, "Open video recordings", () => OpenFolder(Path.Combine(_dataRoot, "video-recordings")));
        var specialist = Module("Specialist and isolated tests", "Review lightweight movement advice in Observe. For play tests that leave your desktop alone, open the isolated visual test and start at 25% stepped speed. Headless tests check engine behavior; they do not verify vision.");
        Action(specialist, "Open specialist in Observe", () => OpenObserve(true));
        Action(specialist, "Local Dodge and test controls…", () =>
        {
            using var tests = new TestGamesForm(dodgeOnly: true);
            WorkspaceTheme.Apply(tests);
            tests.ShowDialog(this);
        });
        var experiments = Module("Experiments", "Compare a baseline and candidate across fresh games, inspect score distributions, and preserve the evidence. Use the existing Dodge comparison plan; one private game runs at a time.");
        Action(experiments, "Open comparison experiments", OpenExperiments);
        Action(experiments, "Open experiment evidence", () => OpenFolder(Path.Combine(_dataRoot, "experiments")));
        var packs = Module("Game packs and reports", "Build and review named actions and rules in Workshop. A saved pack is a draft, not proof of reliable play. Reports distinguish isolated vision, headless engine checks and desktop tests.");
        Action(packs, "Open Game Pack Workshop", () => _openWorkshop?.Invoke(), _openWorkshop != null);
        Action(packs, "Open a Dodge pack…", OpenDodgePack, _openPack != null);
        Action(packs, "Open saved packs", () => OpenFolder(Path.Combine(_dataRoot, "game-packs")));
        Action(packs, "Open desktop test reports", () => OpenFolder(Path.Combine(_dataRoot, "dodge-runs")));
        Action(packs, "Open headless test reports", () => OpenFolder(Path.Combine(_dataRoot, "dodge-native-runs")));
        Action(packs, "Open isolated visual reports", OpenIsolatedReports);
        _status.Text = "Nothing is running. Shared recording, pack and experiment folders stay in your existing Maelstrom data location.";
        body.Controls.Add(_status);
        Controls.Add(body);
        WorkspaceTheme.Apply(this);
        title.ForeColor = WorkspaceTheme.Palette.Accent;
    }

    private static Label Note(string text) => new() { Text = text, AutoSize = true, MaximumSize = new Size(750, 0), Margin = new Padding(0, 6, 0, 8) };

    private void OpenObserve(bool advice)
    {
        if (_openObserve != null) { _openObserve(advice); return; }
        if (_observe == null || _observe.IsDisposed) _observe = new GameWorkspaceForm(_openPack, _dataRoot);
        _observe.Show(this);
        _observe.PreferDodge(advice);
        WorkspaceTheme.Apply(_observe);
        _observe.Activate();
    }

    private void OpenExperiments()
    {
        if (_openExperiments != null) { _openExperiments(); return; }
        if (_experiments == null || _experiments.IsDisposed) _experiments = new ExperimentsForm(_dataRoot);
        WorkspaceTheme.Apply(_experiments);
        _experiments.Show(this);
        _experiments.Activate();
    }

    private void OpenDodgePack()
    {
        using var picker = new OpenFileDialog { Title = "Choose a Dodge game pack", Filter = "Game pack|*.json", InitialDirectory = Path.Combine(_dataRoot, "game-packs") };
        if (picker.ShowDialog(this) == DialogResult.OK) _openPack?.Invoke(GameWorkspaceCatalog.RequireDodgePack(picker.FileName));
    }

    private void OpenIsolatedReports()
    {
        var workspace = TestGameWorkspaceStore.Load(_dataRoot) ?? ToolIntegrationService.FindWorkspace(AppContext.BaseDirectory);
        if (workspace == null) throw new InvalidOperationException("Choose the AutoWizard101 folder in Local Dodge and test controls first.");
        OpenFolder(Path.Combine(workspace, ".local", "wsl-dodge-runs"));
    }

    private void OpenFolder(string path)
    {
        if (!Directory.Exists(path)) { _status.Text = "No saved items in this folder yet: " + path; return; }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        _status.Text = "Opened: " + path;
    }
}
