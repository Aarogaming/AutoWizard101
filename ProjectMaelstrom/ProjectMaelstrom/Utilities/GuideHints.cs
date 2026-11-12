using System.Text.RegularExpressions;

namespace ProjectMaelstrom.Utilities;

/// <summary>Extracts candidate control sentences from user-supplied guide text; never executes them.</summary>
internal static class GuideHints
{
    private static readonly Regex Controls = new(@"\b(press|hold|click|tap|drag|move|arrow keys|spacebar|keyboard|mouse|controller|wasd)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Guidance = new(@"\b(tip|strategy|secret|hidden|collectible|treasure|location|reward|behind|unlock)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static IReadOnlyList<string> Extract(string text)
    {
        return ExtractWith(text, Controls);
    }

    public static IReadOnlyList<string> ExtractGuidance(string text) => ExtractWith(text, Guidance);

    private static IReadOnlyList<string> ExtractWith(string text, Regex pattern)
    {
        if (text.Length > 100_000) throw new InvalidDataException("Guide text must be at most 100,000 characters.");
        return Regex.Split(text, @"(?<=[.!?])\s+|[\r\n]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
            .Select(s => s.Trim()).Where(s => s.Length is > 0 and <= 500 && pattern.IsMatch(s))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();
    }
}
