using System.Diagnostics;
using System.Media;
using System.Runtime.InteropServices;

namespace ProjectMaelstrom.Utilities;

/// <summary>Nonactivating, click-through notice. Never grants input ownership.
/// Completion requires uninterrupted readiness, cancellation and target checks.</summary>
internal sealed class ActionStartNotice : Form
{
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var value = base.CreateParams; value.ExStyle |= 0x08000000 | 0x20 | 0x80; return value; }
    }

    internal static int Remaining(double seconds) => Math.Max(0, (int)Math.Ceiling(5 - seconds));
    internal static Rectangle BannerBounds(Rectangle client) => new(client.Left + 16, client.Top + 16,
        Math.Max(1, client.Width - 32), Math.Clamp(client.Height / 6, 80, 160));

    internal static Task ShowAsync(GameWindow target, Func<bool> ready, bool sound, CancellationToken token, string? evidenceDirectory = null)
    {
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var window = new ActionStartNotice
                {
                    FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, TopMost = true,
                    StartPosition = FormStartPosition.Manual, Size = new Size(440, 100),
                    BackColor = Color.FromArgb(24, 27, 36), ForeColor = Color.White
                };
                var label = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 16, FontStyle.Bold) };
                window.Controls.Add(label);
                using var timer = new System.Windows.Forms.Timer { Interval = 50 };
                var elapsed = new Stopwatch();
                Exception? failure = null;
                bool completed = false;
                bool recorded = false;
                void Tick()
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        if (!ready() || !GameWindows.IsCurrent(target) || GameWindows.Minimized(target))
                            throw new InvalidOperationException("Start countdown cancelled: game readiness or desktop ownership changed.");
                        if (!GetClientRect(target.Handle, out var rect)) throw new InvalidOperationException("Game bounds unavailable.");
                        var origin = new NativePoint();
                        if (!ClientToScreen(target.Handle, ref origin)) throw new InvalidOperationException("Game origin unavailable.");
                        var banner = BannerBounds(new Rectangle(origin.X, origin.Y, rect.Right, rect.Bottom));
                        // Explicitly show despite hidden CLI startup, in physical
                        // client coordinates. Never activate the overlay.
                        if (!SetWindowPos(window.Handle, new nint(-1), banner.X, banner.Y, banner.Width, banner.Height, 0x0010 | 0x0040))
                            throw new InvalidOperationException("Countdown placement failed.");
                        if (!IsWindowVisible(window.Handle)) throw new InvalidOperationException("Countdown is not visible.");
                        int remaining = Remaining(elapsed.Elapsed.TotalSeconds);
                        label.Text = $"Maelstrom starts in {remaining}…\nPress a key in W101 to stop";
                        if (!recorded && evidenceDirectory is not null)
                        {
                            window.Update();
                            using var bitmap = new Bitmap(window.Width, window.Height);
                            window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                            bitmap.Save(Path.Combine(evidenceDirectory, "countdown-render.png"));
                            GameExperiments.WriteAtomic(Path.Combine(evidenceDirectory, "countdown-placement.json"),
                                new { target = target.Handle.ToInt64(), clientOrigin = new { origin.X, origin.Y }, banner,
                                    visible = true, soundRequested = sound, renderOnly = true, desktopVisibilityConfirmed = false });
                            recorded = true;
                        }
                        if (remaining == 0) { completed = true; window.Close(); }
                    }
                    catch (Exception ex) { failure = ex; window.Close(); }
                }
                window.Shown += (_, _) =>
                {
                    elapsed.Start(); Tick();
                    if (failure is not null) return;
                    if (sound) { try { SystemSounds.Asterisk.Play(); } catch (Exception) { /* Visual cue remains authoritative. */ } }
                    timer.Start();
                };
                timer.Tick += (_, _) => Tick();
                Application.Run(window);
                if (failure is not null) result.TrySetException(failure);
                else if (completed) result.TrySetResult();
                else result.TrySetException(new InvalidOperationException("Start countdown closed before completion."));
            }
            catch (Exception ex) { result.TrySetException(ex); }
        }) { IsBackground = true, Name = "Maelstrom start notice" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return result.Task;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out Rect rectangle);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint window, ref NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
}
