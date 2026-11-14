using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record W101TravelOptions(string Destination, bool ReturnHome = true, int MaximumHomeWaitSeconds = 60, int MaximumPages = 8, bool CaptureHomeCooldown = false)
{
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Destination) || Destination.Length > 64 || Destination.Any(c => !char.IsLetter(c) && c != ' ') ||
            (CaptureHomeCooldown && !ReturnHome) || MaximumHomeWaitSeconds is < 0 or > 120 || MaximumPages is < 1 or > 8)
            throw new InvalidDataException("Choose a world name, 1–8 pages and a Home wait of 0–120 seconds.");
    }
}

/// <summary>Data-only travel card, bounded menu geometry and cooldown decisions.
/// UI geometry stays parent-relative until calibrated against this frame's labels.</summary>
internal static class W101TravelCard
{
    internal const string Id = "w101.travel";
    internal sealed record Node(string Name, int Parent, RectangleF Bounds);
    internal static string Normalize(string text) => string.Concat(text.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    internal static W101TravelOptions Options(JobCardSpec card)
    {
        if (card.RoutineId != Id) throw new InvalidDataException("Choose a W101 Travel card.");
        var s = card.Settings ?? [];
        if (s.Keys.Any(key => key is not ("destination" or "returnHome" or "maximumHomeWaitSeconds" or "maximumPages" or "captureHomeCooldown")))
            throw new InvalidDataException("Use only documented travel settings.");
        if (!bool.TryParse(s.GetValueOrDefault("captureHomeCooldown", "false"), out var capture) ||
            !bool.TryParse(s.GetValueOrDefault("returnHome", "true"), out var home) ||
            !int.TryParse(s.GetValueOrDefault("maximumHomeWaitSeconds", "60"), out var wait) ||
            !int.TryParse(s.GetValueOrDefault("maximumPages", "8"), out var pages)) throw new InvalidDataException("Invalid travel limits.");
        var result = new W101TravelOptions(s.GetValueOrDefault("destination", ""), home, wait, pages, capture); result.Validate(); return result;
    }
    internal static W101TravelOptions Load(string path) => Options(JsonSerializer.Deserialize<JobCardSpec>(GameExperiments.BoundedBytes(path, 16384), JobDeckRules.Json) ?? throw new InvalidDataException("Empty travel card."));
    internal static IReadOnlyList<JsonElement> Trees(string path) => File.ReadLines(path).Select(line => JsonDocument.Parse(line)).Select(doc =>
    { using (doc) return doc.RootElement.Clone(); }).Where(root => root.TryGetProperty("kind", out var kind) && kind.GetString() == "tree").TakeLast(2).ToArray();
    internal static Node[] Nodes(JsonElement tree) => tree.GetProperty("nodes").EnumerateArray().Where(n => n.GetProperty("visible").GetBoolean() &&
        n.GetProperty("enabled").GetBoolean() && n.TryGetProperty("rawRectangle", out var r) && r.ValueKind == JsonValueKind.Array && r.GetArrayLength() == 4)
        .Select(n => { var r = n.GetProperty("rawRectangle"); return new Node(n.GetProperty("name").GetString()!, n.GetProperty("parent").GetInt32(), RectangleF.FromLTRB(r[0].GetSingle(), r[1].GetSingle(), r[2].GetSingle(), r[3].GetSingle())); }).ToArray();
    internal static bool Selected(IEnumerable<Node> nodes, string destination)
    {
        var list = nodes.ToArray(); var world = list.Where(n => Normalize(n.Name) == "WBTN" + Normalize(destination)).ToArray();
        var marker = list.Where(n => n.Name == "selectedWorldCheckMark").ToArray();
        if (world.Length != 1 || marker.Length != 1 || world[0].Parent != marker[0].Parent) return false;
        var rows = list.Where(n => n.Name.StartsWith("wbtn", StringComparison.Ordinal) && n.Parent == marker[0].Parent).ToArray();
        var center = marker[0].Bounds.Top + marker[0].Bounds.Height / 2;
        var targetCenter = world[0].Bounds.Top + world[0].Bounds.Height / 2;
        var others = rows.Where(n => n.Name != world[0].Name).Select(n => n.Bounds.Top + n.Bounds.Height / 2).ToArray();
        var radius = others.Length == 0 ? world[0].Bounds.Height : others.Min(y => Math.Abs(y - targetCenter)) / 2;
        return radius > 0 && Math.Abs(center - targetCenter) < radius;

    }
    internal static bool SelectionVerified(string trace, string destination)
    { var trees = Trees(trace); return trees.Count == 2 && trees.All(t => Selected(Nodes(t), destination)); }
    internal static RectangleF? Label(PetDanceMenuText text, string destination)
    {
        var words = text.Words.Where(w => w.Bounds.Left > text.ImageSize.Width * .55 && w.Bounds.Top > text.ImageSize.Height * .2 && w.Bounds.Bottom < text.ImageSize.Height * .76).ToArray();
        var hits = new List<RectangleF>();
        foreach (var word in words)
        {
            var row = words.Where(w => Math.Abs(w.Bounds.Top - word.Bounds.Top) < Math.Max(3, word.Bounds.Height / 2)).OrderBy(w => w.Bounds.Left).ToArray();
            for (var start = 0; start < row.Length; start++)
                for (var count = 1; count <= 4 && start + count <= row.Length; count++)
                {
                    var candidate = row.Skip(start).Take(count).ToArray();
                    if (Normalize(string.Concat(candidate.Select(w => w.Text))) == Normalize(destination))
                        hits.Add(candidate.Select(w => w.Bounds).Aggregate(RectangleF.Union));
                }
        }
        return hits.Distinct().Count() == 1 ? hits[0] : null;
    }
    internal static RectangleF? PageButton(IEnumerable<Node> nodes, PetDanceMenuText text, string name)
    {
        var list = nodes.ToArray(); var arrow = list.Where(n => n.Name == name).ToArray();
        if (arrow.Length != 1) return null;
        var pairs = list.Where(n => n.Name.StartsWith("wbtn", StringComparison.Ordinal) && n.Parent == arrow[0].Parent)
            .Select(n => (Node: n, Label: Label(text, n.Name[4..]))).Where(p => p.Label is not null).OrderBy(p => p.Node.Bounds.Top).ToArray();
        if (pairs.Length < 2) return null;
        static PointF Center(RectangleF r) => new(r.Left + r.Width / 2, r.Top + r.Height / 2);
        var first = pairs[0]; var last = pairs[^1]; var a = Center(first.Node.Bounds); var b = Center(last.Node.Bounds);
        var x = Center(first.Label!.Value); var y = Center(last.Label!.Value);
        if (b.Y - a.Y < 1) return null;
        var scale = (y.Y - x.Y) / (b.Y - a.Y);
        if (!float.IsFinite(scale) || scale is < .25f or > 4) return null;
        var dx = x.X - scale * a.X; var dy = x.Y - scale * a.Y;
        if (pairs.Any(p => { var raw = Center(p.Node.Bounds); var pixel = Center(p.Label!.Value); return Math.Abs(raw.Y * scale + dy - pixel.Y) > 8 || Math.Abs(raw.X * scale + dx - pixel.X) > 12; })) return null;
        var r = arrow[0].Bounds; var mapped = new RectangleF(r.X * scale + dx, r.Y * scale + dy, r.Width * scale, r.Height * scale);
        return PetDancePostGameClassifier.ValidBounds(mapped, text.ImageSize) ? mapped : null;
    }
    internal static bool NearOrigin(W101PosePoint origin, W101PosePoint arrival) =>
        new[] { origin.X, origin.Y, origin.Z, arrival.X, arrival.Y, arrival.Z }.All(double.IsFinite) &&
        Math.Sqrt(Math.Pow(origin.X - arrival.X, 2) + Math.Pow(origin.Y - arrival.Y, 2) + Math.Pow(origin.Z - arrival.Z, 2)) <= 300;
    internal static string HomeDecision(bool? enabled, int elapsedSeconds, int maximumSeconds) =>
        enabled == true ? "ready" : enabled is null ? "unknown" : elapsedSeconds >= maximumSeconds ? "expired" : "wait";

    internal static bool? HomeAvailable(string path)
    {
        var trees = Trees(path); if (trees.Count != 2) return null;
        var values = new List<bool>();
        foreach (var tree in trees)
        {
            var nodes = tree.GetProperty("nodes").EnumerateArray().ToArray();
            var parent = nodes.Where(n => n.GetProperty("name").GetString() == "compassAndTeleporterButtons" && n.GetProperty("visible").GetBoolean()).ToArray();
            if (parent.Length != 1) return null;
            var home = nodes.Where(n => n.GetProperty("name").GetString() == "GoHomeButton" && n.GetProperty("parent").ValueKind == JsonValueKind.Number && n.GetProperty("parent").GetInt32() == parent[0].GetProperty("index").GetInt32() && n.GetProperty("visible").GetBoolean()).ToArray();
            if (home.Length != 1) return null;
            var timer = nodes.Where(n => n.GetProperty("name").GetString() == "txtGoHomeTimer" && n.GetProperty("parent").ValueKind == JsonValueKind.Number && n.GetProperty("parent").GetInt32() == home[0].GetProperty("index").GetInt32()).ToArray();
            if (timer.Length != 1) return null;
            values.Add(home[0].GetProperty("enabled").GetBoolean() && !timer[0].GetProperty("visible").GetBoolean());
        }
        return values.Distinct().Count() == 1 ? values[0] : null;
    }

    internal sealed record MenuView(W101UiTreeResult Reading, W101ModuleImage Frame, PetDanceMenuText Text, string Trace);
    internal sealed record Selection(MenuView View, bool Verified, string Reason);
    internal static async Task<Selection> SelectAsync(MenuView first, W101TravelOptions options,
        Func<string, Task<MenuView>> read, Func<MenuView, RectangleF, Task<bool>> click, Func<bool> owned)
    {
        var view = first; var visited = new HashSet<string>();
        for (var i = 0; i < options.MaximumPages && owned(); i++)
        {
            if (view.Reading.Loading != false || !W101WorldGateVisit.MenuMatches(view.Text.Text)) return new(view, false, "menu-unverified");
            var trees = Trees(view.Trace); if (trees.Count != 2) return new(view, false, "tree-pair-missing");
            var nodes = Nodes(trees[^1]);
            var target = nodes.Where(n => Normalize(n.Name) == "WBTN" + Normalize(options.Destination)).ToArray();
            if (target.Length == 1 && Label(view.Text, options.Destination) is { } label)
            {
                if (SelectionVerified(view.Trace, options.Destination)) return new(view, true, "already-selected");
                if (!await click(view, label).ConfigureAwait(false)) return new(view, false, "selection-input-rejected");
                view = await read("selected-world").ConfigureAwait(false);
                return new(view, owned() && SelectionVerified(view.Trace, options.Destination), "selection-observed");
            }
            var signature = string.Join("|", nodes.Where(n => n.Name.StartsWith("wbtn", StringComparison.Ordinal)).Select(n => n.Name).Order());
            if (signature.Length == 0 || !visited.Add(signature)) return new(view, false, "world-unavailable-page-cycle");
            if (i + 1 == options.MaximumPages) break;
            if (PageButton(nodes, view.Text, "leftButton") is not { } previous || !await click(view, previous).ConfigureAwait(false)) return new(view, false, "page-control-unqualified");
            view = await read("world-page-" + i).ConfigureAwait(false);
        }
        return new(view, false, "page-budget-or-takeover");
    }
}
