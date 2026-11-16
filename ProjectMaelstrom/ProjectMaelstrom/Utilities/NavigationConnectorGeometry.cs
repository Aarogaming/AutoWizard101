namespace ProjectMaelstrom.Utilities;

internal sealed record ConnectorBox(NavigationPoint Min, NavigationPoint Max);
internal sealed record ConnectorFloor(double MinX, double MinY, double MaxX, double MaxY, double Z);
internal sealed record ConnectorTriangle(NavigationPoint A, NavigationPoint B, NavigationPoint C);

/// <summary>Conservative continuous checks for reviewed world-space boxes and flat
/// floor patches. Raw BCD primitives/flags are not accepted as qualified geometry.
/// Unsupported geometry must leave coverage incomplete.</summary>
internal static class NavigationConnectorGeometry
{
    internal static bool Clear(NavigationPoint start, NavigationPoint end, double radius,
        double height, double floorTolerance, bool coverageComplete,
        IReadOnlyList<ConnectorBox> obstacles, IReadOnlyList<ConnectorFloor> floors,
        IReadOnlyList<ConnectorTriangle>? ramps = null, double maximumGrade = 0)
    {
        bool Finite(NavigationPoint p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
        if (!coverageComplete || !Finite(start) || !Finite(end) ||
            !double.IsFinite(radius) || radius < 0 || !double.IsFinite(height) || height <= 0 ||
            !double.IsFinite(floorTolerance) || floorTolerance < 0 || obstacles.Count > 10000 || floors.Count > 10000 ||
            !double.IsFinite(maximumGrade) || maximumGrade < 0 || ramps?.Count > 100000)
            return false;
        if (!ObstaclesClear(start, end, radius, height, obstacles)) return false;
        var intervals = new List<(double Start, double End)>();
        foreach (var triangle in ramps ?? [])
        {
            if (!Finite(triangle.A) || !Finite(triangle.B) || !Finite(triangle.C)) return false;
            if (RampInterval(start, end, triangle, radius, floorTolerance, maximumGrade, out double lo, out double hi))
                intervals.Add((lo, hi));
        }
        if (ramps is not null)
            AddConvexSeams(start, end, ramps, radius, floorTolerance, maximumGrade, intervals);
        foreach (var f in floors)
        {
            if (!new[] { f.MinX, f.MinY, f.MaxX, f.MaxY, f.Z }.All(double.IsFinite) || f.MinX > f.MaxX || f.MinY > f.MaxY) return false;
            if (Math.Abs(start.Z - f.Z) > floorTolerance || Math.Abs(end.Z - f.Z) > floorTolerance) continue;
            if (f.MaxX - f.MinX < 2 * radius || f.MaxY - f.MinY < 2 * radius) continue;
            if (Interval(start, end, new(f.MinX + radius, f.MinY + radius, f.Z - floorTolerance),
                new(f.MaxX - radius, f.MaxY - radius, f.Z + floorTolerance), out double lo, out double hi)) intervals.Add((lo, hi));
        }
        double covered = 0;
        foreach (var part in intervals.OrderBy(p => p.Start))
        {
            if (part.Start > covered) return false;
            covered = Math.Max(covered, part.End);
            if (covered >= 1) return true;
        }
        return false;
    }

    internal static bool ObstaclesClear(NavigationPoint start, NavigationPoint end, double radius, double height,
        IReadOnlyList<ConnectorBox> obstacles)
    {
        bool Finite(NavigationPoint p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
        if (!Finite(start) || !Finite(end) || !double.IsFinite(radius) || radius < 0 || !double.IsFinite(height) || height <= 0 || obstacles.Count > 10000) return false;
        foreach (var box in obstacles)
        {
            if (!Finite(box.Min) || !Finite(box.Max) || box.Min.X > box.Max.X || box.Min.Y > box.Max.Y || box.Min.Z > box.Max.Z) return false;
            if (Interval(start, end, new(box.Min.X - radius, box.Min.Y - radius, box.Min.Z - height),
                new(box.Max.X + radius, box.Max.Y + radius, box.Max.Z), out _, out _)) return false;
        }
        return true;
    }

    // Only exact shared XYZ edges of two coplanar triangles forming a convex
    // quadrilateral are merged. No vertex welding, holes, T-junction repair or
    // tolerance-based bridging. Non-manifold edges are not merged.
    private static void AddConvexSeams(NavigationPoint start, NavigationPoint end,
        IReadOnlyList<ConnectorTriangle> triangles, double radius, double tolerance, double maximumGrade,
        List<(double Start, double End)> intervals)
    {
        var edges = new Dictionary<(NavigationPoint, NavigationPoint), List<(int Index, NavigationPoint Third)>>();
        bool Before(NavigationPoint a, NavigationPoint b) => a.X < b.X || a.X == b.X && (a.Y < b.Y || a.Y == b.Y && a.Z < b.Z);
        for (int i = 0; i < triangles.Count; i++)
        {
            var t = triangles[i]; var p = new[] { t.A, t.B, t.C };
            for (int j = 0; j < 3; j++)
            {
                var a = p[j]; var b = p[(j + 1) % 3];
                var key = Before(a, b) ? (a, b) : (b, a);
                if (!edges.TryGetValue(key, out var entries)) edges[key] = entries = [];
                if (entries.Count < 3) entries.Add((i, p[(j + 2) % 3]));
            }
        }
        foreach (var (edge, entries) in edges)
        {
            if (entries.Count != 2 || entries[0].Index == entries[1].Index) continue;
            var (a, b) = edge; var c = entries[0].Third; var d = entries[1].Third;
            double nx = (b.Y - a.Y) * (c.Z - a.Z) - (b.Z - a.Z) * (c.Y - a.Y);
            double ny = (b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z);
            double nz = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            double plane = nx * (d.X - a.X) + ny * (d.Y - a.Y) + nz * (d.Z - a.Z);
            if (!double.IsFinite(plane) || plane != 0 || nz == 0) continue;
            var polygon = new[] { a, c, b, d };
            double sign = 0; bool convex = true;
            for (int i = 0; i < 4; i++)
            {
                var p = polygon[i]; var q = polygon[(i + 1) % 4]; var r = polygon[(i + 2) % 4];
                double turn = (q.X - p.X) * (r.Y - q.Y) - (q.Y - p.Y) * (r.X - q.X);
                if (!double.IsFinite(turn) || turn == 0 || sign != 0 && Math.Sign(turn) != sign) { convex = false; break; }
                sign = Math.Sign(turn);
            }
            if (!convex) continue;
            double lo = 0, hi = 1;
            for (int i = 0; i < 4 && lo <= hi; i++)
            {
                var p = polygon[i]; var q = polygon[(i + 1) % 4];
                double dx = q.X - p.X, dy = q.Y - p.Y, inset = radius * Math.Sqrt(dx * dx + dy * dy);
                double Side(NavigationPoint x) => sign * (dx * (x.Y - p.Y) - dy * (x.X - p.X)) - inset;
                if (!Clip(Side(start), Side(end), ref lo, ref hi)) { convex = false; break; }
            }
            if (!convex) continue;
            foreach (var entry in entries)
                if (RampInterval(start, end, triangles[entry.Index], 0, tolerance, maximumGrade, out double x, out double y) &&
                    Math.Max(lo, x) <= Math.Min(hi, y)) intervals.Add((Math.Max(lo, x), Math.Min(hi, y)));
        }
    }

    // Clips a continuous segment against all three footprint-inset triangle
    // halfplanes and its affine height band. No sample-spacing blind spots.
    // Eroding triangles individually can reject valid shared seams; it cannot
    // invent coverage across them. Polygon union is intentionally not assumed.
    private static bool RampInterval(NavigationPoint start, NavigationPoint end, ConnectorTriangle t,
        double radius, double tolerance, double maximumGrade, out double lo, out double hi)
    {
        lo = 0; hi = 1;
        double ux = t.B.X - t.A.X, uy = t.B.Y - t.A.Y, uz = t.B.Z - t.A.Z;
        double vx = t.C.X - t.A.X, vy = t.C.Y - t.A.Y, vz = t.C.Z - t.A.Z;
        double nz = ux * vy - uy * vx;
        if (!double.IsFinite(nz) || Math.Abs(nz) < 1e-10) return false;
        double sx = (uz * vy - uy * vz) / nz, sy = (ux * vz - uz * vx) / nz;
        double grade = Math.Sqrt(sx * sx + sy * sy);
        if (!double.IsFinite(grade) || grade > maximumGrade) return false;
        double Height(NavigationPoint p) => t.A.Z + sx * (p.X - t.A.X) + sy * (p.Y - t.A.Y);
        double h0 = start.Z - Height(start), h1 = end.Z - Height(end);
        if (!Clip(h0 + tolerance, h1 + tolerance, ref lo, ref hi) ||
            !Clip(tolerance - h0, tolerance - h1, ref lo, ref hi)) return false;
        var points = new[] { t.A, t.B, t.C, t.A };
        double sign = Math.Sign(nz);
        for (int i = 0; i < 3; i++)
        {
            var a = points[i]; var b = points[i + 1];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double inset = radius * Math.Sqrt(dx * dx + dy * dy);
            double Side(NavigationPoint p) => sign * (dx * (p.Y - a.Y) - dy * (p.X - a.X)) - inset;
            if (!Clip(Side(start), Side(end), ref lo, ref hi)) return false;
        }
        return lo <= hi;
    }

    private static bool Clip(double a, double b, ref double lo, ref double hi)
    {
        if (!double.IsFinite(a) || !double.IsFinite(b)) return false;
        double delta = b - a;
        if (delta == 0) return a >= 0;
        if (delta > 0) lo = Math.Max(lo, -a / delta);
        else hi = Math.Min(hi, -a / delta);
        return lo <= hi;
    }

    private static bool Interval(NavigationPoint a, NavigationPoint b, NavigationPoint min, NavigationPoint max, out double lo, out double hi)
    {
        lo = 0; hi = 1;
        var starts = new[] { a.X, a.Y, a.Z }; var ends = new[] { b.X, b.Y, b.Z };
        var mins = new[] { min.X, min.Y, min.Z }; var maxs = new[] { max.X, max.Y, max.Z };
        for (int i = 0; i < 3; i++)
        {
            double delta = ends[i] - starts[i];
            if (delta == 0) { if (starts[i] < mins[i] || starts[i] > maxs[i]) return false; }
            else
            {
                double x = (mins[i] - starts[i]) / delta, y = (maxs[i] - starts[i]) / delta;
                lo = Math.Max(lo, Math.Min(x, y)); hi = Math.Min(hi, Math.Max(x, y));
                if (lo > hi) return false;
            }
        }
        return true;
    }
}
