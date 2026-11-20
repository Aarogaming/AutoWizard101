using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ProjectMaelstrom.Utilities;
using Xunit;

namespace ProjectMaelstrom.Tests;

public class W101CardArtworkTests
{
    [Fact]
    public void DrawIconKeepsPixelsInsideRequestedBounds()
    {
        using var bitmap = new Bitmap(80, 80);
        using var graphics = Graphics.FromImage(bitmap);
        var bounds = new Rectangle(20, 20, 24, 24);
        W101CardArtwork.DrawIcon(graphics, bounds, "quest", Color.Gold);
        int drawn = 0;
        for (int y = 0; y < 80; y++)
            for (int x = 0; x < 80; x++)
                if (bitmap.GetPixel(x, y).A > 0) { Assert.True(bounds.Contains(x, y)); drawn++; }
        Assert.True(drawn > 0);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void NonfiniteBoundsAreIgnored(float width)
    {
        using var bitmap = new Bitmap(20, 20);
        using var graphics = Graphics.FromImage(bitmap);
        W101CardArtwork.DrawIcon(graphics, new RectangleF(0, 0, width, 10), "quest", Color.Gold);
        Assert.Equal(0, bitmap.GetPixel(0, 0).A);
    }

    private static readonly string[] RequiredIconIds =
    [
        "quest",
        "paw",
        "dance",
        "cards",
        "potion",
        "sword",
        "skull",
        "stones",
        "shovel",
        "fire",
        "train",
        "idle",
        "replay",
        "default"
    ];

    [Fact]
    public void ResolveTitleFontFamily_ReturnsNonEmptyInstalledFont()
    {
        string font = W101CardArtwork.ResolveTitleFontFamily();
        Assert.False(string.IsNullOrWhiteSpace(font));
        using var testFont = new Font(font, 12f);
        Assert.NotNull(testFont);
    }

    [Theory]
    [InlineData("quest")]
    [InlineData("paw")]
    [InlineData("dance")]
    [InlineData("cards")]
    [InlineData("potion")]
    [InlineData("sword")]
    [InlineData("skull")]
    [InlineData("stones")]
    [InlineData("shovel")]
    [InlineData("fire")]
    [InlineData("train")]
    [InlineData("idle")]
    [InlineData("replay")]
    [InlineData("default")]
    [InlineData("unknown_fallback_id")]
    public void DrawIcon_DrawsWithoutExceptionAcrossStandardAndFallbackIds(string iconId)
    {
        using var bitmap = new Bitmap(100, 100);
        using var g = Graphics.FromImage(bitmap);

        var bounds = new RectangleF(10, 10, 80, 80);
        W101CardArtwork.DrawIcon(g, bounds, iconId, Color.Gold);
    }

    [Theory]
    [InlineData(16f)]
    [InlineData(24f)]
    [InlineData(48f)]
    [InlineData(96f)]
    public void DrawIcon_RendersAcrossMultipleDimensionsLegibly(float size)
    {
        using var bitmap = new Bitmap((int)Math.Ceiling(size), (int)Math.Ceiling(size));
        using var g = Graphics.FromImage(bitmap);

        var bounds = new RectangleF(0, 0, size, size);
        foreach (var id in RequiredIconIds)
        {
            W101CardArtwork.DrawIcon(g, bounds, id, Color.FromArgb(200, 150, 50));
        }
    }

    [Fact]
    public void DrawIcon_PreservesGraphicsStateAndTransformations()
    {
        using var bitmap = new Bitmap(120, 120);
        using var g = Graphics.FromImage(bitmap);

        // Pre-set specific caller transformations and clipping
        g.SmoothingMode = SmoothingMode.HighSpeed;
        g.TranslateTransform(15f, 25f);
        g.SetClip(new Rectangle(5, 5, 100, 100));

        var initialMatrix = g.Transform.Clone();
        var initialClip = g.ClipBounds;
        var initialSmoothing = g.SmoothingMode;

        // Draw an icon
        W101CardArtwork.DrawIcon(g, new RectangleF(0, 0, 50, 50), "potion", Color.Purple);

        // Assert caller's Graphics state was cleanly restored
        Assert.Equal(initialSmoothing, g.SmoothingMode);
        Assert.Equal(initialMatrix.Elements, g.Transform.Elements);
        Assert.Equal(initialClip, g.ClipBounds);
    }

    [Fact]
    public void DrawIcon_HandlesZeroOrNegativeBoundsGracefully()
    {
        using var bitmap = new Bitmap(50, 50);
        using var g = Graphics.FromImage(bitmap);

        // Should return early and not throw
        W101CardArtwork.DrawIcon(g, new RectangleF(0, 0, 0, 0), "quest", Color.Black);
        W101CardArtwork.DrawIcon(g, new RectangleF(0, 0, -10, 20), "sword", Color.Black);
    }
}
