using System.Text.Json;
using Maelstrom.DeckApi;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class JobDeckHostActuationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maelstrom-actuation-test-" + Guid.NewGuid().ToString("N"));

    public JobDeckHostActuationTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }
        catch { }
    }

    private OperationScheduler CreateScheduler() => new(_root, Path.Combine(_root, "locks"));

    private Task<JsonElement> Call(string command, object? arguments = null, string? requestId = null) =>
        JobDeckWire.CallAsync(_root, JsonSerializer.SerializeToUtf8Bytes(
            new JobDeckRequest(command, JsonSerializer.SerializeToElement(arguments ?? new { }, JobDeckRules.Json), requestId), JobDeckRules.Json));

    private static JsonElement Result(JsonElement reply)
    {
        Assert.True(reply.GetProperty("ok").GetBoolean(), reply.TryGetProperty("error", out var err) ? err.GetString() : "Unknown error");
        return reply.GetProperty("result");
    }

    [Fact]
    public async Task Actuate_SimulatedSteering_ReturnsVerifiedDispatchedKeyEvents()
    {
        await using var scheduler = CreateScheduler();
        await using var host = new JobDeckHost(_root, scheduler);

        var args = new
        {
            simulation = true,
            target = "pid-123-start-456",
            steering = new
            {
                forwardPulseMs = 150,
                turnLeftPulseMs = 0,
                turnRightPulseMs = 0,
                reversePulseMs = 0,
                curvature = 0.05,
                angleErrorRad = 0.0,
                isArrived = false
            }
        };

        var reply = await Call("actuate", args);
        var result = Result(reply);

        Assert.True(result.GetProperty("accepted").GetBoolean());
        Assert.True(result.GetProperty("released").GetBoolean());
        Assert.True(result.GetProperty("simulation").GetBoolean());
        Assert.Equal("game.move-forward", result.GetProperty("actionId").GetString());

        var events = result.GetProperty("dispatchedEvents").EnumerateArray().ToArray();
        Assert.Equal(2, events.Length);
        Assert.Equal("key", events[0].GetProperty("type").GetString());
        Assert.Equal("W", events[0].GetProperty("target").GetString());
        Assert.False(events[0].GetProperty("up").GetBoolean());

        Assert.Equal("key", events[1].GetProperty("type").GetString());
        Assert.Equal("W", events[1].GetProperty("target").GetString());
        Assert.True(events[1].GetProperty("up").GetBoolean());
    }

    [Fact]
    public async Task Actuate_SimulatedTurnLeft_ReturnsTurnKeyEvents()
    {
        await using var scheduler = CreateScheduler();
        await using var host = new JobDeckHost(_root, scheduler);

        var args = new
        {
            simulation = true,
            target = "pid-123-start-456",
            steering = new
            {
                forwardPulseMs = 0,
                turnLeftPulseMs = 80,
                turnRightPulseMs = 0,
                reversePulseMs = 0,
                curvature = 0.2,
                angleErrorRad = 0.5,
                isArrived = false
            }
        };

        var reply = await Call("actuate", args);
        var result = Result(reply);

        Assert.True(result.GetProperty("accepted").GetBoolean());
        Assert.True(result.GetProperty("released").GetBoolean());
        Assert.Equal("game.turn-left", result.GetProperty("actionId").GetString());

        var events = result.GetProperty("dispatchedEvents").EnumerateArray().ToArray();
        Assert.Equal(2, events.Length);
        Assert.Equal("A", events[0].GetProperty("target").GetString());
        Assert.False(events[0].GetProperty("up").GetBoolean());
        Assert.Equal("A", events[1].GetProperty("target").GetString());
        Assert.True(events[1].GetProperty("up").GetBoolean());
    }

    [Fact]
    public async Task Actuate_SimulatedKinematicClick_ReturnsVerifiedMouseEvents()
    {
        await using var scheduler = CreateScheduler();
        await using var host = new JobDeckHost(_root, scheduler);

        var args = new
        {
            simulation = true,
            target = "pid-123-start-456",
            kinematic = new
            {
                pointCount = 20,
                totalDurationMs = 250.0,
                dwellMs = 65.0,
                targetX = 640.0,
                targetY = 360.0
            }
        };

        var reply = await Call("actuate", args);
        var result = Result(reply);

        Assert.True(result.GetProperty("accepted").GetBoolean());
        Assert.True(result.GetProperty("released").GetBoolean());
        Assert.True(result.GetProperty("simulation").GetBoolean());
        Assert.Equal("mouse.click", result.GetProperty("actionId").GetString());

        var events = result.GetProperty("dispatchedEvents").EnumerateArray().ToArray();
        Assert.Equal(2, events.Length);
        Assert.Equal("mouse", events[0].GetProperty("type").GetString());
        Assert.Equal("640.0,360.0", events[0].GetProperty("target").GetString());
        Assert.False(events[0].GetProperty("up").GetBoolean());

        Assert.Equal("mouse", events[1].GetProperty("type").GetString());
        Assert.Equal("640.0,360.0", events[1].GetProperty("target").GetString());
        Assert.True(events[1].GetProperty("up").GetBoolean());
    }

    [Fact]
    public async Task Actuate_SimulatedOutOfBoundsClick_RejectedWithZeroEvents()
    {
        await using var scheduler = CreateScheduler();
        await using var host = new JobDeckHost(_root, scheduler);

        var args = new
        {
            simulation = true,
            target = "pid-123-start-456",
            kinematic = new
            {
                pointCount = 20,
                totalDurationMs = 250.0,
                dwellMs = 65.0,
                targetX = 1500.0, // Out of bounds (> 1280)
                targetY = 360.0
            }
        };

        var reply = await Call("actuate", args);
        var result = Result(reply);

        Assert.False(result.GetProperty("accepted").GetBoolean());
        Assert.True(result.GetProperty("released").GetBoolean());
        Assert.Equal("kinematic.rejected", result.GetProperty("proposalId").GetString());

        var events = result.GetProperty("dispatchedEvents").EnumerateArray().ToArray();
        Assert.Empty(events);
    }

    [Fact]
    public async Task Actuate_LiveModeWithoutConnection_RejectsGracefully()
    {
        await using var scheduler = CreateScheduler();
        await using var host = new JobDeckHost(_root, scheduler);

        var args = new
        {
            simulation = false, // live mode!
            target = "pid-123-start-456",
            steering = new
            {
                forwardPulseMs = 150,
                turnLeftPulseMs = 0,
                turnRightPulseMs = 0,
                reversePulseMs = 0,
                curvature = 0.05,
                angleErrorRad = 0.0,
                isArrived = false
            }
        };

        var reply = await Call("actuate", args);
        Assert.False(reply.GetProperty("ok").GetBoolean());
        Assert.Contains("Connect W101 and enable travel/quest actions before live actuation", reply.GetProperty("error").GetString());
    }
}
