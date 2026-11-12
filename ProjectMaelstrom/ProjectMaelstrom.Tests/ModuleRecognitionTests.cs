using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class ModuleRecognitionTests
{
    [Fact]
    public void CaptureOnlyCannotPretendToRecognizeAControlOrFact()
    {
        var template = Recognition();
        var capture = template with
        {
            Frame = template.Frame with { TextOrigin = "selected-window-capture" },
            Scene = "Unknown",
            Controls = [],
            VisualFacts = []
        };
        ModuleRecognition.Validate(capture);
        Assert.Throws<InvalidDataException>(() => ModuleRecognition.Validate(capture with { Scene = "MenuObserved" }));
        Assert.Throws<InvalidDataException>(() => ModuleRecognition.Validate(capture with { VisualFacts = ["wizard101.ui"] }));
        Assert.Throws<InvalidDataException>(() => ModuleRecognition.Validate(capture with { Controls = template.Controls }));
    }

    private static readonly RoutineBinding Binding = new("target-1", "frame-1282x760", "menus.visit.one");
    private static readonly Size Geometry = new(1282, 760);
    private static ModuleVisualRecognition Recognition() => new(
        new(new string('a', 64), 100, Geometry, Binding, "saved-ocr"), "MenuObserved",
        [new("game-menu.idle", new(450, 580, 40, 20))], ["wizard101.ui"], "recognized-menu");
    private static ModuleObservation Convert(ModuleVisualRecognition recognition, double now = 110) =>
        ModuleRecognition.AsObservation(recognition, Binding, Geometry, now, true, true, true);

    [Theory]
    [InlineData(100, true)]
    [InlineData(400, true)]
    [InlineData(400.001, false)]
    [InlineData(99.9, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    public void CaptureAgeIsStrictlyBoundedWithoutRetimestampingOrInventingOutcomes(double now, bool fresh)
    {
        var observation = Convert(Recognition(), now);
        Assert.Equal(fresh, observation.Observation.Fresh);
        Assert.Equal(100, observation.Observation.TimeMs);
        Assert.Null(observation.Observation.OutcomeForProposalId);
        Assert.Equal(fresh, observation.Observation.ControlVerified);
        Assert.Equal(fresh ? 1 : 0, observation.VerifiedControls!.Length);
        Assert.Equal(fresh ? 1 : 0, observation.VerifiedFacts!.Length);
    }

    [Fact]
    public void ChangedBindingIsPreservedForTheRunnerToDetectAndCannotCarryControls()
    {
        var original = Recognition();
        var changed = original with { Frame = original.Frame with { Binding = Binding with { ContextId = "other.visit.one" } } };
        var observation = Convert(changed);
        Assert.Equal(changed.Frame.Binding, observation.Observation.Binding);
        Assert.False(observation.Observation.ContextVerified);
        Assert.False(observation.Observation.Fresh);
        Assert.Empty(observation.VerifiedControls!);
        Assert.Empty(observation.VerifiedFacts!);
        var resized = ModuleRecognition.AsObservation(original, Binding, new(1282, 759), 110, true, true, true);
        Assert.False(resized.Observation.Fresh);
        Assert.Empty(resized.VerifiedControls!);
    }

    [Fact]
    public void TargetContextOwnershipCancellationAndTakeoverRemainExternalGates()
    {
        (bool Target, bool Context, bool Owner, bool User, bool Cancel)[] cases =
        {
            (false, true, true, false, false),
            (true, false, true, false, false), (true, true, false, false, false),
            (true, true, true, true, false), (true, true, true, false, true)
        };
        foreach (var gates in cases)
        {
            var result = ModuleRecognition.AsObservation(Recognition(), Binding, Geometry, 110,
                gates.Target, gates.Context, gates.Owner, gates.User, gates.Cancel);
            Assert.False(result.Observation.ControlVerified);
            Assert.Empty(result.VerifiedControls!);
            Assert.Empty(result.VerifiedFacts!);
            Assert.Equal(gates.User, result.Observation.UserActive);
            Assert.Equal(gates.Cancel, result.Observation.Cancelled);
        }
    }

    [Fact]
    public void FactAndControlCollectionsAreCopiedAndDoNotGrantAnOutcome()
    {
        var recognition = Recognition(); var external = new[] { "wizard101.ui", "allowed-context" };
        var result = ModuleRecognition.AsObservation(recognition, Binding, Geometry, 110, true, true, true,
            externalFacts: external);
        recognition.Controls[0] = new("changed", new(1, 1, 20, 20));
        recognition.VisualFacts[0] = "changed"; external[1] = "changed";
        Assert.NotNull(result.VerifiedControls);
        Assert.NotNull(result.VerifiedFacts);
        Assert.Equal(["game-menu.idle"], result.VerifiedControls!);
        Assert.Equal(["wizard101.ui", "allowed-context"], result.VerifiedFacts!);
        Assert.Null(result.Observation.OutcomeForProposalId);
    }

    [Fact]
    public void InvalidIdentityAndProvenanceCannotBecomeModuleEvidence()
    {
        var frame = Recognition().Frame;
        foreach (var invalid in new[]
        {
            frame with { ImageSha256 = "bad" }, frame with { CapturedMs = -1 }, frame with { CapturedMs = double.NaN },
            frame with { Geometry = Size.Empty }, frame with { Geometry = new(9000, 760) },
            frame with { TextOrigin = "guessed-success" }, frame with { Binding = Binding with { TargetId = "" } }
        }) Assert.Throws<InvalidDataException>(() => ModuleRecognition.ValidateFrame(invalid));
    }

    [Fact]
    public void UncertainScenesDuplicateControlsAndInvalidBoundsCannotBeActionable()
    {
        var original = Recognition();
        foreach (var scene in new[] { "Unknown", "Loading", "OutsideCandidate" })
            Assert.Throws<InvalidDataException>(() => Convert(original with { Scene = scene }));
        Assert.Throws<InvalidDataException>(() => Convert(original with { Controls = [original.Controls[0], original.Controls[0]] }));
        foreach (var bounds in new[] { new RectangleF(float.NaN, 1, 10, 10), new RectangleF(1, 1, -1, 10),
            new RectangleF(1200, 700, 100, 100), new RectangleF(-1, 1, 10, 10) })
            Assert.Throws<InvalidDataException>(() => Convert(original with { Controls = [new("control", bounds)] }));
        Assert.Throws<InvalidDataException>(() => Convert(original with { VisualFacts = ["fact", "fact"] }));
        Assert.Throws<InvalidDataException>(() => Convert(original with
        {
            Controls = Enumerable.Range(0, 33)
            .Select(i => new ModuleVisualControl("control" + i, new(1, 1, 10, 10))).ToArray()
        }));
    }
}
