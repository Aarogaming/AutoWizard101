using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

internal sealed class CardRequirementsForm : Form
{
    internal JobCondition[] Conditions { get; private set; }
    internal CardRequirementsForm(JobCondition[] conditions, IReadOnlyList<JobFactDefinition> definitions)
    {
        Conditions = conditions;
        Text = "Card back · Requirements"; Size = new(720, 470); StartPosition = FormStartPosition.CenterParent;
        var grid = new DataGridView { Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = true, AllowUserToDeleteRows = true, RowHeadersVisible = true };
        var facts = new DataGridViewComboBoxColumn { HeaderText = "Observed fact", Name = "fact" };
        facts.Items.AddRange(definitions.Select(f => f.Id).Distinct().Cast<object>().ToArray()); grid.Columns.Add(facts);
        var ops = new DataGridViewComboBoxColumn { HeaderText = "Comparison", Name = "operator" };
        ops.Items.AddRange("eq", "gte", "lte"); grid.Columns.Add(ops);
        grid.Columns.Add("value", "Value (text, number, true/false)");
        foreach (var c in conditions) grid.Rows.Add(c.Fact, c.Operator, c.Value.ToString());
        var info = new Label { Dock = DockStyle.Top, Height = 65, Text = "All requirements must match fresh recorded observations.\nSelect a row and press Delete to remove it. Unavailable facts remain unknown; they are never guessed.", Padding = new(12) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50 };
        var save = new Button { Text = "Use requirements", AutoSize = true };
        save.Click += (_, _) =>
        {
            try
            {
                grid.EndEdit();
                var result = grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r =>
                {
                    var id = r.Cells[0].Value?.ToString() ?? "";
                    var kind = definitions.FirstOrDefault(f => f.Id == id)?.Kind ?? throw new InvalidDataException("Choose a registered fact.");
                    var text = r.Cells[2].Value?.ToString() ?? "";
                    var value = kind == "string" ? JsonSerializer.SerializeToElement(text) : JsonSerializer.Deserialize<JsonElement>(text.ToLowerInvariant());
                    var condition = new JobCondition(id, r.Cells[1].Value?.ToString() ?? "eq", value);
                    JobFactCatalog.ValidateCondition(condition, definitions); return condition;
                }).ToArray();
                if (result.Length > 16) throw new InvalidDataException("Use at most 16 requirements.");
                Conditions = result; DialogResult = DialogResult.OK;
            }
            catch (Exception e) { MessageBox.Show(this, e.Message, "Check requirement"); }
        };
        buttons.Controls.Add(save); buttons.Controls.Add(new Button { Text = "Cancel", DialogResult = DialogResult.Cancel });
        Controls.Add(grid); Controls.Add(info); Controls.Add(buttons); WorkspaceTheme.Apply(this);
    }
}
