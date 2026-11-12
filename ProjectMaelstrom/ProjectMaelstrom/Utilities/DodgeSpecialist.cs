using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ProjectMaelstrom.Utilities;

internal sealed record DodgeObservation(PointF? Player, IReadOnlyList<PointF> Threats, string Advice, string Detail, string State = "unknown");

/// <summary>Bounded, game-specific visual advice for the stock Godot Dodge demo. Never sends input.</summary>
internal static class DodgeSpecialist
{
    // A small fixed working image keeps the amount of CPU work independent of window resolution.
    private const int Width = 160;
    private const int Height = 250;

    public static DodgeObservation Analyze(Bitmap source, DodgeGameContext? context = null)
    {
        // This recognizer is calibrated only for the portrait Godot sample, including window chrome.
        if (source.Width < 100 || source.Height < 150 || source.Width / (double)source.Height is < 0.5 or > 0.8)
            return new(null, Array.Empty<PointF>(), "Unknown", "Expected the portrait Godot Dodge window.");
        using var small = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(small))
        {
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.DrawImage(source, 0, 0, Width, Height);
        }
        var pixels = new byte[Width * Height * 4];
        var bits = small.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { Marshal.Copy(bits.Scan0, pixels, 0, pixels.Length); }
        finally { small.UnlockBits(bits); }

        if (context != null)
        {
            var uiState = MatchState(pixels, context);
            if (uiState != null) return new(null, Array.Empty<PointF>(), "Unknown", "Matched a game-state signature.", uiState);
        }
        // Context-aware WSL frames contain the game viewport only. Legacy desktop
        // capture includes window chrome, which must remain excluded.
        var top = context == null ? 35 : 3;
        var players = Components(pixels, player: true, top, stableEye: context != null);
        if (context != null && players.Count is >= 2 and <= 4)
        {
            // The score can cut the one-player sprite into nearby pieces. Merge
            // only fragments whose union is sprite-sized and intersects the score.
            var largest = players.MaxBy(c => c.Count)!;
            var fragments = players.Where(c => c != largest).ToArray();
            var union = players.Select(c => c.Bounds).Aggregate(Rectangle.Union);
            var plausibleFragments = fragments.All(c => (c.Count < 80 || c.Bounds.Height <= 8) && Rectangle.Inflate(largest.Bounds, 12, 12).IntersectsWith(c.Bounds));
            if (plausibleFragments && union.Top < 25 && union.Right >= 66 && union.Left <= 95 && union.Width <= 30 && union.Height <= 36)
                players = [new(new PointF((union.Left + union.Right) / 2f / Width, (union.Top + union.Bottom) / 2f / Height), players.Sum(c => c.Count), union)];
        }
        var threatComponents = Components(pixels, player: false, top, stableEye: false);
        if (threatComponents.Count > 32)
            return new(null, Array.Empty<PointF>(), "Unknown", "Too many threat candidates; no movement advice.");
        var threats = threatComponents.Select(c => c.Center).ToArray();
        if (context != null && players.Count == 0)
        {
            // Only recover a unique pupil under the score when nearby pixels
            // also resemble the player. Score shapes alone are not evidence.
            var (pupil, ambiguous) = ScoreAreaPupil(source, new Rectangle(62, 0, 35, 25));
            if (!ambiguous && pupil is PointF landmark && HasPlayerColorNear(pixels, landmark))
                return new(landmark, threats, Recommend(landmark, threats), "Player landmark and color found beneath the score.", "playing");
        }
        if (players.Count != 1)
            return new(null, threats, "Unknown", $"Player candidates: {players.Count}; bounds {string.Join(", ", players.Select(p => $"{p.Bounds}:{p.Count}"))}. No movement advice.");
        var player = players[0].Center;
        if (context != null && players[0].Bounds.Top < 25 && players[0].Bounds.Right >= 60 && players[0].Bounds.Left <= 100)
        {
            var (pupil, ambiguous) = ScoreAreaPupil(source, players[0].Bounds);
            if (ambiguous)
                return new(null, threats, "Unknown", $"Multiple possible player landmarks near the score; no movement advice. Bounds: {players[0].Bounds}");
            player = ScoreAreaOpaquePlayer(source, players[0].Bounds) ?? pupil ?? player;
        }
        return new(player, threats, Recommend(player, threats), $"Player found · {threats.Length} possible threats · experimental advice only.", "playing");
    }

    private static string? MatchState(byte[] pixels, DodgeGameContext context)
    {
        var scores = new List<(string State, double Score)>();
        foreach (var (name, mask) in context.StateMasks)
        {
            var index = 0; var expected = 0; var intersection = 0; var extra = 0;
            for (var y = context.MaskY; y < context.MaskY + context.MaskHeight; y++)
                for (var x = context.MaskX; x < context.MaskX + context.MaskWidth; x++, index++)
                {
                    var i = (y * Width + x) * 4;
                    var bright = pixels[i] >= 235 && pixels[i + 1] >= 235 && pixels[i + 2] >= 235;
                    var wanted = (mask[index / 8] & (1 << (7 - index % 8))) != 0;
                    if (wanted) expected++;
                    if (bright && wanted) intersection++;
                    if (bright && !wanted) extra++;
                }
            scores.Add((name, intersection / (double)(expected + .2 * extra)));
        }
        var sorted = scores.OrderByDescending(s => s.Score).ToArray();
        return sorted.Length > 1 && sorted[0].Score >= .70 && sorted[0].Score - sorted[1].Score >= .12 ? sorted[0].State : null;
    }

    private sealed record Component(PointF Center, int Count, Rectangle Bounds);

    private static bool HasPlayerColorNear(byte[] pixels, PointF landmark)
    {
        var cx = (int)(landmark.X * Width); var cy = (int)(landmark.Y * Height);
        var count = 0; var minX = Width; var maxX = 0; var minY = Height; var maxY = 0;
        for (var y = Math.Max(0, cy - 7); y <= Math.Min(Height - 1, cy + 12); y++)
            for (var x = Math.Max(0, cx - 8); x <= Math.Min(Width - 1, cx + 8); x++)
            {
                var i = (y * Width + x) * 4;
                var b = pixels[i]; var g = pixels[i + 1]; var r = pixels[i + 2];
                if (r is < 115 or > 195 || g is < 95 or > 175 || b is < 95 or > 175 || r < g || r - g > 40 || Math.Abs(g - b) > 25) continue;
                count++; minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
        return count >= 14 && maxX - minX >= 3 && maxY - minY >= 3;
    }

    // When score text hides the pupil, the opaque body color is more stable
    // than a bounding box that may include the translucent movement trail.
    private static PointF? ScoreAreaOpaquePlayer(Bitmap source, Rectangle reducedBounds)
    {
        var left = Math.Max(0, reducedBounds.Left * source.Width / Width - 12);
        var top = Math.Max(0, reducedBounds.Top * source.Height / Height - 12);
        var right = Math.Min(source.Width, (reducedBounds.Right * source.Width + Width - 1) / Width + 12);
        var bottom = Math.Min(source.Height, (reducedBounds.Bottom * source.Height + Height - 1) / Height + 12);
        var width = right - left; var height = bottom - top;
        if (width <= 0 || height <= 0 || width > 150 || height > 150) return null;
        using var crop = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(crop)) graphics.DrawImage(source, new Rectangle(0, 0, width, height), new Rectangle(left, top, width, height), GraphicsUnit.Pixel);
        var bytes = new byte[width * height * 4];
        var bits = crop.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { Marshal.Copy(bits.Scan0, bytes, 0, bytes.Length); }
        finally { crop.UnlockBits(bits); }
        var count = 0; var sx = 0; var sy = 0; var minX = width; var maxX = 0; var minY = height; var maxY = 0;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                if (bytes[i] is < 121 or > 127 || bytes[i + 1] is < 124 or > 130 || bytes[i + 2] is < 145 or > 151) continue;
                count++; sx += x; sy += y; minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
        return count >= 100 && maxX - minX >= 10 && maxY - minY >= 10
            ? new PointF((left + sx / (float)count) / source.Width, (top + sy / (float)count) / source.Height)
            : null;
    }

    // The eye's small dark pupil remains visible in many frames where white
    // score digits obscure the player. Return no estimate if it is ambiguous.
    internal static (PointF? Position, bool Ambiguous) ScoreAreaPupil(Bitmap source, Rectangle reducedBounds)
    {
        // Inspect a wider bounded area so a clipped enemy or second eye can be
        // classified by its actual shape rather than the small visible fragment.
        const int margin = 32;
        var left = Math.Max(0, reducedBounds.Left * source.Width / Width - margin);
        var top = Math.Max(0, reducedBounds.Top * source.Height / Height - margin);
        var right = Math.Min(source.Width, (reducedBounds.Right * source.Width + Width - 1) / Width + margin);
        var bottom = Math.Min(source.Height, (reducedBounds.Bottom * source.Height + Height - 1) / Height + margin);
        var width = right - left; var height = bottom - top;
        if (width <= 0 || height <= 0 || width > 200 || height > 200) return (null, true);
        using var crop = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(crop)) graphics.DrawImage(source, new Rectangle(0, 0, width, height), new Rectangle(left, top, width, height), GraphicsUnit.Pixel);
        var bytes = new byte[width * height * 4];
        var bits = crop.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { Marshal.Copy(bits.Scan0, bytes, 0, bytes.Length); }
        finally { crop.UnlockBits(bits); }
        var dark = new bool[width * height];
        for (var i = 0; i < dark.Length; i++)
        {
            var p = i * 4; var b = bytes[p]; var g = bytes[p + 1]; var r = bytes[p + 2];
            dark[i] = r < 115 && g < 105 && b < 105 && r >= g;
        }
        var queue = new int[dark.Length]; var candidates = new List<PointF>(); var clippedCandidate = false;
        for (var i = 0; i < dark.Length; i++)
        {
            if (!dark[i]) continue;
            var head = 0; var tail = 0; var sx = 0; var sy = 0; var white = 0; var clipped = false;
            queue[tail++] = i; dark[i] = false;
            while (head < tail)
            {
                var point = queue[head++]; var x = point % width; var y = point / width;
                sx += x; sy += y;
                clipped |= x == 0 || y == 0 || x == width - 1 || y == height - 1;
                CheckWhite(x - 2, y); CheckWhite(x + 2, y); CheckWhite(x, y - 2); CheckWhite(x, y + 2);
                if (x > 0) Add(point - 1); if (x + 1 < width) Add(point + 1);
                if (y > 0) Add(point - width); if (y + 1 < height) Add(point + width);
                void Add(int next) { if (dark[next]) { dark[next] = false; queue[tail++] = next; } }
                void CheckWhite(int wx, int wy)
                {
                    if (wx < 0 || wx >= width || wy < 0 || wy >= height) return;
                    var p = (wy * width + wx) * 4;
                    if (bytes[p] >= 235 && bytes[p + 1] >= 235 && bytes[p + 2] >= 235) white++;
                }
            }
            // A single antialiased score pixel beside the pupil is not a
            // second landmark; larger distinct pupils remain ambiguous.
            if (tail is >= 2 and <= 60 && white >= 3)
            {
                // An unresolved clipped candidate could be another eye; do
                // not silently select a different candidate in that case.
                if (clipped) clippedCandidate = true;
                else candidates.Add(new PointF((left + sx / (float)tail) / source.Width, (top + sy / (float)tail) / source.Height));
            }
        }
        return (candidates.Count == 1 && !clippedCandidate ? candidates[0] : null, candidates.Count > 1 || clippedCandidate);
    }
    private static List<Component> Components(byte[] pixels, bool player, int top, bool stableEye)
    {
        var mask = new bool[Width * Height];
        // Ignore window chrome and score; red eyes and the player's gray ring are distinctive in this demo.
        for (var y = top; y < Height - 2; y++)
            for (var x = 1; x < Width - 1; x++)
            {
                var i = (y * Width + x) * 4;
                var b = pixels[i]; var g = pixels[i + 1]; var r = pixels[i + 2];
                mask[y * Width + x] = player
                    ? r is >= 115 and <= 195 && g is >= 95 and <= 175 && b is >= 95 and <= 175 && r >= g && r - g <= 40 && Math.Abs(g - b) <= 25
                    : r >= 165 && r > g * 1.8 && r > b * 1.5 && g < 115;
            }
        var result = new List<Component>();
        var queue = new int[Width * Height];
        for (var index = 0; index < mask.Length; index++)
        {
            if (!mask[index]) continue;
            var head = 0; var tail = 0; var sumX = 0; var sumY = 0;
            var minX = Width; var maxX = 0; var minY = Height; var maxY = 0;
            queue[tail++] = index; mask[index] = false;
            while (head < tail)
            {
                var p = queue[head++]; var x = p % Width; var y = p / Width;
                sumX += x; sumY += y;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                if (x > 0) Add(p - 1);
                if (x < Width - 1) Add(p + 1);
                if (y > 0) Add(p - Width);
                if (y < Height - 1) Add(p + Width);
                void Add(int next) { if (mask[next]) { mask[next] = false; queue[tail++] = next; } }
            }
            if (tail < (player ? 20 : 8) || tail > (player ? 400 : 350)) continue;
            if (player)
            {
                // The opaque player has a bright eye; translucent trail sprites do not.
                // Still require a unique candidate instead of guessing between players.
                var bright = 0; var brightX = 0; var brightY = 0;
                for (var y = minY; y <= maxY; y++)
                    for (var x = minX; x <= maxX; x++)
                    {
                        var i = (y * Width + x) * 4;
                        if (pixels[i] >= 235 && pixels[i + 1] >= 235 && pixels[i + 2] >= 235) { bright++; brightX += x; brightY += y; }
                    }
                if (bright < 8) continue;
                // White score digits can overlap the eye and bias its centroid.
                // Near the score, use the gray component's bounds instead.
                if (stableEye && minY < 25 && maxX >= 60 && minX <= 100)
                {
                    result.Add(new(new PointF((minX + maxX + 1) / 2f / Width, (minY + maxY + 1) / 2f / Height), tail, Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1)));
                    continue;
                }
                // A motion trail can merge with the opaque sprite and drag the
                // component centroid toward an old position. Use the bright eye
                // consistently in calibrated frames and in stretched components.
                if (stableEye || maxX - minX > 24 || maxY - minY > 24)
                {
                    result.Add(new(new PointF(brightX / (float)bright / Width, brightY / (float)bright / Height), tail, Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1)));
                    continue;
                }
            }
            result.Add(new(new PointF(sumX / (float)tail / Width, sumY / (float)tail / Height), tail, Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1)));
        }
        return result;
    }

    internal static string Recommend(PointF player, IReadOnlyList<PointF> threats)
    {
        if (threats.Count == 0) return "Hold";
        // Evaluate short steps away from nearby threats and away from the edges.
        var options = new (string Name, float X, float Y)[] { ("Up", 0, -1), ("Down", 0, 1), ("Left", -1, 0), ("Right", 1, 0), ("Hold", 0, 0) };
        var best = "Hold"; var bestScore = double.NegativeInfinity;
        foreach (var option in options)
        {
            var x = player.X + option.X * .08; var y = player.Y + option.Y * .08;
            if (x < .08 || x > .92 || y < .15 || y > .94) continue;
            var score = threats.Min(t => Math.Sqrt(Math.Pow((x - t.X) * Width, 2) + Math.Pow((y - t.Y) * Height, 2)));
            score += Math.Min(Math.Min(x, 1 - x) * Width, Math.Min(y - .1, 1 - y) * Height) * .15;
            if (score > bestScore) { bestScore = score; best = option.Name; }
        }
        return best;
    }
}
