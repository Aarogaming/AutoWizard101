using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class NavigationConnectorGeometryTests
{
    private static bool Seam(double gap = 0, double raised = 0) => NavigationConnectorGeometry.Clear(
        new(2, 5, 1), new(8, 5, 4), 1, 10, .01, true, [], [],
        [new(new(0, 0, 0), new(10, 0, 5), new(10, 10, 5)),
         new(new(0, gap, raised), new(10, 10 + gap, 5 + raised), new(0, 10, raised))], .6);
    [Fact] public void CoplanarSharedRampSeamPasses() => Assert.True(Seam());
    [Fact] public void SmallCrackIsNotWelded() => Assert.False(Seam(gap: .001));
    [Fact] public void OverlappingDifferentFloorDoesNotMerge() => Assert.False(Seam(raised: 1));
    private static bool Ramp(double grade = .6, double endZ = 10, ConnectorBox[]? boxes = null) =>
        NavigationConnectorGeometry.Clear(new(0, 0, 0), new(20, 0, endZ), 1, 10, .01, true,
            boxes ?? [], [], [new(new(-20, -20, -10), new(80, -20, 40), new(-20, 80, -10))], grade);
    [Fact] public void ContinuousRampPasses() => Assert.True(Ramp());
    [Fact] public void SteepRampRejected() => Assert.False(Ramp(.4));
    [Fact] public void WrongRampLayerRejected() => Assert.False(Ramp(endZ: 0));
    [Fact] public void ThinObstacleOnRampRejected() => Assert.False(Ramp(boxes: [new(new(10, -5, 4), new(10.001, 5, 30))]));
    private static bool Check(bool complete = true, ConnectorBox[]? boxes = null, ConnectorFloor[]? floors = null) =>
        NavigationConnectorGeometry.Clear(new(0, 0, 0), new(100, 0, 0), 1, 10, .1, complete,
            boxes ?? [], floors ?? [new(-2, -2, 102, 2, 0)]);

    [Fact] public void ContinuousFlatSupportPasses() => Assert.True(Check());
    [Fact] public void ThinWallBetweenSamplesBlocks() => Assert.False(Check(boxes: [new(new(50, -2, 0), new(50.01, 2, 20))]));
    [Fact] public void MissingCoverageBlocks() => Assert.False(Check(complete: false));
    [Fact] public void WrongFloorBlocks() => Assert.False(Check(floors: [new(-2, -2, 102, 2, 25)]));
    [Fact] public void SmallFloorGapBlocks() => Assert.False(Check(floors: [new(-2, -2, 49, 2, 0), new(51, -2, 102, 2, 0)]));
    [Fact] public void InvalidGeometryBlocks() => Assert.False(Check(boxes: [new(new(double.NaN, 0, 0), new(1, 1, 1))]));
}
