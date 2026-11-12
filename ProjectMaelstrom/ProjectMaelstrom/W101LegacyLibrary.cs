using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

/// <summary>Read-only mapping of legacy alternatives and their qualification evidence.</summary>
internal sealed class W101LegacyLibrary : UserControl
{
    public W101LegacyLibrary()
    {
        Dock = DockStyle.Fill;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        Controls.Add(layout);
        var intro = new Label { AutoSize = true, MaximumSize = new Size(680, 0), Margin = new Padding(3, 3, 3, 12) };
        layout.Controls.Add(intro, 0, 0);
        try
        {
            var catalog = W101LegacyCatalog.Load();
            intro.Text = $"{catalog.Entries.Length} legacy projects · reviewed {catalog.ReviewedUtc:yyyy-MM-dd}\nCompare approaches and see where they fit. These entries are research references; live players need separate qualification.";
            var filterRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            filterRow.Controls.Add(new Label { Text = "Show area", AutoSize = true, Padding = new Padding(0, 6, 8, 0) });
            var filter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230, AccessibleName = "Legacy capability filter" };
            filter.Items.Add("All areas");
            filter.Items.AddRange(catalog.Entries.SelectMany(e => e.Capabilities).Distinct().Order().Cast<object>().ToArray());
            filterRow.Controls.Add(filter); layout.Controls.Add(filterRow, 0, 1);
            var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, AccessibleName = "Legacy project alternatives" };
            list.Columns.Add("Project", 345); list.Columns.Add("Review status", 220);
            layout.Controls.Add(list, 0, 2);
            var details = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, AccessibleName = "Legacy project review and test evidence" };
            layout.Controls.Add(details, 0, 3);
            list.SelectedIndexChanged += (_, _) =>
            {
                if (list.SelectedItems.Count == 0) { details.Clear(); return; }
                var entry = (W101LegacyEntry)list.SelectedItems[0].Tag!;
                details.Text = entry.Archive + Environment.NewLine + Environment.NewLine
                    + "Fits into: " + string.Join(", ", entry.Capabilities) + Environment.NewLine
                    + entry.Destination + Environment.NewLine + Environment.NewLine
                    + "Before use: " + entry.Limitations + Environment.NewLine + Environment.NewLine
                    + "Test evidence:" + Environment.NewLine + string.Join(Environment.NewLine, entry.Evidence.Select(e => "• " + e))
                    + Environment.NewLine + Environment.NewLine + "Source ID: " + entry.Id + Environment.NewLine + "SHA-256: " + entry.Sha256;
            };
            filter.SelectedIndexChanged += (_, _) =>
            {
                list.BeginUpdate();
                try
                {
                    list.Items.Clear(); details.Clear();
                    foreach (var entry in catalog.ForCapability(filter.SelectedIndex == 0 ? null : filter.SelectedItem?.ToString()))
                        list.Items.Add(new ListViewItem([entry.Archive, entry.Qualification]) { Tag = entry });
                    if (list.Items.Count > 0) list.Items[0].Selected = true;
                }
                finally { list.EndUpdate(); }
            };
            filter.SelectedIndex = 0;
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException)
        {
            intro.Text = "The legacy library could not load: " + error.Message;
        }
    }
}
