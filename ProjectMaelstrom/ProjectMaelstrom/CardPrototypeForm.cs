using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Native offline prototype editor; no host, scheduler or live input path.</summary>
internal sealed class CardPrototypeForm : Form
{
    private readonly string _dataRoot;
    private readonly string _gameId;
    private readonly string _library;
    private readonly Dictionary<string, JobRoutine> _routines = JobDeckRoutines.Create().ToDictionary(routine => routine.Info.Id, StringComparer.Ordinal);
    private readonly FlowLayoutPanel _toolbar = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new(10), WrapContents = true };
    private readonly FlowLayoutPanel _preview = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly CardChoiceButton _actions = new() { Width = 260 };
    private readonly JobRoutineInfo[] _actionChoices;
    private readonly ListBox _history = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private readonly TextBox _status = new() { Dock = DockStyle.Bottom, Height = 125, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly FlowLayoutPanel _historyTools = new() { Dock = DockStyle.Bottom, Height = 50, Padding = new(5) };
    private CardPrototypeDocument _document;
    private JobCardView? _view;
    private bool _dirty, _busy;
    private readonly Button _test = new() { Text = "Test configured action", AutoSize = true };

    internal CardPrototypeForm(string dataRoot, string gameId = "wizard101")
    {
        _dataRoot = Path.GetFullPath(dataRoot); _gameId = gameId; _library = Path.Combine(_dataRoot, "card-prototypes");
        Text = "Card Workshop · create and test a prototype";
        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea.Size;
        Size = FitToWorkingArea(workingArea);
        MinimumSize = new(Math.Min(920, Width), Math.Min(620, Height));
        StartPosition = FormStartPosition.CenterParent;
        _actionChoices = _routines.Values.Select(routine => routine.Info).Where(info => info.GameId is "*" || info.GameId == gameId).ToArray();
        if (_actionChoices.Length == 0) throw new InvalidOperationException("This workspace has no catalog actions.");
        _actions.Items.AddRange(_actionChoices.Select(info => (object)info.Title));
        _actions.SelectedIndex = Math.Max(0, Array.FindIndex(_actionChoices, info => info.Id == "review.routine"));
        _document = CardPrototype.Create(_actionChoices[_actions.SelectedIndex].Id, gameId, "My prototype", _routines);
        _toolbar.Controls.Add(_actions);
        AddButton("New card", () =>
        {
            if (!CanLeaveEditor() || !ConfirmDiscard()) return;
            var action = _actionChoices[_actions.SelectedIndex];
            SetDocument(CardPrototype.Create(action.Id, _document.GameId, "My " + action.Title, _routines), false);
        });
        AddButton("Save new version…", Save);
        AddButton("Load prototype…", LoadPrototype);
        AddButton("Choose routine file…", () => ChooseReplayFile("graphFile", "Choose this card's routine graph"));
        AddButton("Choose recording…", () => ChooseReplayFile("sessionFile", "Choose this card's recorded routine session"));
        _test.Click += async (_, _) =>
        {
            if (!CanLeaveEditor()) return;
            var snapshot = JobDeckRules.Copy(_document);
            await RunReviewAsync(() =>
            {
                var result = CardPrototype.Test(_dataRoot, snapshot, _routines);
                return result.Qualification with { Reason = result.Qualification.Reason + Environment.NewLine + "Report: " + result.ReportDirectory };
            });
        };
        _toolbar.Controls.Add(_test);
        var split = new SplitContainer { Size = new(1080, 650), Dock = DockStyle.Fill, SplitterDistance = 370, FixedPanel = FixedPanel.Panel1 };
        split.Panel1.Controls.Add(_preview);
        var help = new Label { Dock = DockStyle.Top, Padding = new(12), UseMnemonic = false, Text =
            "1. Flip the card to edit its settings and requirements.\n2. Apply changes with Save & flip, then save a version.\n3. Replay can test saved recordings; other actions are drafts.\nTests run offline. They do not control your game." };
        void FitHelp()
        {
            var width = Math.Max(120, split.Panel2.ClientSize.Width - help.Padding.Horizontal);
            var height = TextRenderer.MeasureText(help.Text, help.Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + help.Padding.Vertical + 6;
            if (help.Height != height) help.Height = height;
        }
        split.Panel2.SizeChanged += (_, _) => FitHelp();
        help.FontChanged += (_, _) => FitHelp();
        var review = new Button { Text = "Verify selected evidence for this version", AutoSize = true };
        review.Click += async (_, _) =>
        {
            if (!CanLeaveEditor() || _history.SelectedItem is not CardPrototypeAttempt attempt) return;
            var snapshot = JobDeckRules.Copy(_document);
            await RunReviewAsync(() => CardPrototype.ReviewHistory(_dataRoot, snapshot, attempt.Id, _routines));
        };
        _historyTools.Controls.Add(review);
        split.Panel2.Controls.Add(_history); split.Panel2.Controls.Add(_status); split.Panel2.Controls.Add(_historyTools); split.Panel2.Controls.Add(help);
        Controls.Add(split); Controls.Add(_toolbar);
        FormClosing += (_, args) =>
        {
            if (_busy) { args.Cancel = true; _status.Text = "Finishing the bounded local review. No game inputs are active."; }
            else if (!ConfirmDiscard()) args.Cancel = true;
        };
        SetDocument(_document, false); WorkspaceTheme.Apply(this); FitHelp();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (Owner is null) return;
        var area = Screen.FromControl(Owner).WorkingArea;
        Size = FitToWorkingArea(area.Size);
        MinimumSize = new(Math.Min(920, Width), Math.Min(620, Height));
        Location = new(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
    }

    internal static Size FitToWorkingArea(Size workingArea)
        => new(Math.Max(640, Math.Min(1120, workingArea.Width)),
            Math.Max(520, Math.Min(790, workingArea.Height)));

    private void AddButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => Try(action); _toolbar.Controls.Add(button);
    }
    private void Try(Action action)
    {
        try { action(); }
        catch (Exception error) { _status.Text = "Unqualified · " + error.Message; }
    }
    private void SetDocument(CardPrototypeDocument document, bool dirty)
    {
        if (document.GameId != _gameId) throw new InvalidDataException("Open this prototype from its own game workspace.");
        CardPrototype.Validate(document, _routines); _document = JobDeckRules.Copy(document); _dirty = dirty;
        if (_view is not null) { _preview.Controls.Remove(_view); _view.Dispose(); }
        var routine = _routines[_document.Card.RoutineId];
        _view = new JobCardView(_document.Card, routine.Info,
            card => Try(() => SetDocument(_document with { Card = card with { InDeck = false } }, true)),
            () => Try(() => SetDocument(_document with { Card = _document.Card with
            { Id = Guid.NewGuid().ToString("N"), Title = _document.Card.Title[..Math.Min(90, _document.Card.Title.Length)] + " copy", InDeck = false } }, true)),
            CardPrototype.Facts(_document, _routines), prototypeMode: true);
        _view.SetStatus(new(), new(_document.Card.Id, false, "Unqualified prototype; no execution permission."));
        _preview.Controls.Add(_view);
        _actions.SelectedIndex = Array.FindIndex(_actionChoices, info => info.Id == _document.Card.RoutineId);
        _status.Text = "Unqualified · This version has not been checked." + Environment.NewLine +
            "Select a previous test to recheck it against these settings.";
        _test.Enabled = _document.Card.RoutineId == "review.routine";
        if (!_test.Enabled) _status.AppendText(Environment.NewLine + "This action is a draft. Recorded-outcome testing is not connected yet.");
        RefreshHistory();
    }
    private bool CanLeaveEditor()
    {
        if (_view?.ShowingBack != true) return true;
        MessageBox.Show(this, "Use Save & flip on the card first. Tests and saved versions use the applied card settings.", "Finish editing");
        return false;
    }
    private bool ConfirmDiscard() => !_dirty && _view?.ShowingBack != true || MessageBox.Show(this,
        "Leave this draft without saving a new version?", "Unsaved prototype", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    private void Save()
    {
        if (!CanLeaveEditor()) return;
        Directory.CreateDirectory(_library);
        var name = new string(_document.Card.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c).ToArray());
        using var file = new SaveFileDialog { Title = "Save a new prototype version", Filter = "Card prototype (*.json)|*.json", InitialDirectory = _library, FileName = name + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json", OverwritePrompt = false };
        file.FileOk += (_, args) =>
        {
            if (!File.Exists(file.FileName)) return;
            args.Cancel = true;
            MessageBox.Show(this, "Existing versions are preserved. Choose a new filename.", "Save a new version");
        };
        if (file.ShowDialog(this) != DialogResult.OK) return;
        CardPrototype.Save(file.FileName, _document, _routines); _dirty = false;
        _status.Text = "Prototype saved. Saving does not grant qualification.\n" + file.FileName;
    }
    private void LoadPrototype()
    {
        if (!CanLeaveEditor() || !ConfirmDiscard()) return;
        using var file = new OpenFileDialog { Title = "Load a saved prototype", Filter = "Card prototype (*.json)|*.json", InitialDirectory = _library };
        if (file.ShowDialog(this) == DialogResult.OK) SetDocument(CardPrototype.Load(file.FileName, _routines), false);
    }
    private void ChooseReplayFile(string setting, string title)
    {
        if (!CanLeaveEditor()) return;
        if (_document.Card.RoutineId != "review.routine") throw new InvalidOperationException("Choose the Replay action to configure recorded routine files.");
        using var file = new OpenFileDialog { Title = title, Filter = "JSON (*.json)|*.json" };
        if (file.ShowDialog(this) != DialogResult.OK) return;
        var settings = new Dictionary<string, string>(_document.Card.Settings ?? []) { [setting] = Path.GetFullPath(file.FileName) };
        SetDocument(_document with { Card = _document.Card with { Settings = settings } }, true);
    }
    private void RefreshHistory()
    {
        _history.Items.Clear(); _history.Items.AddRange(CardPrototype.History(_dataRoot, _document.Card.Id));
        if (_history.Items.Count > 0) _history.SelectedIndex = 0;
    }
    private async Task RunReviewAsync(Func<CardQualificationStatus> review)
    {
        if (_busy) return;
        _busy = true; _toolbar.Enabled = _preview.Enabled = _historyTools.Enabled = false;
        _status.Text = "Checking the applied card and recorded evidence locally…";
        try
        {
            var result = await Task.Run(review);
            _status.Text = result.Stage + " · " + result.Reason + "\nEvidence never grants game-control permission.";
        }
        catch (Exception error) { _status.Text = "Unqualified · " + error.Message; }
        finally { _busy = false; _toolbar.Enabled = _preview.Enabled = _historyTools.Enabled = true; RefreshHistory(); }
    }
}
