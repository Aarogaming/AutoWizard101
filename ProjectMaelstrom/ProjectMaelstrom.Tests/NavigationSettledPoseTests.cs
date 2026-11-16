using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class NavigationSettledPoseTests
{
    private static PathObservation Sample(double time) => new(1, 2, 3, 0, time, "zone", "epoch1");
    private static NavigationPoint? Check(PathObservation a, PathObservation b) => NavigationSettledPose.Validate(a, b, "zone", "epoch1", 150, 90, 100, 10, 40, .1, .01);
    [Fact] public void SettledPairPasses() => Assert.NotNull(Check(Sample(100), Sample(120)));
    [Fact] public void DuplicateTimeFails() => Assert.Null(Check(Sample(100), Sample(100)));
    [Fact] public void ChangedEpochFails() => Assert.Null(Check(Sample(100), Sample(120) with { Context = "epoch2" }));
    [Fact] public void MovementFails() => Assert.Null(Check(Sample(100), Sample(120) with { Z = 4 }));
    [Fact] public void PreReleaseFails() => Assert.Null(Check(Sample(90), Sample(120)));
    [Fact] public void FutureFails() => Assert.Null(Check(Sample(130), Sample(160)));
    [Fact] public void FocusLossFails() => Assert.Null(Check(Sample(100), Sample(120) with { Focused = false }));
}
