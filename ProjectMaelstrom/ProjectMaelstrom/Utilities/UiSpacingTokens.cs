using System.Windows.Forms;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Shared spacing tokens (WinForms-friendly). Keep values aligned to existing UI to avoid visual drift.
/// </summary>
internal static class UiSpacingTokens
{
    public const int SpaceXs = 2;
    public const int SpaceS = 4;
    public const int SpaceM = 8;
    public const int SpaceL = 12;
    public const int SpaceXl = 16;

    public static Padding PaddingXs => new Padding(SpaceXs);
    public static Padding PaddingS => new Padding(SpaceS);
    public static Padding PaddingM => new Padding(SpaceM);
    public static Padding PaddingL => new Padding(SpaceL);
    public static Padding PaddingXl => new Padding(SpaceXl);
}
