using System.Drawing;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Central placeholder for shared UI color tokens to be used in a future polish pass.
/// NOTE: Do not change behavior yet; these constants are currently unused on purpose.
/// </summary>
internal static class UiColorTokens
{
    // Accent palette
    public static readonly Color AccentGreen = Color.FromArgb(34, 74, 54);
    public static readonly Color AccentGreenText = Color.LightGreen;
    public static readonly Color AccentRed = Color.FromArgb(104, 52, 52);
    public static readonly Color AccentRedText = Color.MistyRose;
    public static readonly Color AccentAmber = Color.Goldenrod;
    public static readonly Color AccentGold = Color.Gold;

    // Surfaces / text
    public static readonly Color SurfaceDeep = Color.FromArgb(18, 24, 38);
    public static readonly Color SurfaceDark = Color.FromArgb(26, 34, 58);
    public static readonly Color SurfaceMuted = Color.FromArgb(54, 54, 54);
    public static readonly Color TextMuted = Color.Silver;

    // Status badges / rows
    public static readonly Color StatusExternalBack = Color.FromArgb(24, 92, 96);
    public static readonly Color StatusExternalText = Color.FromArgb(180, 255, 255);
    public static readonly Color StatusReferenceBack = SurfaceMuted;
    public static readonly Color StatusReferenceText = TextMuted;
    public static readonly Color StatusDeprecatedBack = AccentRed;
    public static readonly Color StatusDeprecatedText = AccentRedText;
    public static readonly Color StatusDefaultBack = Color.FromArgb(62, 74, 110);
    public static readonly Color StatusDefaultText = AccentGold;

    // Profile chips / badges
    public static readonly Color ProfilePublicBack = Color.FromArgb(28, 94, 65);
    public static readonly Color ProfileExperimentalBack = Color.FromArgb(120, 30, 30);
    public static readonly Color FilterChipBack = Color.FromArgb(50, 74, 110);
}
