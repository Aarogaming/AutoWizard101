using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class W101PathSessionTests
{
    [Theory]
    [InlineData(12, 99, 1, true)]
    [InlineData(13, 99, 1, false)]
    [InlineData(12, 100, 1, false)]
    [InlineData(12, 99, 2, false)]
    public void BoundStreamRejectsLifetimeChangesAndMissingSequence(int processId, long started, long sequence, bool accepted)
    {
        var target = new WizwalkerTarget(44, 12, 99, "test", "test", "hash");
        var id = new string('a', 32);
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            kind = "moving-pose-candidate",
            zone = "zone",
            observedUtcTicks = 10000000,
            sampleAgeMs = 5,
            streamId = id,
            sequence,
            binding = new { runId = "run", target = new { handle = 44, processId, startedUtcTicks = started } },
            sample = new { position = new { x = 1, y = 2, z = 3 }, yawRadians = 0 }
        });
        PathObservation Read(string stream) => W101PathSession.DecodeBoundStreamCandidate(json, "run", stream, 0, target, "zone", "epoch", 10100000, 100, 50, 2);
        if (accepted) Assert.Equal(83, Read(id).TimeMs);
        else Assert.Throws<InvalidDataException>(() => Read(id));
        Assert.Throws<InvalidDataException>(() => Read(new string('b', 32)));
    }

    [Fact]
    public void StreamMapsReceiptEnvelopeWithoutComparingSourceClockOrigins()
    {
        const string line = """
        {"kind":"moving-pose-candidate","zone":"zone","observedUtcTicks":10000000,"sampleAgeMs":5,
        "sample":{"observed":987654.3,"position":{"x":1,"y":2,"z":3},"yawRadians":0}}
        """;
        var sample = W101PathSession.DecodeStreamCandidate(line, "zone", "epoch", 10100000, 100, 50, 2);
        Assert.Equal(83, sample.TimeMs);
        Assert.Throws<InvalidDataException>(() => W101PathSession.DecodeStreamCandidate(line, "wrong", "epoch", 10100000, 100, 50, 2));
        Assert.Throws<InvalidDataException>(() => W101PathSession.DecodeStreamCandidate(line, "zone", "epoch", 9999999, 100, 50, 2));
        Assert.Throws<InvalidDataException>(() => W101PathSession.DecodeStreamCandidate(line, "zone", "epoch", 11000000, 200, 50, 2));
    }

    [Fact]
    public void RecordedForwardDisplacementSupportsHeadingAndOppositeTurnBinding()
    {
        // Live operation d9f249d8c80042dd88761feb3304f0cc. One orientation,
        // not a complete movement profile or a calibrated collision envelope.
        var observed = Math.Atan2(-36.790130615234375, 48.9073486328125);
        Assert.InRange(Math.Abs(observed - W101PathSession.CanonicalHeading(5.35732364654541)), 0, 0.001);
        Assert.Equal("D", W101PathSession.TurnKey(-1));
        Assert.Equal("A", W101PathSession.TurnKey(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => W101PathSession.TurnKey(0));
        Assert.Throws<InvalidDataException>(() => W101PathSession.CanonicalHeading(double.NaN));
    }

    [Theory]
    [InlineData(0, -1.5707963267948966)]
    [InlineData(1.5707963267948966, -3.141592653589793)]
    public void PythonWindowUsesConservativeHostTimeAndExplicitHeadingTransform(double yaw, double expected)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = "candidate",
            layoutId = "archived-wizwalker-actor-body",
            samples = new[] {
            new { observed = 900000.0, position = new { x = 1, y = 2, z = 3 }, yawRadians = yaw },
            new { observed = 900000.05, position = new { x = 2, y = 3, z = 3 }, yawRadians = yaw } }
        });
        var sample = W101PathSession.DecodeCandidate(json, "zone", "epoch", 100, 120, 50);
        Assert.Equal(100, sample.TimeMs); Assert.Equal(expected, sample.Heading, 8);
        Assert.Equal(2, sample.X);
        Assert.Throws<InvalidDataException>(() => W101PathSession.DecodeCandidate(json, "zone", "epoch", 100, 200, 50));
    }

    [Theory]
    [InlineData(true, true, true, "Completed")]
    [InlineData(false, true, true, "Failed")]
    [InlineData(true, false, true, "Failed")]
    [InlineData(true, true, false, "CleanupFailed")]
    public async Task SchedulerKeepsQualificationPromptAndReleaseDistinct(bool qualified, bool prompt, bool release, string expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "nav-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            double now = 0; int read = 0, applied = 0, prompts = 0;
            var identity = new NavigationIdentity("zone", "instance", "modern", "hash", "pose", "input", "profile");
            var route = new NavigationRoute(true, "fixture", "dance", identity, [new(0, 0, 0, 20), new(100, 0, 0, 20)], "Dance Game");
            var runtime = new W101PathRuntime(() => true, () => now,
                _ =>
                {
                    now += 50;
                    var x = read++ == 0 ? 0 : 100;
                    // Actual Python decoder wire shape; independent source timebase.
                    var wire = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "candidate",
                        layoutId = "archived-wizwalker-actor-body",
                        samples = new[] {
                        new { observed = 5000.0, position = new { x, y = 0, z = 0 }, yawRadians = 3 * Math.PI / 2 },
                        new { observed = 5000.05, position = new { x, y = 0, z = 0 }, yawRadians = 3 * Math.PI / 2 } }
                    });
                    return Task.FromResult(W101PathSession.DecodeCandidate(wire, "zone", "epoch", now - 10, now, 50));
                },
                (motion, _) => { applied++; now += 10; return Task.FromResult(new PathMotionReceipt(motion.SequenceId, true, now, motion.IssuedMs, null, now)); },
                _ => Task.FromResult(release),
                (title, _) => { prompts++; Assert.Equal("Dance Game", title); return Task.FromResult(prompt); }, qualified);
            await using var scheduler = new OperationScheduler(Path.Combine(root, "runs"), Path.Combine(root, "locks"));
            var request = new OperationRequest(Guid.NewGuid().ToString("N"), "navigation-test", "1", 10, true, "pid-123-start-456", new Dictionary<string, string>());
            var result = await scheduler.Submit(request, (context, token) => W101PathSession.RunOwnedAsync(context, route, new("zone", "epoch"), runtime, token)).Completion;
            Assert.Equal(expected, result.State.ToString());
            Assert.Equal(qualified ? 1 : 0, applied);
            Assert.Equal(qualified && release ? 1 : 0, prompts);
        }
        finally { Directory.Delete(root, true); }
    }
}
