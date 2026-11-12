using System.Text.Json;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class PetDanceVisitReplayTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "maelstrom-visit-replay-" + Guid.NewGuid().ToString("N"));
    private const string Session = "offline-visit-test";
    private static readonly string Hash = new('a', 64);
    private static readonly Size Geometry = new(1280, 720);
    private static readonly PetDanceVisitOptions Options = new("offline-window", Geometry, "test-visit", PetDanceArena.WizardCity, Enabled: true);
    private static readonly RectangleF Control = new(780, 600, 60, 20);
    public PetDanceVisitReplayTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    private static PetDanceVisitObservation Frame(double time, PetDanceScene scene) => new(time, Hash, scene,
        Options.TargetId, Geometry, Options.ContextId, ControlBounds: Control,
        Fresh: true, TargetReady: true, ContextVerified: true, InputOwned: true,
        EntryCostApproved: true, WatchStartVerified: true, SpecialistQualified: true, WholeGameResultVerified: true);
    private static PetDanceVisitReplayStep Obs(double time, PetDanceScene scene) => new("observation", time, Observation: Frame(time, scene));
    private static PetDanceVisitReplayStep Receipt(double time, int ordinal, PetDanceLifecycleAction action) =>
        new("action_receipt", time, Dispatch: new(Session + "/pet-dance-visit/" + ordinal, action, true, true));

    private static PetDanceVisitReplayFixture CompleteFixture()
    {
        var entry = new PetDanceEntryScene(PetDanceScene.InstructionsReady, Hash, 1, Geometry, "synthetic-qualified-entry", Control,
            PetDanceArena.WizardCity, [new(PetDanceArena.WizardCity, new(300, 440, 40, 40), true)], true, true);
        return new(1, Session, true, Options,
        [
            new("observation", 1, Observation: Frame(1, PetDanceScene.InstructionsReady) with { Entry = entry }),
            Receipt(2, 1, PetDanceLifecycleAction.Play), Obs(3, PetDanceScene.Loading), Obs(4, PetDanceScene.Watch),
            new("specialist_receipt", 10, Specialist: new(Session + "/pet-dance-visit/2", 10, Options.TargetId, Geometry,
                Options.ContextId, PetDanceArena.WizardCity, Started: true, Completed: true, InputReleased: true)),
            Obs(11, PetDanceScene.GameResult), Receipt(12, 3, PetDanceLifecycleAction.Next),
            Obs(13, PetDanceScene.SnackOffer), Receipt(14, 4, PetDanceLifecycleAction.FinishWithoutSnack),
            Obs(15, PetDanceScene.OutsideAfterGame), Receipt(16, 5, PetDanceLifecycleAction.OpenMenu),
            Obs(17, PetDanceScene.MenuObserved), Receipt(18, 6, PetDanceLifecycleAction.SelectIdle), Obs(19, PetDanceScene.NativeIdle)
        ]);
    }

    private (string Source, string Output) Save(PetDanceVisitReplayFixture fixture)
    {
        var source = Path.Combine(_directory, "input.json");
        File.WriteAllText(source, JsonSerializer.Serialize(fixture, PetDanceVisitReplay.Json));
        return (source, Path.Combine(_directory, "report.json"));
    }

    [Fact]
    public void FullVisitUsesExplicitReceiptsAndProducesZeroInputRehearsal()
    {
        var files = Save(CompleteFixture());
        Assert.True(PetDanceVisitReplay.Run(files.Source, files.Output));
        using var report = JsonDocument.Parse(File.ReadAllText(files.Output));
        Assert.Equal(6, report.RootElement.GetProperty("proposal_count").GetInt32());
        Assert.Equal(6, report.RootElement.GetProperty("supplied_receipt_count").GetInt32());
        Assert.Equal(0, report.RootElement.GetProperty("desktop_dispatch_count").GetInt32());
        Assert.False(report.RootElement.GetProperty("live_qualified").GetBoolean());
        Assert.Throws<IOException>(() => PetDanceVisitReplay.Run(files.Source, files.Output));
    }

    [Fact]
    public void MissingSpecialistCompletionCannotAdvanceToReward()
    {
        var fixture = CompleteFixture();
        var files = Save(fixture with { Events = fixture.Events.Where(e => e.Kind != "specialist_receipt").ToArray() });
        Assert.False(PetDanceVisitReplay.Run(files.Source, files.Output));
        using var report = JsonDocument.Parse(File.ReadAllText(files.Output));
        Assert.True(report.RootElement.GetProperty("stopped").GetBoolean());
        Assert.Equal(2, report.RootElement.GetProperty("proposal_count").GetInt32());
    }

    [Fact]
    public void InitialIdleNeverWakesOrCompletesAndUnknownOcrCannotReuseAClaimedReadyState()
    {
        var fixture = CompleteFixture();
        var files = Save(fixture with { Events = [Obs(0, PetDanceScene.NativeIdle), fixture.Events[0] with { Text = new(Geometry, []) }] });
        Assert.False(PetDanceVisitReplay.Run(files.Source, files.Output));
        using var report = JsonDocument.Parse(File.ReadAllText(files.Output));
        Assert.Equal(0, report.RootElement.GetProperty("proposal_count").GetInt32());
    }

    [Fact]
    public void RecordedFramesRequireExistingContainedHashMatchedEvidence()
    {
        var fixture = CompleteFixture() with { Synthetic = false };
        var files = Save(fixture);
        Assert.Throws<InvalidDataException>(() => PetDanceVisitReplay.Run(files.Source, files.Output));
        File.WriteAllBytes(Path.Combine(_directory, "source.bin"), [1, 2, 3]);
        foreach (var path in new[] { "../outside.bin", "source.bin" })
        {
            var frame = Obs(1, PetDanceScene.NativeIdle) with { ImageFile = path };
            Save(fixture with { Events = [frame] });
            Assert.Throws<InvalidDataException>(() => PetDanceVisitReplay.Run(files.Source, files.Output));
            Assert.False(File.Exists(files.Output));
        }
    }

    [Fact]
    public void ARealCropCannotBePresentedAsAFullFrameWithMatchingHash()
    {
        var crop = Path.Combine(AppContext.BaseDirectory, "TestData", "PetDance", "Entry", "play-disabled.png");
        var bytes = File.ReadAllBytes(crop);
        File.WriteAllBytes(Path.Combine(_directory, "crop.png"), bytes);
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        var step = Obs(1, PetDanceScene.NativeIdle) with
        {
            ImageFile = "crop.png",
            Observation = Frame(1, PetDanceScene.NativeIdle) with { ImageSha256 = digest }
        };
        var files = Save(CompleteFixture() with { Synthetic = false, Events = [step] });
        var error = Assert.Throws<InvalidDataException>(() => PetDanceVisitReplay.Run(files.Source, files.Output));
        Assert.Contains("geometry", error.Message);
        Assert.False(File.Exists(files.Output));
    }

    [Fact]
    public void HashMatchedNonPngAndUnboundedHeaderAreRejected()
    {
        foreach (var nonPng in new[] { true, false })
        {
            var bytes = nonPng ? new byte[] { 1, 2, 3 } : File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,
                "TestData", "PetDance", "Entry", "play-disabled.png"));
            if (!nonPng) System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), 1000000);
            File.WriteAllBytes(Path.Combine(_directory, "source.png"), bytes);
            var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            var step = Obs(1, PetDanceScene.NativeIdle) with
            {
                ImageFile = "source.png",
                Observation = Frame(1, PetDanceScene.NativeIdle) with { ImageSha256 = digest }
            };
            var files = Save(CompleteFixture() with { Synthetic = false, Events = [step] });
            Assert.Throws<InvalidDataException>(() => PetDanceVisitReplay.Run(files.Source, files.Output));
            Assert.False(File.Exists(files.Output));
        }
    }

    [Fact]
    public void AmbiguousPayloadAndOutOfOrderTimeFailWithoutPublishing()
    {
        var fixture = CompleteFixture();
        var files = Save(fixture with { Events = [fixture.Events[0] with { Dispatch = new("bad", PetDanceLifecycleAction.Play, true, true) }] });
        Assert.Throws<InvalidDataException>(() => PetDanceVisitReplay.Run(files.Source, files.Output));
        Save(fixture with { Events = [Obs(2, PetDanceScene.Unknown), Obs(1, PetDanceScene.Unknown)] });
        Assert.Throws<InvalidDataException>(() => PetDanceVisitReplay.Run(files.Source, files.Output));
        Assert.False(File.Exists(files.Output));
    }
}
