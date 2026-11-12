using System.Text.RegularExpressions;

namespace ProjectMaelstrom.Utilities;

internal static class GamePackNaming
{
    public static string SuggestGameId(string? workspace)
    {
        var name = Regex.Replace(workspace ?? "", @"\s*\(DEBUG\)\s*$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var id = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (id.Length == 0) return "my-game";
        if (Regex.IsMatch(id, @"\A(con|prn|aux|nul|com[1-9]|lpt[1-9])\z")) id = "game-" + id;
        return id[..Math.Min(id.Length, 64)].TrimEnd('-');
    }
}
