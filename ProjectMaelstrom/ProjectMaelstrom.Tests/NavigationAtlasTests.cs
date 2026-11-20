using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class NavigationAtlasTests
{
    [Fact]
    public void DynamicSelectionRequiresExactPoseGeometryAndExistingAtlasEvidence()
    {
        var map = Map(); var id = Identity(map); var atlas = new NavigationAtlas([Destination(id)]);
        var pose = new NavigationPoint(-20, 0, 0);
        var start = Connector(id, 0) with { Pose = pose };
        NavigationRoute? Run(bool complete, NavigationConnector candidate) => atlas.PlanDynamic("dance", id, map,
            pose, [candidate], Connector(id, 1), [Corridor(id)], 20, .1, complete, [], [new(-40, -20, 120, 20, 0)], 5);
        Assert.NotNull(Run(true, start));
        Assert.Null(Run(false, start));
        Assert.Null(Run(true, start with { Pose = new(-21, 0, 0) }));
        Assert.Null(Run(true, start with { Verified = false }));
    }

    private static NavigationMap Map()
    {
        using var s = new MemoryStream(); using var w = new BinaryWriter(s);
        w.Write((short)1); w.Write((short)2); w.Write((short)0);
        for (short i = 0; i < 2; i++) { w.Write(i * 100f); w.Write(0f); w.Write(0f); w.Write(i); }
        w.Write(1); w.Write((short)0); w.Write((short)1);
        return NavigationMap.Parse(s.ToArray());
    }
    private static NavigationIdentity Identity(NavigationMap map) => new("zone", "instance", "modern", map.Sha256, "pose1", "input1", "unmounted1");
    private static NavigationDestination Destination(NavigationIdentity id) => new("dance", "Pet Dance", ["Dance Game"], id, 1, "Dance Game", true);
    private static NavigationConnector Connector(NavigationIdentity id, int vertex) => new(new(vertex * 100, 0, 0), vertex, id, 10, ["connector-proof"], true);
    private static NavigationCorridor Corridor(NavigationIdentity id) => new(new(0, 1), id, NavigationEvidenceGrade.Repeatable, 10, ["attempt1", "attempt2"]);

    [Fact]
    public void OpenPortalNeedsVerifiedDestinationInsteadOfInventedPrompt()
    {
        var map = Map(); var id = Identity(map);
        var portal = Destination(id) with
        {
            RequiredPrompt = null,
            PromptVerified = false,
            ArrivalKind = NavigationArrivalKind.ZoneTransition,
            DestinationZone = "pavilion",
            ArrivalVerified = true
        };
        NavigationRoute Plan(NavigationDestination d) => new NavigationAtlas([d]).Plan("dance", id, map,
            Connector(id, 0), Connector(id, 1), [Corridor(id)]);
        Assert.True(Plan(portal).Ready);
        Assert.Equal(NavigationArrivalKind.ZoneTransition, Plan(portal).ArrivalKind);
        Assert.Equal("pavilion", Plan(portal).DestinationZone);
        Assert.False(Plan(portal with { ArrivalVerified = false }).Ready);
        Assert.False(Plan(portal with { DestinationZone = null }).Ready);
    }

    [Fact]
    public void AliasResolvesAndQualifiedEvidencePlansWithoutAuthorizingExecution()
    {
        var map = Map(); var id = Identity(map); var atlas = new NavigationAtlas([Destination(id)]);
        var route = atlas.Plan("dance game", id, map, Connector(id, 0), Connector(id, 1), [Corridor(id)]);
        Assert.True(route.Ready); Assert.Equal("qualified-evidence-plan-not-execution", route.Reason);
        Assert.Equal(2, route.Points.Count); Assert.Equal("Dance Game", route.RequiredPrompt);
    }

    [Fact]
    public void UnknownClearanceAndCandidateEdgesDoNotBecomeTraversable()
    {
        var map = Map(); var id = Identity(map); var atlas = new NavigationAtlas([Destination(id)]);
        foreach (var c in new[] { Corridor(id) with { Clearance = null }, Corridor(id) with { Grade = NavigationEvidenceGrade.Candidate }, Corridor(id) with { Blocked = true }, Corridor(id) with { Attempts = [] } })
            Assert.Equal("no-qualified-directed-path", atlas.Plan("dance", id, map, Connector(id, 0), Connector(id, 1), [c]).Reason);
    }

    [Fact]
    public void NearbyPoseCannotBindWithoutConnectorEvidence()
    {
        var map = Map(); var id = Identity(map); var atlas = new NavigationAtlas([Destination(id)]);
        Assert.Equal("connector-unqualified", atlas.Plan("dance", id, map, Connector(id, 0) with { Verified = false }, Connector(id, 1), [Corridor(id)]).Reason);
    }

    [Fact]
    public void NearTentIsNotPromptArrival()
    {
        var map = Map(); var id = Identity(map); var atlas = new NavigationAtlas([Destination(id) with { PromptVerified = false }]);
        Assert.Equal("destination-prompt-unqualified", atlas.Plan("dance", id, map, Connector(id, 0), Connector(id, 1), [Corridor(id)]).Reason);
    }

    [Fact]
    public void DifferentMovementProfileAndConflictingEvidenceCannotReuseQualification()
    {
        var map = Map(); var id = Identity(map); var atlas = new NavigationAtlas([Destination(id)]);
        var c = Corridor(id);
        foreach (var evidence in new[] { new[] { c with { Identity = id with { MovementProfile = "mounted" } } }, new[] { c, c with { Blocked = true } } })
            Assert.False(atlas.Plan("dance", id, map, Connector(id, 0), Connector(id, 1), evidence).Ready);
    }

    [Fact]
    public void MapHashMismatchRejectsOtherwiseValidEvidence()
    {
        var map = Map(); var id = Identity(map) with { NavSha256 = "old" }; var atlas = new NavigationAtlas([Destination(id)]);
        Assert.Equal("map-version-mismatch", atlas.Plan("dance", id, map, Connector(id, 0), Connector(id, 1), [Corridor(id)]).Reason);
    }

    [Fact]
    public void DuplicateAliasesAreAmbiguousAndInstancesRemainSeparate()
    {
        var map = Map(); var id = Identity(map); var d = Destination(id);
        var atlas = new NavigationAtlas([d, d with { Id = "other" }]);
        Assert.Null(atlas.Resolve("Pet Dance", id));
        Assert.Null(atlas.Resolve("dance", id with { Instance = "another-house" }));
    }
}
