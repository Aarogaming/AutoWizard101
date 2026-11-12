using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Edits local planning preferences only; saving does not enable feeding,
/// establish inventory identity or submit any game operation.</summary>
internal sealed class PetTrainingPreferencesForm : Form
{
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360, AccessibleName = "Snack policy" };
    private readonly ComboBox _goal = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360, AccessibleName = "Pet training goal" };
    private readonly CheckBox _useTarget = new() { Text = "Set a numeric target", AutoSize = true };
    private readonly NumericUpDown _target = Number(999999);
    private readonly NumericUpDown _feeds = Number(100);
    private readonly NumericUpDown _reserve = Number(999999);
    private readonly NumericUpDown _energy = Number(999999);
    private readonly TextBox _allowed = NamesBox("Allowed snack names");
    private readonly TextBox _protected = NamesBox("Protected snack names");
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(700, 0), Margin = new Padding(0, 8, 0, 8) };
    private readonly Label _goalNote = new() { AutoSize = true, MaximumSize = new Size(700, 0), Margin = new Padding(0, 4, 0, 10) };

    public PetTrainingPreferences Result { get; private set; }

    public PetTrainingPreferencesForm(PetTrainingPreferences preferences)
    {
        Result = PetTrainingPreferences.Normalize(preferences);
        BindingContext = new BindingContext();
        Text = "Maelstrom · Pet snacks and goals";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        Size = new Size(820, 730);
        MinimumSize = new Size(760, 560);
        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(24)
        };
        void Note(string text, bool heading = false)
        {
            body.Controls.Add(new Label
            {
                Text = text, AutoSize = true, MaximumSize = new Size(700, 0),
                Font = heading ? new Font(Font.FontFamily, 12, FontStyle.Bold) : Font,
                Margin = new Padding(0, heading ? 10 : 4, 0, 8)
            });
        }
        Note("Your snacks, your rules", true);
        Note("These are saved planning preferences. This version finishes Dance without feeding; automatic feeding needs verified pet, inventory and consumption checks.");
        _mode.FormattingEnabled = true;
        _mode.Format += (_, args) => args.Value = args.ListItem switch
        {
            PetSnackPreference.NeverFeed => "Don't use my snacks (default)",
            PetSnackPreference.SuggestOnly => "Suggest snacks only",
            PetSnackPreference.SelectedSnacks => "Use selected snacks — planning only",
            _ => args.Value
        };
        _mode.Items.AddRange(Enum.GetValues<PetSnackPreference>().Cast<object>().ToArray());
        body.Controls.Add(_mode);
        Note("Desired outcome", true);
        _goal.FormattingEnabled = true;
        _goal.Format += (_, args) => args.Value = args.ListItem is PetTrainingGoal.TrainingXp ? "Training XP" : args.ListItem?.ToString();
        _goal.Items.AddRange(Enum.GetValues<PetTrainingGoal>().Cast<object>().ToArray());
        body.Controls.Add(_goal);
        body.Controls.Add(_goalNote);
        var targetRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        targetRow.Controls.AddRange([_useTarget, _target]);
        body.Controls.Add(targetRow);
        Note("Training XP targets refer to the current observed age. Stop and review after an age change. Stat targets are saved for future support; they do not select a pet talent. Happiness is a separate workflow.");
        Note("Limits and reserves", true);
        var limits = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 8) };
        void Limit(string label, NumericUpDown number)
        {
            var row = limits.RowCount++;
            limits.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 18, 8) }, 0, row);
            limits.Controls.Add(number, 1, row);
        }
        Limit("Maximum snacks per visit (0 = none)", _feeds);
        Limit("Keep at least this many of each snack", _reserve);
        Limit("Keep this much Energy (yellow orb)", _energy);
        body.Controls.Add(limits);
        Note("These limits constrain future verified snack proposals. They do not currently monitor game-entry Energy or create a consumption budget on their own.");
        Note("Crown Shop purchases are locked. No purchases or automatic feeding. Bazaar supply planning is not connected yet.");
        Note("Allowed and protected snacks", true);
        Note("Enter one exact snack name per line. Names are preferences, not verified inventory. Protected names always win, even if also allowed. Unknown items remain unavailable.");
        var lists = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 8) };
        lists.Controls.Add(new Label { Text = "Allowed snacks", AutoSize = true }, 0, 0);
        lists.Controls.Add(new Label { Text = "Never use these snacks", AutoSize = true }, 1, 0);
        lists.Controls.Add(_allowed, 0, 1);
        lists.Controls.Add(_protected, 1, 1);
        body.Controls.Add(lists);
        body.Controls.Add(_status);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 10, 16, 12) };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var save = new Button { Text = "Save preferences", AutoSize = true };
        footer.Controls.AddRange([cancel, save]);
        Controls.Add(body);
        Controls.Add(footer);
        AcceptButton = save;
        CancelButton = cancel;
        WorkspaceTheme.Apply(this);

        _mode.SelectedItem = Result.Snacks;
        _goal.SelectedItem = Result.Goal;
        _target.Value = Result.GoalTarget ?? 0;
        _useTarget.Checked = Result.GoalTarget.HasValue;
        _target.Enabled = _useTarget.Checked;
        _feeds.Value = Result.MaxFeedsPerVisit;
        _reserve.Value = Result.MinimumSnackRemaining;
        _energy.Value = Result.EnergyReserve;
        _allowed.Lines = Result.AllowedSnackNames!;
        _protected.Lines = Result.ProtectedSnackNames!;
        _useTarget.CheckedChanged += (_, _) => _target.Enabled = _useTarget.Checked;
        _goal.SelectedIndexChanged += (_, _) => UpdateGoalNote();
        _mode.SelectedIndexChanged += (_, _) => UpdateModeNote();
        save.Click += (_, _) => Save();
        UpdateGoalNote();
        UpdateModeNote();
    }

    private void UpdateGoalNote() => _goalNote.Text = _goal.SelectedItem is PetTrainingGoal.TrainingXp
        ? "The read-only planner can compare known XP benefits when the pet, items and current goal are verified."
        : "This stat objective is saved for future planning. Verified stat-specific benefits and cap handling are not connected yet.";

    private void UpdateModeNote() => _status.Text = _mode.SelectedItem switch
    {
        PetSnackPreference.NeverFeed => "Snacks stay untouched. A saved allowance does not override this policy.",
        PetSnackPreference.SuggestOnly => "Suggestions need verified observations. Saving this setting does not produce a suggestion from names alone.",
        _ => "Selected-snack planning only. Automatic feeding remains unavailable; no snack is consumed by saving."
    };

    private void Save()
    {
        try
        {
            Result = PetTrainingPreferences.Normalize(Result with
            {
                Snacks = (PetSnackPreference)_mode.SelectedItem!,
                Goal = (PetTrainingGoal)_goal.SelectedItem!,
                GoalTarget = _useTarget.Checked ? (int)_target.Value : null,
                MaxFeedsPerVisit = (int)_feeds.Value,
                MinimumSnackRemaining = (int)_reserve.Value,
                EnergyReserve = (int)_energy.Value,
                AllowedSnackNames = ReadNames(_allowed),
                ProtectedSnackNames = ReadNames(_protected)
            });
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception error) when (error is InvalidDataException or ArgumentException)
        { _status.Text = error.Message; }
    }

    private static string[] ReadNames(TextBox box) => box.Lines.Select(line => line.Trim()).Where(line => line.Length != 0).ToArray();
    private static NumericUpDown Number(int maximum) => new() { Minimum = 0, Maximum = maximum, Width = 150, ThousandsSeparator = true };
    private static TextBox NamesBox(string label) => new() { Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, Width = 330, Height = 128, MaxLength = 16000, AccessibleName = label };
}

