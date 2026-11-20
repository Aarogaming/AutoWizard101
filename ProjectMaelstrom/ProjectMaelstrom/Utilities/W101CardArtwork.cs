using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Lightweight native C# drawing helper for rendering card front vector icons and resolving title fonts.
/// Preserves and restores Graphics state, disposes created GDI objects, and performs zero I/O during painting.
/// </summary>
internal static class W101CardArtwork
{
    private static readonly string[] PreferredFontCandidates =
    [
        "Sherwood",
        "Palatino Linotype",
        "Georgia",
        "Garamond",
        "Bookman Old Style"
    ];

    private static string? _resolvedFontFamily;

    /// <summary>
    /// Resolves a readable game-style serif font family available on the current Windows host.
    /// Prefers historical fantasy serifs if present, falling back to standard Windows serifs.
    /// </summary>
    internal static string ResolveTitleFontFamily()
    {
        if (_resolvedFontFamily != null)
        {
            return _resolvedFontFamily;
        }

        try
        {
            var installedFamilies = FontFamily.Families;
            var installedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fam in installedFamilies)
            {
                using (fam) installedNames.Add(fam.Name);
            }

            foreach (var candidate in PreferredFontCandidates)
            {
                if (installedNames.Contains(candidate))
                {
                    _resolvedFontFamily = candidate;
                    return candidate;
                }
            }
        }
        catch
        {
            // Ignore any font enumeration errors and fall back gracefully
        }

        _resolvedFontFamily = "Georgia";
        return _resolvedFontFamily;
    }

    /// <summary>
    /// Draws a vector icon silhouette scaled to fit within the specified bounds.
    /// Preserves GraphicsContainer / GraphicsState so caller transformations and clip regions remain untouched.
    /// </summary>
    internal static void DrawIcon(Graphics graphics, RectangleF bounds, string iconId, Color ink)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (bounds.Width <= 0f || bounds.Height <= 0f || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height) ||
            !float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y))
        {
            return;
        }

        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.SetClip(bounds, CombineMode.Intersect);

            // Fit a normalized 100x100 virtual canvas into bounds
            float scale = Math.Min(bounds.Width / 100f, bounds.Height / 100f);
            float offsetX = bounds.X + ((bounds.Width - (100f * scale)) / 2f);
            float offsetY = bounds.Y + ((bounds.Height - (100f * scale)) / 2f);

            graphics.TranslateTransform(offsetX, offsetY);
            graphics.ScaleTransform(scale, scale);

            using var brush = new SolidBrush(ink);
            using var pen = new Pen(ink, 4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };

            string id = iconId?.Trim().ToLowerInvariant() ?? "default";
            switch (id)
            {
                case "quest":
                    DrawQuestCompass(graphics, brush, pen);
                    break;
                case "paw":
                    DrawPetPaw(graphics, brush);
                    break;
                case "dance":
                    DrawDanceArrows(graphics, brush, pen);
                    break;
                case "cards":
                    DrawDeckCards(graphics, brush, pen);
                    break;
                case "potion":
                    DrawPotionFlask(graphics, brush, pen);
                    break;
                case "sword":
                    DrawSword(graphics, brush, pen);
                    break;
                case "skull":
                    DrawSkull(graphics, brush);
                    break;
                case "stones":
                    DrawSorceryStones(graphics, brush, pen);
                    break;
                case "shovel":
                    DrawGardeningShovel(graphics, brush, pen);
                    break;
                case "fire":
                    DrawFlame(graphics, brush);
                    break;
                case "train":
                    DrawTrainTrack(graphics, brush, pen);
                    break;
                case "idle":
                    DrawIdleHourglass(graphics, brush, pen);
                    break;
                case "replay":
                    DrawReplayArrow(graphics, brush, pen);
                    break;
                default:
                    DrawDefaultStar(graphics, brush, pen);
                    break;
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    private static void DrawQuestCompass(Graphics g, Brush b, Pen p)
    {
        // 4-point golden quest navigation star
        PointF[] star =
        [
            new(50, 10), new(58, 42), new(90, 50), new(58, 58),
            new(50, 90), new(42, 58), new(10, 50), new(42, 42)
        ];
        g.FillPolygon(b, star);
        g.DrawEllipse(p, 40, 40, 20, 20);
    }

    private static void DrawPetPaw(Graphics g, Brush b)
    {
        // Main pad
        g.FillEllipse(b, 25, 45, 50, 40);
        // 4 toe pads
        g.FillEllipse(b, 15, 25, 16, 20);
        g.FillEllipse(b, 35, 12, 16, 22);
        g.FillEllipse(b, 55, 12, 16, 22);
        g.FillEllipse(b, 75, 25, 16, 20);
    }

    private static void DrawDanceArrows(Graphics g, Brush b, Pen p)
    {
        // Up & Right directional arrows (Dance minigame)
        PointF[] upArrow = [new(50, 15), new(70, 40), new(58, 40), new(58, 65), new(42, 65), new(42, 40), new(30, 40)];
        g.FillPolygon(b, upArrow);
        g.DrawArc(p, 25, 55, 50, 30, 30, 120);
    }

    private static void DrawDeckCards(Graphics g, Brush b, Pen p)
    {
        // Two overlapping playing/spell cards
        g.DrawRectangle(p, 15, 25, 45, 60);
        g.FillRectangle(b, 35, 15, 45, 60);
        using var cutBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
        g.FillEllipse(cutBrush, 48, 35, 20, 20);
    }

    private static void DrawPotionFlask(Graphics g, Brush b, Pen p)
    {
        // Flask stopper & neck
        g.FillRectangle(b, 42, 12, 16, 8);
        g.FillRectangle(b, 45, 20, 10, 15);
        // Round flask body
        g.FillEllipse(b, 22, 32, 56, 56);
        g.DrawEllipse(p, 22, 32, 56, 56);
    }

    private static void DrawSword(Graphics g, Brush b, Pen p)
    {
        // Blade
        PointF[] blade = [new(50, 10), new(56, 25), new(54, 65), new(46, 65), new(44, 25)];
        g.FillPolygon(b, blade);
        // Crossguard
        g.FillRectangle(b, 30, 65, 40, 8);
        // Hilt & Pommel
        g.FillRectangle(b, 47, 73, 6, 15);
        g.FillEllipse(b, 45, 87, 10, 10);
    }

    private static void DrawSkull(Graphics g, Brush b)
    {
        // Cranium
        g.FillEllipse(b, 25, 15, 50, 48);
        // Maxilla / teeth
        g.FillRectangle(b, 36, 52, 28, 25);
    }

    private static void DrawSorceryStones(Graphics g, Brush b, Pen p)
    {
        // Diamond gem
        PointF[] gem1 = [new(50, 15), new(80, 45), new(50, 85), new(20, 45)];
        g.FillPolygon(b, gem1);
        g.DrawPolygon(p, gem1);
    }

    private static void DrawGardeningShovel(Graphics g, Brush b, Pen p)
    {
        // Spade blade
        PointF[] blade = [new(35, 15), new(65, 15), new(65, 45), new(50, 65), new(35, 45)];
        g.FillPolygon(b, blade);
        // Shaft and handle
        g.DrawLine(p, 50, 65, 50, 88);
        g.DrawRectangle(p, 42, 88, 16, 8);
    }

    private static void DrawFlame(Graphics g, Brush b)
    {
        PointF[] flame =
        [
            new(50, 10), new(68, 38), new(80, 60), new(68, 85),
            new(50, 92), new(32, 85), new(20, 60), new(36, 45), new(42, 30)
        ];
        g.FillPolygon(b, flame);
    }

    private static void DrawTrainTrack(Graphics g, Brush b, Pen p)
    {
        // Two rails
        g.DrawLine(p, 30, 15, 30, 85);
        g.DrawLine(p, 70, 15, 70, 85);
        // Railroad ties
        g.FillRectangle(b, 20, 25, 60, 6);
        g.FillRectangle(b, 20, 45, 60, 6);
        g.FillRectangle(b, 20, 65, 60, 6);
    }

    private static void DrawIdleHourglass(Graphics g, Brush b, Pen p)
    {
        // Top and bottom plates
        g.FillRectangle(b, 25, 15, 50, 8);
        g.FillRectangle(b, 25, 77, 50, 8);
        // Glass curves
        PointF[] topBulb = [new(30, 23), new(70, 23), new(50, 50)];
        PointF[] botBulb = [new(50, 50), new(70, 77), new(30, 77)];
        g.FillPolygon(b, topBulb);
        g.FillPolygon(b, botBulb);
        g.DrawLine(p, 30, 23, 70, 77);
        g.DrawLine(p, 70, 23, 30, 77);
    }

    private static void DrawReplayArrow(Graphics g, Brush b, Pen p)
    {
        // Circular reload / replay arc
        g.DrawArc(p, 20, 20, 60, 60, 45, 270);
        // Arrow head
        PointF[] head = [new(68, 20), new(85, 35), new(65, 45)];
        g.FillPolygon(b, head);
    }

    private static void DrawDefaultStar(Graphics g, Brush b, Pen p)
    {
        PointF[] star =
        [
            new(50, 15), new(61, 38), new(85, 38), new(66, 53),
            new(73, 78), new(50, 63), new(27, 78), new(34, 53),
            new(15, 38), new(39, 38)
        ];
        g.FillPolygon(b, star);
    }
}
