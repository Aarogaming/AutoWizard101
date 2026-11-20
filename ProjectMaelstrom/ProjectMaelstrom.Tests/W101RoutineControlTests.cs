using ProjectMaelstrom.Utilities;
namespace ProjectMaelstrom.Tests;

public class W101RoutineControlTests
{
    [Fact]
    public void PauseResumesButStopCannotBeCleared()
    {
        var signals = new W101RoutineControl();
        Assert.False(signals.Paused); Assert.False(signals.Stopped);
        signals.Pause(); Assert.True(signals.Paused);
        signals.Resume(); Assert.False(signals.Paused);
        signals.Stop(); signals.Resume(); signals.Pause();
        Assert.True(signals.Stopped); Assert.False(signals.Paused);
    }
}
