using System.Diagnostics;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom;

internal sealed class GameResearchForm : Form
{
    public GameResearch? Selected { get; private set; }
    public GameResearchForm(string dataRoot, string game)
    {
        Text = "Game Research";
        AppPreferences preferences;
        try { preferences = AppPreferences.Load(dataRoot); }
        catch (Exception ex) { preferences = new(false, false); MessageBox.Show("Could not load settings: " + ex.Message + "\nSuggestions are disabled until settings are repaired.", "Game Research"); }
        Size = new Size(760, 640); MinimumSize = new Size(700, 580);
        StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 10);
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(24) };
        body.Controls.Add(new Label { Text = "Research a game", Font = new Font(Font.FontFamily, 20, FontStyle.Bold), AutoSize = true });
        body.Controls.Add(new Label { Text = "Find a guide, then save a source and a hypothesis to test during sampling.\nResearch notes are unverified until you test them.", AutoSize = true, Margin = new Padding(0, 10, 0, 12) });
        body.Controls.Add(new Label { Text = "Game title", AutoSize = true });
        var title = new TextBox { Width = 650, MaxLength = 120, Text = game };
        body.Controls.Add(title);
        var results = new ComboBox { Width = 650, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Public game research results" };
        var discover = new Button { Text = "Find public game information in Maelstrom", AutoSize = true };
        discover.Click += async (_, _) =>
        {
            discover.Enabled = false;
            try
            {
                var found = await PublicGameSearch.SearchAsync(title.Text);
                if (IsDisposed) return;
                results.Items.Clear();
                foreach (var item in found) results.Items.Add(item);
                if (results.Items.Count > 0) results.SelectedIndex = 0;
                else MessageBox.Show(this, "No encyclopedia results. Try another title or use a guide URL.");
            }
            catch (Exception ex) { if (!IsDisposed) MessageBox.Show(this, ex.Message, "Public game search unavailable"); }
            finally { if (!IsDisposed) discover.Enabled = true; }
        };
        body.Controls.Add(discover);
        var firecrawl = new Button { Text = "Search with Firecrawl", AutoSize = true };
        firecrawl.Click += async (_, _) =>
        {
            firecrawl.Enabled = false;
            try
            {
                var found = await FirecrawlSearch.SearchAsync(title.Text, Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY") ?? "");
                if (IsDisposed) return;
                results.Items.Clear();
                foreach (var item in found) results.Items.Add(item);
                if (results.Items.Count > 0) results.SelectedIndex = 0;
                else MessageBox.Show(this, "No HTTPS sources found. Try another search.");
            }
            catch (Exception ex) { if (!IsDisposed) MessageBox.Show(this, ex.Message, "Firecrawl search"); }
            finally { if (!IsDisposed) firecrawl.Enabled = true; }
        };
        body.Controls.Add(firecrawl);
        body.Controls.Add(results);
        var search = new Button { Text = "Search the web for controls and tutorials", AutoSize = true, Margin = new Padding(0, 8, 0, 12) };
        search.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) return;
            var query = Uri.EscapeDataString(title.Text.Trim() + " controls tutorial gameplay guide");
            try { Process.Start(new ProcessStartInfo("https://www.google.com/search?q=" + query) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Search could not open"); }
        };
        body.Controls.Add(search);
        body.Controls.Add(new Label { Text = "Source URL", AutoSize = true });
        var url = new TextBox { Width = 650, MaxLength = 2048 };
        body.Controls.Add(url);
        results.SelectedIndexChanged += (_, _) => { if (results.SelectedItem is PublicGameResult result) url.Text = result.Url; };
        body.Controls.Add(new Label { Text = "What should we test?", AutoSize = true, Margin = new Padding(0, 12, 0, 0) });
        var hypothesis = new TextBox { Width = 650, Height = 90, Multiline = true, MaxLength = 2000, ScrollBars = ScrollBars.Vertical };
        body.Controls.Add(hypothesis);
        body.Controls.Add(new Label { Text = "Paste a short guide excerpt to suggest controls", AutoSize = true, Margin = new Padding(0, 12, 0, 0) });
        var excerpt = new TextBox { Width = 650, Height = 90, Multiline = true, MaxLength = 100000, ScrollBars = ScrollBars.Vertical };
        body.Controls.Add(excerpt);
        var fetch = new Button { Text = "Fetch guide text from URL", AutoSize = true };
        fetch.Click += async (_, _) =>
        {
            fetch.Enabled = false;
            try { excerpt.Text = await GuideReader.FetchAsync(url.Text.Trim()); }
            catch (Exception ex) { if (!IsDisposed) MessageBox.Show(this, ex.Message + "\nYou can also paste an excerpt manually.", "Guide retrieval"); }
            finally { if (!IsDisposed) fetch.Enabled = true; }
        };
        body.Controls.Add(fetch);
        var extract = new Button { Text = "Suggest control hypotheses", AutoSize = true };
        extract.Click += (_, _) =>
        {
            try
            {
                var hints = GuideHints.Extract(excerpt.Text);
                hypothesis.Text = hints.Count == 0 ? "" : string.Join(Environment.NewLine, hints)[..Math.Min(2000, string.Join(Environment.NewLine, hints).Length)];
                if (hints.Count == 0) MessageBox.Show(this, "No control sentences found. Enter a hypothesis yourself.");
                else MessageBox.Show(this, "Review these unverified suggestions before saving. They are not commands.");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Guide suggestions"); }
        };
        if (preferences.ControlSuggestions) body.Controls.Add(extract);
        var guidance = new Button { Text = "Suggest tips and hidden-item clues", AutoSize = true };
        guidance.Click += (_, _) =>
        {
            try
            {
                var hints = GuideHints.ExtractGuidance(excerpt.Text);
                var joined = string.Join(Environment.NewLine, hints);
                if (joined.Length == 0) { MessageBox.Show(this, "No candidate tips or clues found in this text."); return; }
                hypothesis.Text = joined[..Math.Min(2000, joined.Length)];
                MessageBox.Show(this, "These are unverified source excerpts, not confirmed locations. Review them before saving.");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Guide suggestions"); }
        };
        if (preferences.TipsAndHiddenItems) body.Controls.Add(guidance);
        var save = new Button { Text = "Save and use in sampler", AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
        save.Click += (_, _) =>
        {
            try
            {
                var tags = new List<string>();
                if (GuideHints.Extract(hypothesis.Text).Count > 0) tags.Add("controls");
                if (GuideHints.ExtractGuidance(hypothesis.Text).Count > 0) tags.Add("guidance");
                var note = GameResearch.Create(title.Text, url.Text, hypothesis.Text) with { SuggestedTags = tags.ToArray() };
                note.Save(dataRoot); Selected = note; DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Game Research"); }
        };
        body.Controls.Add(save);
        var open = new Button { Text = "Use an existing research note…", AutoSize = true };
        open.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Filter = "Research note|*.json", InitialDirectory = Path.Combine(dataRoot, "research") };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            try { Selected = GameResearch.Load(picker.FileName); DialogResult = DialogResult.OK; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Game Research"); }
        };
        body.Controls.Add(open);
        Controls.Add(body);
    }
}
