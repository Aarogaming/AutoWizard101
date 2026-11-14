using System.Drawing.Drawing2D;
using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>The collection is a binder; only dealt cards enter the routine deck.
/// Fronts show intent/readiness. Backs edit the same persisted scheduler contracts.</summary>
internal sealed class JobDeckForm : Form
{
    private readonly JobDeckHost _host;
    private readonly FlowLayoutPanel _cards = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20), BackColor = Color.FromArgb(22, 17, 38) };
    private readonly CardChoiceButton _collection = new() { Width = 170 };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(1000, 0), Padding = new Padding(12) };
    private readonly Label _count = new() { AutoSize = true, Padding = new Padding(8) };
    private readonly CheckBox _onlyDeck = new() { Text = "Routine Deck only", AutoSize = true, Padding = new Padding(8) };
    private readonly System.Windows.Forms.Timer _refresh = new() { Interval = 1000 };
    private long _revision = -1;
    private bool _closing;
    private bool _safeToClose;
    internal JobDeckForm(JobDeckHost host)
    {
        _host = host; Text = "Maelstrom · Wizard101 · Deck Builder"; Font = new Font("Segoe UI", 10);
        Size = new Size(1210, 820); MinimumSize = new Size(800, 650); AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(22, 17, 38); ForeColor = Color.FromArgb(239, 222, 181);
        var header = new Label { Text = "MAELSTROM  /  WIZARD101\nYour collection. Your routine.", Dock = DockStyle.Top, Height = 100,
            Padding = new Padding(24, 16, 10, 6), Font = new Font("Georgia", 22, FontStyle.Bold), ForeColor = Color.FromArgb(232, 195, 109) };
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(18, 6, 10, 6), WrapContents = true };
        _collection.BackColor = Color.FromArgb(245, 231, 197); _collection.ForeColor = Color.FromArgb(35, 27, 30); _collection.FlatStyle = FlatStyle.Flat;
        _collection.Items.AddRange(new object[] { "All collections" }.Concat(host.Deck.Catalog.Select(c => c.Collection).Distinct().Cast<object>()).ToArray());
        _collection.SelectedIndex = 0; toolbar.Controls.Add(_collection); toolbar.Controls.Add(_onlyDeck);
        Button AddButton(string title, Action action)
        {
            var button = new Button { Text = title, AutoSize = true, FlatStyle = FlatStyle.Flat, Padding = new Padding(6), BackColor = Color.FromArgb(53, 38, 75) };
            button.Click += (_, _) => Attempt(action); toolbar.Controls.Add(button); return button;
        }
        AddButton("Play one", () => Run(false)); AddButton("Play deck", () => Run(true));
        AddButton("Stop", () => ShowResult(_host.Handle(Request("stop"))));
        AddButton("New run", () =>
        {
            if (MessageBox.Show(this, "Reset this deck's consumed copies and attempt counters for a new bounded run? Saved operation reports will remain.",
                "New run", MessageBoxButtons.OKCancel) == DialogResult.OK)
                ShowResult(_host.Handle(Request("reset", new { revision = _host.Deck.State.Revision })));
        });
        AddButton("Deck limits", EditLimits); AddButton("Connect W101", Connect);
        AddButton("Observe once", () => ShowResult(_host.Handle(Request("observe"))));
        AddButton("New", AddCard); AddButton("Load card", ImportCard); AddButton("Export", Export); AddButton("Import", Import);
        toolbar.Controls.Add(_count);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 84 }; footer.Controls.Add(_status);
        Controls.Add(_cards); Controls.Add(footer); Controls.Add(toolbar); Controls.Add(header);
        _collection.SelectedIndexChanged += (_, _) => Reload(); _onlyDeck.CheckedChanged += (_, _) => Reload();
        _refresh.Tick += (_, _) => RefreshState(); _refresh.Start(); Reload();
        FormClosing += async (_, e) =>
        {
            if (_safeToClose) return;
            e.Cancel = true;
            if (_closing) return;
            _closing = true;
            try { await _host.StopAndDrainAsync(); _safeToClose = true; Close(); }
            catch (Exception ex) { _closing = false; _status.Text = "Cleanup needs attention: " + ex.Message; }
        };
        FormClosed += (_, _) => { _refresh.Stop(); _refresh.Dispose(); };
    }
    private static JobDeckRequest Request(string command, object? arguments = null) =>
        new(command, JsonSerializer.SerializeToElement(arguments ?? new { }, JobDeckRules.Json), Guid.NewGuid().ToString("N"));
    private void Run(bool continuous) => ShowResult(_host.Handle(Request(continuous ? "start" : "step")));
    private void ShowResult(JobDeckReply result)
    {
        if (!result.Ok) throw new InvalidOperationException(result.Error);
        RefreshState();
    }
    private void Attempt(Action action)
    {
        try { action(); }
        catch (Exception ex) { _status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Maelstrom deck", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }
    private void Reload()
    {
        var state = _host.Deck.State; _revision = state.Revision;
        _cards.SuspendLayout();
        foreach (Control control in _cards.Controls.Cast<Control>().ToArray()) control.Dispose();
        _cards.Controls.Clear();
        foreach (var card in state.Deck.Cards.OrderBy(c => c.Priority))
        {
            var info = _host.Deck.Catalog.Single(c => c.Id == card.RoutineId);
            if (_onlyDeck.Checked && !card.InDeck || _collection.SelectedIndex > 0 && info.Collection != _collection.SelectedItem?.ToString()) continue;
            var view = new JobCardView(card, info, updated => Attempt(() => SaveCard(updated)), () => Attempt(() => Duplicate(card)), _host.Deck.FactDefinitions[card.RoutineId]);
            _cards.Controls.Add(view);
        }
        _cards.ResumeLayout(); RefreshState();
    }
    private void SaveCard(JobCardSpec card)
    {
        var state = _host.Deck.State;
        _host.Deck.Replace(state.Deck with { Cards = state.Deck.Cards.Select(c => c.Id == card.Id ? card : c).ToArray() }, _revision);
        Reload();
    }
    private void Duplicate(JobCardSpec card)
    {
        var state = _host.Deck.State;
        var copy = JobDeckRules.Copy(card) with { Id = Guid.NewGuid().ToString("N"), Title = card.Title + " copy", InDeck = false };
        _host.Deck.Replace(state.Deck with { Cards = [.. state.Deck.Cards, copy] }, _revision); Reload();
    }
    private void AddCard()
    {
        var state = _host.Deck.State;
        var choices = _host.Deck.Catalog.Where(c => c.GameId == "*" || c.GameId == state.Deck.GameId).ToArray();
        using var dialog = new Form { Text = "Add a card to your collection", Width = 650, Height = 230,
            StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), FlowDirection = FlowDirection.TopDown };
        var picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 590 };
        picker.Items.AddRange(choices.Select(c => (object)$"{c.Collection} · {c.Title} — {c.Qualification}").ToArray());
        if (choices.Length > 0) picker.SelectedIndex = 0;
        flow.Controls.Add(picker);
        flow.Controls.Add(new Label { Text = "New cards are undealt. Configure the card before adding it to your Routine Deck.", AutoSize = true });
        flow.Controls.Add(new Button { Text = "Add to collection", AutoSize = true, DialogResult = DialogResult.OK, Enabled = choices.Length > 0 });
        dialog.Controls.Add(flow); WorkspaceTheme.Apply(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK || picker.SelectedIndex < 0) return;
        var card = JobDeckRoutines.NewCard(choices[picker.SelectedIndex]);
        _host.Deck.Replace(state.Deck with { Cards = [.. state.Deck.Cards, card] }, state.Revision);
        _onlyDeck.Checked = false; _collection.SelectedIndex = 0; Reload();
    }
    private void RefreshState()
    {
        if (IsDisposed) return;
        var state = _host.Deck.State;
        if (state.Revision != _revision) { Reload(); return; }
        var eligibility = _host.Deck.Preview().ToDictionary(c => c.CardId);
        foreach (var card in _cards.Controls.OfType<JobCardView>())
            card.SetStatus(state.Progress.GetValueOrDefault(card.CardId, new()), eligibility[card.CardId]);
        _count.Text = $"{state.Deck.Cards.Count(c => c.InDeck)} dealt / {state.Deck.Cards.Length} collected";
        _status.Text = $"{state.Status} · {state.Progress.Values.Sum(p => p.Completed)} verified completions · {state.Progress.Values.Sum(p => p.Attempts)}/{state.Deck.AttemptLimit} attempts · {state.Deck.DurationSeconds / 60.0:0.#} min limit\n" +
            (state.Error ?? "Flip a card to configure it. Undealt cards stay in your collection. Missing facts wait; they are never guessed.");
    }
    private void EditLimits()
    {
        var state = _host.Deck.State;
        using var dialog = new Form { Text = "Routine Deck limits", Size = new Size(440, 280), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(18), WrapContents = false };
        var name = new TextBox { Text = state.Deck.Name, Width = 360 };
        var attempts = new NumericUpDown { Minimum = 1, Maximum = 1000, Value = state.Deck.AttemptLimit };
        var seconds = new NumericUpDown { Minimum = 1, Maximum = 3600, Value = state.Deck.DurationSeconds };
        flow.Controls.Add(new Label { Text = "Deck name", AutoSize = true }); flow.Controls.Add(name);
        flow.Controls.Add(new Label { Text = "Total attempts / time limit in seconds", AutoSize = true }); flow.Controls.Add(attempts); flow.Controls.Add(seconds);
        var save = new Button { Text = "Save", DialogResult = DialogResult.OK }; flow.Controls.Add(save); dialog.Controls.Add(flow); WorkspaceTheme.Apply(dialog);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _host.Deck.Replace(state.Deck with { Name = name.Text, AttemptLimit = (int)attempts.Value, DurationSeconds = (int)seconds.Value }, state.Revision);
        RefreshState();
    }
    private void Export()
    {
        using var dialog = new SaveFileDialog { Filter = "Maelstrom deck (*.json)|*.json", FileName = "routine-deck.json" };
        if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_host.Deck.State.Deck, JobDeckRules.Json));
    }
    private void Import()
    {
        using var dialog = new OpenFileDialog { Filter = "Maelstrom deck (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var deck = JsonSerializer.Deserialize<JobDeckDocument>(GameExperiments.BoundedBytes(dialog.FileName, 65536), JobDeckRules.Json) ?? throw new InvalidDataException("Empty deck.");
        _host.Deck.Replace(deck, _host.Deck.State.Revision); Reload();
    }
    private void ImportCard()
    {
        using var file = new OpenFileDialog { Filter = "Maelstrom card (*.json)|*.json" };
        if (file.ShowDialog(this) != DialogResult.OK) return;
        var card = JsonSerializer.Deserialize<JobCardSpec>(GameExperiments.BoundedBytes(file.FileName, 65536), JobDeckRules.Json)
            ?? throw new InvalidDataException("Missing card.");
        var state = _host.Deck.State;
        card = card with { Id = Guid.NewGuid().ToString("N"), InDeck = false };
        _host.Deck.Replace(state.Deck with { Cards = [.. state.Deck.Cards, card] }, state.Revision);
        Reload();
    }
    private void Connect()
    {
        using var dialog = new Form { Text = "Connect Wizard101 · optional client reader", Size = new Size(690, 380), StartPosition = FormStartPosition.CenterParent };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(20), WrapContents = false };
        var windows = GameWindows.List().Where(w => w.ProcessName == "WizardGraphicalClient").ToArray();
        var choice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 610 }; choice.Items.AddRange(windows.Cast<object>().ToArray()); if (windows.Length > 0) choice.SelectedIndex = 0;
        var path = new TextBox { Width = 610, PlaceholderText = "Maelstrom source workspace containing the reviewed optional adapter" };
        var observe = new CheckBox { Text = "Enable reviewed client observation and UI-tree reader", AutoSize = true };
        var actions = new CheckBox { Text = "Also allow my selected conversation cards to advance dialogue", AutoSize = true };
        var travel = new CheckBox { Text = "Also allow my configured Travel cards to select worlds and return Home", AutoSize = true };
        flow.Controls.Add(choice); flow.Controls.Add(path); flow.Controls.Add(observe); flow.Controls.Add(actions); flow.Controls.Add(travel);
        flow.Controls.Add(new Label { Text = "Connection alone sends no inputs. Observe once collects a bounded reading.\nUnobserved resources remain unknown. No networking or arbitrary client commands.", AutoSize = true });
        flow.Controls.Add(new Button { Text = "Connect", AutoSize = true, DialogResult = DialogResult.OK }); dialog.Controls.Add(flow); WorkspaceTheme.Apply(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (choice.SelectedItem is not GameWindow window) throw new InvalidOperationException("Choose a current Wizard101 window.");
        ShowResult(_host.Handle(Request("connect", new { handle = window.Handle.ToInt64(), processId = window.ProcessId, startedUtcTicks = window.StartedUtcTicks,
            workspace = path.Text, enableClientObservation = observe.Checked, enableUiTree = observe.Checked, enableQuestActions = actions.Checked, enableTravelActions = travel.Checked })));
    }
}

internal sealed class JobCardView : UserControl
{
    private readonly JobCardSpec _card;
    private readonly JobRoutineInfo _info;
    private readonly IReadOnlyList<JobFactDefinition> _factDefinitions;
    private readonly Action<JobCardSpec> _save;
    private readonly Action _duplicate;
    private readonly Panel _back = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(45, 31, 64), Padding = new Padding(12) };
    private readonly System.Windows.Forms.Timer _flip = new() { Interval = 16 };
    private readonly ToolTip _tip = new() { AutoPopDelay = 15000 };
    private readonly Dictionary<string, Control> _options = [];
    private JobCardProgress _progress = new();
    private JobEligibility? _eligibility;
    private bool _showBack;
    private int _frame;
    private TextBox _title = null!, _include = null!, _exclude = null!, _fact = null!, _conditions = null!;
    private JobCondition[] _editedConditions = [];
    private NumericUpDown _copies = null!, _attempts = null!, _priority = null!, _cooldown = null!;
    internal string CardId => _card.Id;
    internal string RoutineId => _info.Id;
    internal bool ShowingBack => _showBack && !_flip.Enabled;
    internal void PreviewFlip() => Flip();
    internal JobCardView(JobCardSpec card, JobRoutineInfo info, Action<JobCardSpec> save, Action duplicate, IReadOnlyList<JobFactDefinition>? factDefinitions = null)
    {
        _card = card; _info = info; _save = save; _duplicate = duplicate;
        _factDefinitions = factDefinitions ?? JobFactCatalog.All;
        Size = new Size(330, 540); Margin = new Padding(10, 8, 10, 12); DoubleBuffered = true;
        AccessibleName = card.Title; AccessibleDescription = info.Description; TabStop = true;
        _editedConditions = JobDeckRules.Copy(card.Conditions ?? []);
        Cursor = Cursors.Hand; BuildBack(); _back.Visible = false; Controls.Add(_back);
        _flip.Tick += (_, _) =>
        {
            if (++_frame >= 12) { _flip.Stop(); _showBack = !_showBack; _back.Visible = _showBack; _frame = 0; }
            Invalidate();
        };
        MouseClick += (_, e) =>
        {
            if (_showBack || _flip.Enabled) return;
            if (e.Y > Height - 53) _save(_card with { InDeck = !_card.InDeck }); else Flip();
        };
        KeyDown += (_, e) => { if (e.KeyCode is Keys.Enter or Keys.Space) { Flip(); e.Handled = true; } };
    }
    internal void SetStatus(JobCardProgress progress, JobEligibility eligibility)
    {
        if (_progress == progress && _eligibility == eligibility) return;
        _progress = progress; _eligibility = eligibility;
        AccessibleDescription = _info.Description + " " + eligibility.Reason;
        _tip.SetToolTip(this, _info.Description + Environment.NewLine + eligibility.Reason + (progress.Reason == null ? "" : Environment.NewLine + progress.Reason));
        Invalidate();
    }
    private void Flip() { if (_flip.Enabled) return; _back.Visible = false; _frame = 0; _flip.Start(); }
    private void BuildBack()
    {
        var flow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 292, ForeColor = Color.FromArgb(241, 226, 189) };
        void Label(string text) => flow.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(286, 0) });
        TextBox Text(string text) { var box = new TextBox { Width = 286, Text = text }; flow.Controls.Add(box); return box; }
        NumericUpDown Number(string label, int value, int min, int max)
        { Label(label); var box = new NumericUpDown { Width = 100, Minimum = min, Maximum = max, Value = value }; flow.Controls.Add(box); return box; }
        Label("SETTINGS"); _title = Text(_card.Title);
        _copies = Number("Copies to complete", _card.Runs, 1, 100); _attempts = Number("Maximum attempts", _card.AttemptLimit, 1, 100);
        _priority = Number("Priority (lower plays first)", _card.Priority, 0, 999); _cooldown = Number("Cooldown · seconds", _card.CooldownSeconds, 1, 3600);
        foreach (var option in _info.Options)
        {
            Label(option.Label + (option.Required ? " *" : ""));
            if (option.Kind.StartsWith("choice:", StringComparison.Ordinal))
            {
                var choices = new CardChoiceButton { Width = 286, ForeColor = Color.FromArgb(35, 27, 30), BackColor = Color.FromArgb(245, 231, 197) };
                choices.Items.AddRange(option.Kind[7..].Split(',').Cast<object>().ToArray());
                choices.SelectedItem = option.Name == "mode" && _info.Id == "w101.questing" ? JobDeckRoutines.QuestMode(_card) : _card.Settings?.GetValueOrDefault(option.Name) ?? option.Default;
                if (choices.SelectedIndex < 0) choices.SelectedIndex = 0;
                _options.Add(option.Name, choices); flow.Controls.Add(choices); continue;
            }
            var box = Text(_card.Settings?.GetValueOrDefault(option.Name) ?? option.Default ?? ""); _options.Add(option.Name, box);
            if (option.Kind == "file")
            {
                var browse = new Button { Text = "Choose file…", AutoSize = true };
                browse.Click += (_, _) => { using var picker = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" }; if (picker.ShowDialog(this) == DialogResult.OK) box.Text = picker.FileName; };
                flow.Controls.Add(browse);
            }
        }
        Label("Scope fact (quest.area, quest.action, quest.id)"); _fact = Text(_card.Filters?.FirstOrDefault()?.Fact ?? (_info.Id == "w101.questing" ? "quest.area" : "area"));
        var factPicker = new ComboBox { Width = 286, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Registered scope fact" };
        factPicker.Items.AddRange(_factDefinitions.Where(f => f.Kind == "string").Select(f => (object)f.Id).ToArray());
        factPicker.SelectedItem = _fact.Text;
        factPicker.SelectedIndexChanged += (_, _) => _fact.Text = factPicker.SelectedItem?.ToString() ?? _fact.Text;
        flow.Controls.Add(factPicker);
        Label("Only these values · one per line"); _include = Text(string.Join(Environment.NewLine, _card.Filters?.FirstOrDefault()?.Include ?? [])); _include.Multiline = true; _include.Height = 48;
        Label("Exclude these values · one per line"); _exclude = Text(string.Join(Environment.NewLine, _card.Filters?.FirstOrDefault()?.Exclude ?? [])); _exclude.Multiline = true; _exclude.Height = 48;
        Label("Requirements to play");
        _conditions = Text(string.Join(Environment.NewLine, _editedConditions.Select(c => $"{c.Fact} {c.Operator} {c.Value}")));
        _conditions.Multiline = true; _conditions.Height = 65; _conditions.ReadOnly = true;
        var editRequirements = new Button { Text = "Edit requirements...", AutoSize = true };
        editRequirements.Click += (_, _) =>
        {
            using var editor = new CardRequirementsForm(_editedConditions, _factDefinitions);
            if (editor.ShowDialog(this) != DialogResult.OK) return;
            _editedConditions = editor.Conditions;
            _conditions.Text = string.Join(Environment.NewLine, _editedConditions.Select(c => $"{c.Fact} {c.Operator} {c.Value}"));
        };
        flow.Controls.Add(editRequirements);
        var factHelp = new Button { Text = "Available facts and requirements…", AutoSize = true };
        factHelp.Click += (_, _) => MessageBox.Show(this, string.Join(Environment.NewLine + Environment.NewLine,
            _factDefinitions.Select(f => $"{f.Id} ({f.Kind}, {f.Unit})\n{f.Description}\nProducer: {f.Producer}. {(f.ProducerAvailable ? "Producer supported; current reading may still be unknown." : "Producer not connected; conditions will wait.")}")),
            "Registered facts · definitions, not live readings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        flow.Controls.Add(factHelp);
        Label("Unknown facts wait. Exclusions override inclusions. No automatic purchases or snack use.");
        var save = new Button { Text = "Save & flip", AutoSize = true }; save.Click += (_, _) =>
        {
            try
            {
                var filters = (_card.Filters ?? []).Skip(1).ToList();
                string[] Lines(TextBox box) => box.Lines.Select(s => s.Trim()).Where(s => s.Length != 0).ToArray();
                var include = Lines(_include); var exclude = Lines(_exclude);
                if (include.Length + exclude.Length > 0) filters.Insert(0, new(_fact.Text.Trim(), include, exclude));
                var conditions = _editedConditions;
                _save(_card with { Title = _title.Text.Trim(), Runs = (int)_copies.Value, AttemptLimit = (int)_attempts.Value,
                    Priority = (int)_priority.Value, CooldownSeconds = (int)_cooldown.Value, Settings = _options.ToDictionary(o => o.Key, o => o.Value is CardChoiceButton choice ? choice.SelectedItem?.ToString() ?? "" : o.Value.Text), Conditions = conditions, Filters = filters.ToArray() });
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Card settings", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        };
        flow.Controls.Add(save);
        var cancel = new Button { Text = "Flip back", AutoSize = true }; cancel.Click += (_, _) => Flip(); flow.Controls.Add(cancel);
        var duplicate = new Button { Text = "Make another card", AutoSize = true }; duplicate.Click += (_, _) => _duplicate(); flow.Controls.Add(duplicate);
        var test = new Button { Text = "Test recording...", AutoSize = true };
        test.Click += (_, _) => { using var review = new RecordedCardReviewForm(_card, _factDefinitions, _info.GameId); review.ShowDialog(this); };
        flow.Controls.Add(test);
        var export = new Button { Text = "Export...", AutoSize = true };
        export.Click += (_, _) =>
        {
            using var file = new SaveFileDialog { Filter = "Maelstrom card (*.json)|*.json", FileName = "custom-card.json" };
            if (file.ShowDialog(this) != DialogResult.OK) return;
            try { using var output = new FileStream(file.FileName, FileMode.CreateNew); JsonSerializer.Serialize(output, _card, JobDeckRules.Json); }
            catch (Exception e) { MessageBox.Show(this, e.Message, "Save card"); }
        };
        flow.Controls.Add(export);
        Label("Save changes before testing.");
        _back.Controls.Add(flow);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); if (_showBack && !_flip.Enabled) return;
        var g = e.Graphics; var state = g.Save(); g.SmoothingMode = SmoothingMode.AntiAlias;
        if (_flip.Enabled)
        {
            var scale = Math.Max(.02f, Math.Abs(1f - _frame / 6f)); g.TranslateTransform(Width * (1 - scale) / 2, 0); g.ScaleTransform(scale, 1);
        }
        using var parchment = new LinearGradientBrush(ClientRectangle, Color.FromArgb(243, 225, 177), Color.FromArgb(197, 158, 97), 110);
        using var gold = new Pen(Color.FromArgb(225, 183, 91), 3); using var purple = new SolidBrush(Color.FromArgb(58, 25, 71));
        using var ink = new SolidBrush(Color.FromArgb(59, 33, 48)); using var frame = new Pen(Color.FromArgb(83, 44, 49), 2);
        g.FillRectangle(purple, ClientRectangle); g.FillRectangle(parchment, 9, 9, Width - 18, Height - 18);
        g.DrawRectangle(gold, 4, 4, Width - 9, Height - 9); g.DrawRectangle(frame, 14, 14, Width - 29, Height - 29);
        using var serif = new Font(W101CardArtwork.ResolveTitleFontFamily(), 21, FontStyle.Bold); using var small = new Font("Segoe UI", 9, FontStyle.Bold);
        using var body = new Font("Segoe UI", 9); using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        DrawEmblem(g, new Rectangle(24, 24, 29, 29), _info.Icon, ink);
        var corner = g.Save(); g.TranslateTransform(Width - 22, Height - 22); g.RotateTransform(180); DrawEmblem(g, new Rectangle(0, 0, 20, 20), _info.Icon, ink); g.Restore(corner);
        g.DrawString(_info.Collection.ToUpperInvariant(), small, ink, new RectangleF(50, 28, Width - 85, 23), centered);
        g.DrawEllipse(frame, Width / 2f - 51, 79, 102, 102);
        DrawEmblem(g, new Rectangle(Width / 2 - 33, 97, 66, 66), _info.Icon, ink);
        g.DrawString(_card.Title, serif, ink, new RectangleF(24, 184, Width - 48, 62), centered);
        g.DrawLine(frame, 38, 252, Width - 38, 252);
        var status = !_info.Available ? "PREPARATION ONLY" : !_card.InDeck ? "COLLECTION" : _progress.Status == "Running" ? "PLAYING" :
            _progress.Completed >= _card.Runs ? "COMPLETED" : _eligibility?.Ready == true ? "READY" : "WAITING";
        g.DrawString(status, small, ink, new RectangleF(26, 258, Width - 52, 22), centered);
        g.DrawString($"{_progress.Completed}/{_card.Runs} copies · {_progress.Attempts}/{_card.AttemptLimit} attempts", body, ink, new RectangleF(25, 284, Width - 50, 21), centered);
        g.DrawString("Edit", body, ink, new RectangleF(22, 307, Width - 44, 22), centered);
        g.DrawString(_info.Description, body, ink, new RectangleF(24, 337, Width - 48, 65));
        var requirements = string.Join("; ", (_card.Conditions ?? []).Select(c => $"{c.Fact} {c.Operator} {c.Value}"));
        g.DrawString("Requires: " + (requirements.Length == 0 ? "fresh state and adapter readiness" : requirements), body, ink, new RectangleF(24, 409, Width - 48, 48));
        g.DrawString("" + _info.Qualification, body, ink, new RectangleF(24, 462, Width - 48, 28));
        g.FillRectangle(purple, 15, Height - 47, Width - 64, 31);
        using var cream = new SolidBrush(Color.FromArgb(249, 227, 173));
        g.DrawString(_card.InDeck ? "✓  IN ROUTINE DECK" : "+  DEAL THIS CARD", small, cream, new RectangleF(15, Height - 47, Width - 64, 31), centered);
        g.Restore(state);
    }
    private static void DrawEmblem(Graphics g, Rectangle bounds, string id, Brush ink)
    {
        W101CardArtwork.DrawIcon(g, bounds, id, ink is SolidBrush brush ? brush.Color : Color.FromArgb(59, 33, 48));
    }
    protected override void Dispose(bool disposing) { if (disposing) { _flip.Dispose(); _tip.Dispose(); } base.Dispose(disposing); }
}

/// <summary>Keyboard-accessible native menu picker with a managed text face.
/// The selected value stays distinct from the displayed dropdown indicator.</summary>
internal sealed class CardChoiceButton : Button
{
    private int _selectedIndex = -1;
    private ContextMenuStrip? _menu;
    internal List<object> Items { get; } = [];
    internal event EventHandler? SelectedIndexChanged;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < -1 || value >= Items.Count) throw new ArgumentOutOfRangeException(nameof(value));
            _selectedIndex = value; Text = (SelectedItem?.ToString() ?? "Choose…") + "  ▾";
            AccessibleName = SelectedItem?.ToString() ?? "Choose an option";
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal object? SelectedItem { get => _selectedIndex >= 0 ? Items[_selectedIndex] : null; set => SelectedIndex = value == null ? -1 : Items.IndexOf(value); }
    internal CardChoiceButton()
    {
        Height = 32; FlatStyle = FlatStyle.Flat; TextAlign = ContentAlignment.MiddleLeft;
        UseVisualStyleBackColor = false; AccessibleRole = AccessibleRole.ComboBox;
    }
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e); _menu?.Dispose(); _menu = new ContextMenuStrip();
        for (int i = 0; i < Items.Count; i++)
        {
            var index = i; var item = new ToolStripMenuItem(Items[i].ToString()) { Checked = i == SelectedIndex };
            item.Click += (_, _) => SelectedIndex = index; _menu.Items.Add(item);
        }
        _menu.Show(this, new Point(0, Height));
    }
    protected override void Dispose(bool disposing) { if (disposing) _menu?.Dispose(); base.Dispose(disposing); }
}
