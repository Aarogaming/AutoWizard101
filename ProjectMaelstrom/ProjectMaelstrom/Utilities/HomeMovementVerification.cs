using System.Diagnostics;

namespace ProjectMaelstrom.Utilities;

/// <summary>Bounded hook lifecycle check. No shown windows, captures, focus changes or injected inputs.</summary>
internal static class HomeMovementVerification
{
    public static void Run(string output)
    {
        using var context = new ApplicationContext();
        using var activity = DesktopActivityMonitor.Install(0x4D41454C);
        using var timer = new System.Windows.Forms.Timer { Interval = 100 };
        var clock = Stopwatch.StartNew();
        var installed = activity.Installed;
        var ticks = 0;
        timer.Tick += (_, _) => { ticks++; if (clock.ElapsedMilliseconds >= 1000) context.ExitThread(); };
        timer.Start(); Application.Run(context); timer.Stop(); activity.Dispose();
        if (!installed || activity.Installed || ticks == 0) throw new InvalidOperationException("Activity hook lifecycle check failed.");
        GameExperiments.WriteAtomic(output, new
        {
            installed,
            released = !activity.Installed,
            message_loop_ticks = ticks,
            elapsed_ms = clock.ElapsedMilliseconds,
            activity_events = activity.Revision,
            injected_inputs = 0,
            captured_frames = 0,
            shown_windows = 0,
            note = "Hook lifecycle only; does not validate live W101 movement or idle timeout."
        });
    }
}
