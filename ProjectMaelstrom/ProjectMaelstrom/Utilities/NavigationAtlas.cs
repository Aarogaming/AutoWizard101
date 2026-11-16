namespace ProjectMaelstrom.Utilities;

/// <summary>Evidence scope is independent of process lifetime. A new process must
/// requalify localization; reusable route evidence stays tied to assets/adapters/profile.</summary>
internal sealed record NavigationIdentity(string Zone, string Instance, string Variant,
    string NavSha256, string PoseAdapter, string InputAdapter, string MovementProfile);
internal enum NavigationEvidenceGrade { Unknown, Candidate, Repeatable }
internal sealed record NavigationCorridor(NavigationEdge Edge, NavigationIdentity Identity,
    NavigationEvidenceGrade Grade, double? Clearance, IReadOnlyList<string> Attempts,
    bool Blocked = false);
internal sealed record NavigationDestination(string Id, string Name, IReadOnlyList<string> Aliases,
    NavigationIdentity Identity, int? Vertex, string? RequiredPrompt,
    bool PromptVerified = false, NavigationArrivalKind ArrivalKind = NavigationArrivalKind.InteractionPrompt,
    string? DestinationZone = null, bool ArrivalVerified = false);
internal sealed record NavigationConnector(NavigationPoint Pose, int Vertex,
    NavigationIdentity Identity, double? Clearance, IReadOnlyList<string> Attempts,
    bool Verified = false);
internal sealed record NavigationRoute(bool Ready, string Reason, string DestinationId,
    NavigationIdentity Identity, IReadOnlyList<PathPoint> Points, string? RequiredPrompt,
    NavigationArrivalKind ArrivalKind = NavigationArrivalKind.InteractionPrompt, string? DestinationZone = null);

/// <summary>Named local destinations and strict evidence-filtered planning. This
/// deliberately cannot turn nearby vertices, successful centerlines or archive
/// topology into certified clearance. Portal orchestration remains separate.</summary>
internal sealed class NavigationAtlas
{
    private readonly NavigationDestination[] _destinations;
    internal NavigationAtlas(IEnumerable<NavigationDestination> destinations)
    {
        _destinations = destinations.Select(d => d with { Aliases = d.Aliases.ToArray() }).ToArray();
        if (_destinations.Length > 4096 || _destinations.Any(d => string.IsNullOrWhiteSpace(d.Id) || string.IsNullOrWhiteSpace(d.Name)) ||
            _destinations.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count() != _destinations.Length)
            throw new InvalidDataException("Invalid destination catalogue.");
    }

    internal NavigationDestination? Resolve(string name, NavigationIdentity identity)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var matches = _destinations.Where(d => d.Identity == identity &&
            (string.Equals(d.Id, name.Trim(), StringComparison.OrdinalIgnoreCase) ||
             string.Equals(d.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) ||
             d.Aliases.Any(a => string.Equals(a, name.Trim(), StringComparison.OrdinalIgnoreCase)))).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] with { Aliases = matches[0].Aliases.ToArray() } : null;
    }

    internal NavigationRoute Plan(string name, NavigationIdentity identity, NavigationMap map,
        NavigationConnector start, NavigationConnector end, IReadOnlyList<NavigationCorridor> corridors)
    {
        var destination = Resolve(name, identity);
        NavigationRoute Reject(string reason) => new(false, reason, destination?.Id ?? name, identity, [], destination?.RequiredPrompt);
        if (destination is null) return Reject("destination-unresolved-or-ambiguous");
        if (!string.Equals(identity.NavSha256, map.Sha256, StringComparison.Ordinal)) return Reject("map-version-mismatch");
        if (destination.Vertex is not { } goal || goal < 0 || goal >= map.Points.Count) return Reject("destination-anchor-unqualified");
        if (!Enum.IsDefined(destination.ArrivalKind)) return Reject("destination-arrival-kind-invalid");
        if (destination.ArrivalKind == NavigationArrivalKind.InteractionPrompt &&
            (!destination.PromptVerified || string.IsNullOrWhiteSpace(destination.RequiredPrompt))) return Reject("destination-prompt-unqualified");
        if (destination.ArrivalKind != NavigationArrivalKind.ZoneTransition && destination.DestinationZone is not null &&
            destination.DestinationZone != identity.Zone) return Reject("local-arrival-zone-mismatch");
        if (destination.ArrivalKind != NavigationArrivalKind.InteractionPrompt && !destination.ArrivalVerified)
            return Reject("destination-arrival-unqualified");
        if (destination.ArrivalKind == NavigationArrivalKind.ZoneTransition && string.IsNullOrWhiteSpace(destination.DestinationZone))
            return Reject("destination-zone-unqualified");
        bool Connector(NavigationConnector c) => c.Identity == identity && c.Verified &&
            c.Vertex >= 0 && c.Vertex < map.Points.Count && Positive(c.Clearance) && Evidence(c.Attempts) &&
            new[] { c.Pose.X, c.Pose.Y, c.Pose.Z }.All(double.IsFinite);
        if (!Connector(start) || !Connector(end) || end.Vertex != goal) return Reject("connector-unqualified");
        var qualified = new Dictionary<NavigationEdge, NavigationCorridor>();
        foreach (var group in corridors.Where(c => c.Identity == identity).GroupBy(c => c.Edge))
        {
            // Conflicting/duplicate evidence must be reconciled, never pick the most permissive row.
            var entries = group.ToArray();
            if (entries.Length == 1 && entries[0] is { Blocked: false, Grade: NavigationEvidenceGrade.Repeatable } c &&
                Positive(c.Clearance) && Evidence(c.Attempts)) qualified.Add(group.Key, c);
        }
        var blocked = map.Edges.Where(e => !qualified.ContainsKey(e)).ToHashSet();
        var path = map.FindPath(start.Vertex, goal, blocked);
        if (!path.Found) return Reject("no-qualified-directed-path");
        var points = new List<PathPoint> { new(start.Pose.X, start.Pose.Y, start.Pose.Z, start.Clearance!.Value) };
        for (var i = 0; i < path.Vertices.Count; i++)
        {
            var p = map.Points[path.Vertices[i]];
            var clearance = Math.Min(start.Clearance!.Value, end.Clearance!.Value);
            if (i > 0) clearance = Math.Min(clearance, qualified[new(path.Vertices[i - 1], path.Vertices[i])].Clearance!.Value);
            if (i + 1 < path.Vertices.Count) clearance = Math.Min(clearance, qualified[new(path.Vertices[i], path.Vertices[i + 1])].Clearance!.Value);
            Add(p, clearance);
        }
        Add(end.Pose, end.Clearance!.Value);
        if (points.Count < 2) return Reject("already-at-anchor-requires-prompt-observation");
        return new(true, "qualified-evidence-plan-not-execution", destination.Id, identity, points.AsReadOnly(), destination.RequiredPrompt, destination.ArrivalKind, destination.DestinationZone);

        void Add(NavigationPoint p, double clearance)
        {
            var last = points[^1];
            if (Math.Abs(last.X - p.X) + Math.Abs(last.Y - p.Y) < 0.001)
            {
                if (Math.Abs(last.Z - p.Z) > 0.001) throw new InvalidDataException("Vertical-only connector requires another route.");
                points[^1] = last with { Clearance = Math.Min(last.Clearance, clearance) };
            }
            else points.Add(new(p.X, p.Y, p.Z, clearance));
        }
    }

    /// <summary>Selects the shortest off-graph connector from exact-pose evidence.
    /// The caller must supply current settled localization and reviewed geometry;
    /// this pure planner does not acquire a lease or validate a live target.</summary>
    internal NavigationRoute? PlanDynamic(string name, NavigationIdentity identity, NavigationMap map,
        NavigationPoint settledPose, IReadOnlyList<NavigationConnector> starts, NavigationConnector end,
        IReadOnlyList<NavigationCorridor> corridors, double height, double floorTolerance,
        bool geometryComplete, IReadOnlyList<ConnectorBox> obstacles, IReadOnlyList<ConnectorFloor> floors,
        double footprintRadius, IReadOnlyList<ConnectorTriangle>? ramps = null, double maximumGrade = 0,
        double maximumCrease = 0)
    {
        if (!double.IsFinite(footprintRadius) || footprintRadius <= 0) return null;
        foreach (var start in starts.Where(s => s.Pose == settledPose && s.Identity == identity &&
                     s.Vertex >= 0 && s.Vertex < map.Points.Count && s.Clearance >= footprintRadius)
                 .OrderBy(s => Math.Sqrt(Math.Pow(map.Points[s.Vertex].X - settledPose.X, 2) + Math.Pow(map.Points[s.Vertex].Y - settledPose.Y, 2))))
        {
            if (geometryComplete && ramps is { Count: > 0 } && maximumCrease > 0)
            {
                var sheet = NavigationFloorSheet.Trace(ramps, settledPose, map.Points[start.Vertex], footprintRadius,
                    maximumGrade, maximumCrease, floorTolerance);
                if (sheet is not null && Enumerable.Range(1, sheet.Count - 1).All(i =>
                    NavigationConnectorGeometry.ObstaclesClear(sheet[i - 1], sheet[i], footprintRadius, height, obstacles)))
                {
                    var planned = Plan(name, identity, map, start, end, corridors);
                    var vertex = map.Points[start.Vertex];
                    if (planned.Ready && planned.Points.Count > 1 && planned.Points.All(p => p.Clearance >= footprintRadius) &&
                        planned.Points[1].X == vertex.X && planned.Points[1].Y == vertex.Y && planned.Points[1].Z == vertex.Z)
                        return planned with { Points = sheet.Select(p => new PathPoint(p.X, p.Y, p.Z, start.Clearance!.Value)).Concat(planned.Points.Skip(2)).ToArray() };
                }
            }
            if (!NavigationConnectorGeometry.Clear(settledPose, map.Points[start.Vertex], footprintRadius,
                    height, floorTolerance, geometryComplete, obstacles, floors, ramps, maximumGrade)) continue;
            var route = Plan(name, identity, map, start, end, corridors);
            if (route.Ready && route.Points.All(p => p.Clearance >= footprintRadius)) return route;
        }
        return null;
    }

    /// <summary>Settled-reader entry boundary. Owner supplies the currently fenced
    /// context and host timing; stale or cross-epoch pairs never reach planning.</summary>
    internal NavigationRoute? PlanFromObservations(string name, NavigationIdentity identity, NavigationMap map,
        PathObservation before, PathObservation after, string context, double nowMs, double releasedMs,
        double maximumAgeMs, double minimumSpanMs, double maximumSpanMs, double positionNoise, double headingNoise,
        IReadOnlyList<NavigationConnector> starts, NavigationConnector end, IReadOnlyList<NavigationCorridor> corridors,
        double height, double floorTolerance, bool geometryComplete, IReadOnlyList<ConnectorBox> obstacles,
        IReadOnlyList<ConnectorFloor> floors, double footprintRadius,
        IReadOnlyList<ConnectorTriangle>? ramps = null, double maximumGrade = 0, double maximumCrease = 0)
    {
        var pose = NavigationSettledPose.Validate(before, after, identity.Zone, context, nowMs, releasedMs,
            maximumAgeMs, minimumSpanMs, maximumSpanMs, positionNoise, headingNoise);
        return pose is { } settled ? PlanDynamic(name, identity, map, settled, starts, end, corridors,
            height, floorTolerance, geometryComplete, obstacles, floors, footprintRadius, ramps, maximumGrade, maximumCrease) : null;
    }

    private static bool Positive(double? value) => value is { } v && double.IsFinite(v) && v > 0;
    private static bool Evidence(IReadOnlyList<string> attempts) => attempts.Count > 0 && attempts.All(a => !string.IsNullOrWhiteSpace(a));
}
