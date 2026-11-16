namespace ProjectMaelstrom.Utilities;

/// <summary>Bounded exact-edge sheet tracing for already reviewed support faces.
/// Produces piecewise heights through ramp creases. Never classifies raw BCD roles.
/// No vertex welding; ambiguous layers, nonmanifold topology and holes reject.</summary>
internal static class NavigationFloorSheet
{
    internal static IReadOnlyList<NavigationPoint>? Trace(IReadOnlyList<ConnectorTriangle> faces,
        NavigationPoint start, NavigationPoint end, double radius, double maximumGrade, double maximumCrease,
        double heightTolerance)
    {
        if (faces.Count is < 1 or > 2048 || !new[] { radius, maximumGrade, maximumCrease, heightTolerance }.All(double.IsFinite) ||
            radius <= 0 || maximumGrade < 0 || maximumCrease < 0 || maximumCrease > Math.PI / 2 || heightTolerance < 0) return null;
        bool Finite(NavigationPoint p) => new[] { p.X, p.Y, p.Z }.All(double.IsFinite);
        if (!Finite(start) || !Finite(end)) return null;
        var normals = new (double X, double Y, double Z)[faces.Count];
        var edges = new Dictionary<(NavigationPoint, NavigationPoint), List<int>>();
        var adjacency = Enumerable.Range(0, faces.Count).Select(_ => new List<int>()).ToArray();
        bool Before(NavigationPoint a, NavigationPoint b) => a.X < b.X || a.X == b.X && (a.Y < b.Y || a.Y == b.Y && a.Z < b.Z);
        for (int i = 0; i < faces.Count; i++)
        {
            var t = faces[i]; if (!Finite(t.A) || !Finite(t.B) || !Finite(t.C)) return null;
            double ux = t.B.X - t.A.X, uy = t.B.Y - t.A.Y, uz = t.B.Z - t.A.Z;
            double vx = t.C.X - t.A.X, vy = t.C.Y - t.A.Y, vz = t.C.Z - t.A.Z;
            double x = uy * vz - uz * vy, y = uz * vx - ux * vz, z = ux * vy - uy * vx;
            double length = Math.Sqrt(x * x + y * y + z * z);
            if (!double.IsFinite(length) || Math.Abs(z) < 1e-10 || Math.Sqrt(x * x + y * y) / Math.Abs(z) > maximumGrade) return null;
            double sign = Math.Sign(z); normals[i] = (sign * x / length, sign * y / length, sign * z / length);
            var points = new[] { t.A, t.B, t.C };
            for (int j = 0; j < 3; j++)
            {
                var a = points[j]; var b = points[(j + 1) % 3]; var key = Before(a, b) ? (a, b) : (b, a);
                if (!edges.TryGetValue(key, out var owners)) edges[key] = owners = [];
                owners.Add(i); if (owners.Count > 2) return null;
            }
        }
        foreach (var (edge, owners) in edges.Where(e => e.Value.Count == 2))
        {
            int a = owners[0], b = owners[1]; var n = normals[a]; var m = normals[b];
            if (n.X * m.X + n.Y * m.Y + n.Z * m.Z < Math.Cos(maximumCrease)) continue;
            double Side(ConnectorTriangle t)
            {
                var p = new[] { t.A, t.B, t.C }.First(p => p != edge.Item1 && p != edge.Item2);
                return Cross(edge.Item1, edge.Item2, p);
            }
            if (Math.Sign(Side(faces[a])) == Math.Sign(Side(faces[b]))) return null;
            adjacency[a].Add(b); adjacency[b].Add(a);
        }
        double Height(int i, NavigationPoint p)
        {
            var n = normals[i]; var a = faces[i].A;
            return a.Z - (n.X * (p.X - a.X) + n.Y * (p.Y - a.Y)) / n.Z;
        }
        var seeds = Enumerable.Range(0, faces.Count).Where(i => Contains(faces[i], start) && Math.Abs(Height(i, start) - start.Z) <= heightTolerance).ToArray();
        if (seeds.Length == 0) return null;
        var seen = new HashSet<int> { seeds[0] }; var queue = new Queue<int>(); queue.Enqueue(seeds[0]);
        while (queue.TryDequeue(out int i)) foreach (int j in adjacency[i]) if (seen.Add(j)) queue.Enqueue(j);
        if (seeds.Any(i => !seen.Contains(i))) return null;
        // Global XY boundary distance is conservative even for distant folded sheets.
        foreach (var (edge, owners) in edges)
            if (owners.Any(seen.Contains) && (owners.Count != 2 || !owners.All(seen.Contains) || !adjacency[owners[0]].Contains(owners[1])))
                if (Distance(start, end, edge.Item1, edge.Item2) < radius) return null;
        var spans = new List<(double Lo, double Hi, int Face)>();
        foreach (int i in seen)
        {
            var t = faces[i]; var points = new[] { t.A, t.B, t.C, t.A };
            double sign = Math.Sign(Cross(t.A, t.B, t.C)), lo = 0, hi = 1;
            for (int j = 0; j < 3 && lo <= hi; j++)
            {
                double a = sign * Cross(points[j], points[j + 1], start), b = sign * Cross(points[j], points[j + 1], end), delta = b - a;
                if (delta == 0) { if (a < 0) hi = -1; }
                else if (delta > 0) lo = Math.Max(lo, -a / delta); else hi = Math.Min(hi, -a / delta);
            }
            if (lo <= hi) spans.Add((lo, hi, i));
        }
        var breaks = spans.SelectMany(s => new[] { s.Lo, s.Hi }).Append(0).Append(1).Distinct().Order().ToArray();
        NavigationPoint At(double t) => new(start.X + t * (end.X - start.X), start.Y + t * (end.Y - start.Y), 0);
        var result = new List<NavigationPoint>();
        for (int k = 0; k < breaks.Length - 1; k++)
        {
            double a = breaks[k], b = breaks[k + 1], mid = (a + b) / 2;
            var active = spans.Where(s => s.Lo <= mid && s.Hi >= mid).ToArray();
            if (active.Length == 0) return null;
            foreach (double t in new[] { a, b })
            {
                var p = At(t); double z = Height(active[0].Face, p);
                if (!double.IsFinite(z) || active.Any(s => Math.Abs(Height(s.Face, p) - z) > heightTolerance)) return null;
                if (result.Count > 0 && result[^1].X == p.X && result[^1].Y == p.Y)
                { if (Math.Abs(result[^1].Z - z) > heightTolerance) return null; }
                else result.Add(p with { Z = z });
            }
        }
        if (result.Count < 2 || Math.Abs(result[0].Z - start.Z) > heightTolerance || Math.Abs(result[^1].Z - end.Z) > heightTolerance) return null;
        return result.AsReadOnly();
    }

    private static double Cross(NavigationPoint a, NavigationPoint b, NavigationPoint p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
    private static bool Contains(ConnectorTriangle t, NavigationPoint p)
    {
        double sign = Math.Sign(Cross(t.A, t.B, t.C));
        return sign * Cross(t.A, t.B, p) >= 0 && sign * Cross(t.B, t.C, p) >= 0 && sign * Cross(t.C, t.A, p) >= 0;
    }
    private static double Distance(NavigationPoint a, NavigationPoint b, NavigationPoint c, NavigationPoint d)
    {
        double Point(NavigationPoint p, NavigationPoint x, NavigationPoint y)
        {
            double dx = y.X - x.X, dy = y.Y - x.Y, l = dx * dx + dy * dy;
            double t = l == 0 ? 0 : Math.Clamp(((p.X - x.X) * dx + (p.Y - x.Y) * dy) / l, 0, 1);
            return Math.Sqrt(Math.Pow(p.X - x.X - t * dx, 2) + Math.Pow(p.Y - x.Y - t * dy, 2));
        }
        if (Cross(a, b, c) * Cross(a, b, d) < 0 && Cross(c, d, a) * Cross(c, d, b) < 0) return 0;
        return Math.Min(Math.Min(Point(a, c, d), Point(b, c, d)), Math.Min(Point(c, a, b), Point(d, a, b)));
    }
}
