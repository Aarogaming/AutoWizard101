using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class NavigationFloorSheetTests
{
    private static ConnectorTriangle[] Road() =>
    [new(new(-5, 0, 0), new(5, 0, 0), new(5, 10, 0)),
     new(new(-5, 0, 0), new(5, 10, 0), new(-5, 10, 0)),
     new(new(-5, 10, 0), new(5, 10, 0), new(5, 20, 2)),
     new(new(-5, 10, 0), new(5, 20, 2), new(-5, 20, 2))];
    private static IReadOnlyList<NavigationPoint>? Run(ConnectorTriangle[] faces, double crease = .3) =>
        NavigationFloorSheet.Trace(faces, new(0, 5, 0), new(0, 15, 1), 1.5, .5, crease, .001);
    [Fact]
    public void FlatToRampUsesPiecewiseHeight()
    {
        var path = Run(Road()); Assert.NotNull(path);
        Assert.Contains(path, p => p.Y == 10 && p.Z == 0);
        Assert.Equal(1, path[^1].Z, 10);
    }
    [Fact] public void MissingTriangleExposesBoundary() => Assert.Null(Run(Road()[..3]));
    [Fact] public void ExcessiveCreaseRejected() => Assert.Null(Run(Road(), .01));
    [Fact] public void NonmanifoldRejected() => Assert.Null(Run([.. Road(), Road()[0]]));
    [Fact] public void WrongEndLayerRejected() => Assert.Null(NavigationFloorSheet.Trace(Road(), new(0, 5, 0), new(0, 15, 4), 1, .5, .3, .01));
    [Fact]
    public void ShallowNeighborRejectsFootprint()
    {
        ConnectorTriangle[] faces = [new(new(0, 0, 0), new(10, 0, 0), new(5, 10, 0)), new(new(0, 0, 0), new(5, -1, 0), new(10, 0, 0))];
        Assert.Null(NavigationFloorSheet.Trace(faces, new(4, .5, 0), new(6, .5, 0), 2, .5, .3, .01));
        Assert.False(NavigationConnectorGeometry.Clear(new(4, .5, 0), new(6, .5, 0), 2, 4, .01, true, [], [], faces, .5));
    }
}
