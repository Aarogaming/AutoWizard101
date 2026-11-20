namespace ProjectMaelstrom.Utilities;

internal sealed record W101DanceEntryControl(int Index, int? Parent, string[] Path, string Name,
    string? Text, bool? Visible, bool? Enabled, int[]? RawRectangle, bool? Selected, string SelectionReason);

/// <summary>Discovery evidence only. Parent-local rectangles and unknown selected
/// state must never be promoted directly into click bounds or energy approval.</summary>
internal sealed record W101DanceEntryContext(int SchemaVersion, long ObservedUtcTicks, string Qualification,
    string GeometrySpace, W101DanceEntryControl[] Controls)
{
    /// <summary>Parse discovery labels only after validating their observation.
    /// These numbers are not spending permission or a qualified live energy gate.</summary>
    internal static W101DanceEnergyCandidate? ReadEnergyCandidate(W101DanceEntryContext? value,
        bool included, W101UiTreeResult result, long now)
    {
        if (value is null || !Valid(value, included, result, now)) return null;
        int? Read(string name)
        {
            var matches = value.Controls.Where(c => c.Name == name && c.Path.Length == 5 &&
                c.Path[3] == "wndBkgEnergy").ToArray();
            if (matches.Length != 1 || matches[0].Visible != true) return null;
            var text = matches[0].Text?.Trim();
            if (text is null) return null;
            if (text.StartsWith("<center>", StringComparison.Ordinal) && text.EndsWith("</center>", StringComparison.Ordinal))
                text = text[8..^9].Trim();
            return text.Length is > 0 and <= 9 && text.All(c => c is >= '0' and <= '9') &&
                int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number)
                ? number : null;
        }
        var current = Read("txtYourEnergy"); var cost = Read("txtEnergyCost");
        return current.HasValue && cost.HasValue ? new(current.Value, cost.Value, value.ObservedUtcTicks) : null;
    }

    internal static bool Valid(W101DanceEntryContext? value, bool included, W101UiTreeResult result, long now)
    {
        if (value is null) return true;
        if (!included || result.Status != "observed" || result.Loading != false ||
            !result.CleanupConfirmed || !result.HookRestored || value.SchemaVersion != 1 ||
            value.Qualification != "structural-unqualified" || value.GeometrySpace != "raw-parent-layout-unqualified" ||
            value.ObservedUtcTicks <= 0 || value.ObservedUtcTicks != result.StatesObservedUtcTicks || value.ObservedUtcTicks > now || now - value.ObservedUtcTicks > TimeSpan.FromSeconds(5).Ticks ||
            value.Controls is not { Length: <= 96 }) return false;
        var ids = new HashSet<int>();
        foreach (var control in value.Controls)
        {
            if (control is null || control.Index is < 0 or >= 384 || !ids.Add(control.Index) ||
                control.Parent is < 0 or >= 384 || control.Visible is null || control.Enabled is null ||
                control.Selected is not null || control.SelectionReason != "selection-field-unqualified" ||
                control.Path is not { Length: >= 3 and <= 26 } ||
                control.Path[0] != "Root" || control.Path[1] != "WorldView" || control.Path[2] != "PetGameTracks" ||
                control.Path.Any(name => name is null || name.Length > 128 || name.Any(char.IsControl) ||
                    name == "[excluded]" || new[] { "chat", "whisper", "friend", "password", "login", "account", "guild" }.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase))) ||
                control.Path[^1] != control.Name || control.Text is { Length: > 160 } ||
                control.RawRectangle is { } rect && (rect.Length != 4 || rect.Any(n => n is < -65536 or > 65536) || rect[0] >= rect[2] || rect[1] >= rect[3])) return false;
            if (control.Text != null && !(control.Path.Length == 5 && control.Path[3] == "wndBkgEnergy" &&
                control.Name is "txtEnergyCost" or "txtYourEnergy")) return false;
        }
        return true;
    }
}

/// <summary>Unqualified values from one fresh entry observation; never authorizes Play.</summary>
internal sealed record W101DanceEnergyCandidate(int Current, int Cost, long ObservedUtcTicks)
{
    public string Qualification => "structural-unqualified";
}
