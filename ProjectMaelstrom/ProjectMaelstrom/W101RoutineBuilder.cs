using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Edits data-only motion sequences; the session runner owns all live input.</summary>
internal sealed class W101RoutineBuilder : UserControl
{
    private readonly string _dataRoot;
    private readonly Action<W101Routine> _select;
    private readonly Action<string>? _openPack;
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly TextBox _title = new() { Width = 440, MaxLength = 120, Text = "My motion routine" };
    private readonly ComboBox _saved = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 440 };
    private readonly ComboBox _primitive = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly ListBox _steps = new() { Width = 590, Height = 180 };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(650, 0) };
    private string _id = "motion-" + Guid.NewGuid().ToString("N")[..12];

    public W101RoutineBuilder(string dataRoot, Action<W101Routine> select, Action<string>? openPack)
    {
        _dataRoot = dataRoot; _select = select; _openPack = openPack;
        Dock = DockStyle.Fill;
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(18) };
        void Note(string text) => body.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(650, 0), Margin = new Padding(0, 4, 0, 8) });
        Button Button(string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (_, _) => { try { action(); } catch (Exception ex) { _status.Text = ex.Message; } }; return button;
        }
        Note("Wizard101 · routine builder");
        Note("Build a small motion sequence, save it in a category, then use it from Auto-Idle. Every live step requires a fresh view and a checked outcome. Category names organize drafts; they do not enable farming, pets, potions or quests yet.");
        _category.Items.AddRange(W101Routine.Categories); _category.SelectedIndex = 0;
        var identity = new FlowLayoutPanel { AutoSize = true }; identity.Controls.AddRange([_category, _title]); body.Controls.Add(identity);
        var saved = new FlowLayoutPanel { AutoSize = true }; saved.Controls.AddRange([_saved, Button("Load", LoadSelected), Button("New", New)]); body.Controls.Add(saved);
        _primitive.Items.AddRange(W101Routine.Primitives); _primitive.SelectedIndex = 0;
        var add = new FlowLayoutPanel { AutoSize = true }; add.Controls.AddRange([_primitive, Button("Add action", () => { if (_steps.Items.Count >= 8) throw new InvalidOperationException("Use at most eight actions."); _steps.Items.Add(_primitive.SelectedItem!); _steps.SelectedIndex = _steps.Items.Count - 1; })]); body.Controls.Add(add);
        body.Controls.Add(_steps);
        var edit = new FlowLayoutPanel { AutoSize = true }; edit.Controls.AddRange([Button("Move up", () => MoveStep(-1)), Button("Move down", () => MoveStep(1)), Button("Remove", () => { if (_steps.SelectedIndex >= 0) _steps.Items.RemoveAt(_steps.SelectedIndex); })]); body.Controls.Add(edit);
        Note("Cycle: check view → action → verify scene change → next action. Unknown state or desktop activity pauses. Auto-Idle controls pulse duration, cycle interval and session limit. Walking requires a clear home area.");
        var actions = new FlowLayoutPanel { AutoSize = true }; actions.Controls.AddRange([Button("Save routine", () => SaveDraft()), Button("Use for Auto-Idle", () => { var routine = SaveDraft(); _select(routine); }), Button("Export draft pack", () => { var routine = SaveDraft(); var pack = routine.ExportPack(_dataRoot); _status.Text = "Draft pack saved: " + pack; _openPack?.Invoke(pack); })]); body.Controls.Add(actions);
        body.Controls.Add(_status); Controls.Add(body); RefreshSaved();
    }
    private W101Routine Current() => new(_id, _title.Text.Trim(), _category.SelectedItem?.ToString() ?? "Auto-Idle", _steps.Items.Cast<W101MotionPrimitive>().Select(p => p.Id).ToArray());
    private W101Routine SaveDraft()
    {
        var routine = Current(); routine.Save(_dataRoot); RefreshSaved(); _status.Text = "Saved locally. Nothing is running."; return routine;
    }
    private void RefreshSaved()
    {
        _saved.Items.Clear(); var directory = Path.Combine(_dataRoot, "routines", "wizard101");
        if (!Directory.Exists(directory)) return;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Take(200))
        {
            try { _saved.Items.Add(W101Routine.Load(path)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException) { _status.Text = "Some saved routines could not load: " + ex.Message; }
        }
        if (_saved.Items.Count > 0) _saved.SelectedIndex = 0;
    }
    private void LoadSelected()
    {
        if (_saved.SelectedItem is not W101Routine selected) return;
        if (_steps.Items.Count > 0) SaveDraft();
        selected = W101Routine.Load(Path.Combine(_dataRoot, "routines", "wizard101", selected.Id + ".json"));
        _id = selected.Id; _title.Text = selected.Title; _category.SelectedItem = selected.Category;
        _steps.Items.Clear(); foreach (var id in selected.Actions) _steps.Items.Add(W101Routine.Resolve(id));
        _status.Text = "Loaded. Edit, save or explicitly select for Auto-Idle.";
    }
    private void New()
    {
        if (_steps.Items.Count > 0) SaveDraft();
        _id = "motion-" + Guid.NewGuid().ToString("N")[..12]; _title.Text = "My motion routine"; _steps.Items.Clear(); _status.Text = "New draft. Add actions, then Save routine.";
    }
    private void MoveStep(int offset)
    {
        var index = _steps.SelectedIndex; var next = index + offset;
        if (index < 0 || next < 0 || next >= _steps.Items.Count) return;
        var item = _steps.Items[index]; _steps.Items.RemoveAt(index); _steps.Items.Insert(next, item); _steps.SelectedIndex = next;
    }
}
