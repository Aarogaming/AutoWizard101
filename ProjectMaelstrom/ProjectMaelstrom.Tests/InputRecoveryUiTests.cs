using System.Diagnostics;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class InputRecoveryUiTests
{
    [Fact]
    public void OpeningAndClosingHiddenDialogStartsNoOperation()
    {
        OnSta(() =>
        {
            var workflow = new FakeWorkflow();
            using (var form = new InputRecoveryForm(workflow))
            {
                Assert.False(form.Visible);
                Assert.True(Button(form, "Stop routines for recovery").Enabled);
                Assert.False(Button(form, "Check control cleanup").Enabled);
                Assert.False(Button(form, "Restore game controls after verification").Enabled);
                Assert.True(form.BackColor.GetBrightness() < 0.5f);
                Wait(form.RestoreControlsAsync());
                Assert.Equal(0, workflow.StopCalls + workflow.CheckCalls + workflow.RestoreCalls);
            }
            Assert.True(workflow.Disposed);
            Assert.Equal(0, workflow.StopCalls + workflow.CheckCalls + workflow.RestoreCalls);
        });
    }

    [Fact]
    public void SeparateClicksStopCheckAndRestoreWithoutStartingRoutine()
    {
        OnSta(() =>
        {
            var workflow = new FakeWorkflow();
            using var form = new InputRecoveryForm(workflow);
            Wait(form.StopRoutinesAsync());
            Assert.True(Button(form, "Check control cleanup").Enabled);
            Assert.False(Button(form, "Restore game controls after verification").Enabled);
            Assert.Equal(0, workflow.RestoreCalls);
            Wait(form.CheckCleanupAsync());
            Assert.True(Button(form, "Restore game controls after verification").Enabled);
            Assert.Equal(0, workflow.RestoreCalls);
            Wait(form.RestoreControlsAsync());
            Assert.Equal(1, workflow.StopCalls); Assert.Equal(1, workflow.CheckCalls); Assert.Equal(1, workflow.RestoreCalls);
            Assert.False(Button(form, "Restore game controls after verification").Enabled);
            Assert.Contains("Nothing resumes automatically", Text(form));
            Assert.False(form.Visible);
        });
    }

    [Fact]
    public void RawFailureNeverReachesStatusOrKeepsRestoreEnabled()
    {
        OnSta(() =>
        {
            var workflow = new FakeWorkflow { CheckFailure = new IOException(@"F:\secret\game-input.lock failed") };
            using var form = new InputRecoveryForm(workflow);
            Wait(form.StopRoutinesAsync()); Wait(form.CheckCleanupAsync());
            Assert.Contains("This step could not finish", Text(form));
            Assert.DoesNotContain("secret", Text(form)); Assert.DoesNotContain("game-input.lock", Text(form));
            Assert.False(Button(form, "Restore game controls after verification").Enabled);
            Wait(form.RestoreControlsAsync()); Assert.Equal(0, workflow.RestoreCalls);
            Assert.True(Button(form, "Stop routines for recovery").Enabled);
        });
    }

    [Fact]
    public void ClosingCancelsButRetainsWorkflowUntilPendingStopEnds()
    {
        OnSta(() =>
        {
            var workflow = new FakeWorkflow { PendingStop = new(TaskCreationOptions.RunContinuationsAsynchronously) };
            using var form = new InputRecoveryForm(workflow);
            var pending = form.StopRoutinesAsync();
            Wait(form.StopRoutinesAsync()); Wait(form.CheckCleanupAsync());
            Assert.Equal(1, workflow.StopCalls); Assert.Equal(0, workflow.CheckCalls);
            var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
            typeof(InputRecoveryForm).GetMethod("OnFormClosing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(form, [closing]);
            Assert.True(closing.Cancel); Assert.True(workflow.LastToken.IsCancellationRequested);
            Assert.False(workflow.Disposed); Assert.False(pending.IsCompleted);
            Assert.False(Button(form, "Restore game controls after verification").Enabled);
            workflow.PendingStop.SetResult(new(RecoveryFlowStage.Blocked, "Stopped safely; controls remain paused.", false));
            Wait(pending);
            Assert.True(workflow.Disposed); Assert.Equal(0, workflow.RestoreCalls);
            Assert.False(form.Visible);
        });
    }

    [Fact]
    public void ForcedFormDisposalAlsoDefersWorkflowDisposalUntilPendingWorkEnds()
    {
        OnSta(() =>
        {
            var workflow = new FakeWorkflow { PendingStop = new(TaskCreationOptions.RunContinuationsAsynchronously) };
            var form = new InputRecoveryForm(workflow);
            var pending = form.StopRoutinesAsync(); form.Dispose();
            Assert.True(form.IsDisposed); Assert.False(workflow.Disposed);
            Assert.True(workflow.LastToken.IsCancellationRequested);
            workflow.PendingStop.SetResult(new(RecoveryFlowStage.Blocked, "Stopped safely.", false));
            Wait(pending);
            Assert.True(workflow.Disposed); Assert.Equal(0, workflow.RestoreCalls);
        });
    }

    [Fact]
    public void SharedEntryPointsExposeRecoveryWithoutOpeningIt()
    {
        OnSta(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "maelstrom-recovery-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                using var runs = new OperationRunsView(folder);
                using var settings = new ResearchSettingsForm(folder);
                Assert.NotNull(Button(runs, "Open game control recovery"));
                Assert.NotNull(Button(settings, "Open game control recovery"));
                Assert.False(settings.Visible);
            }
            finally { Directory.Delete(folder, true); }
        });
    }

    [Fact]
    public void OptionalHiddenRenderUsesOnlyFixedFakeRecoveryStates()
    {
        var output = Environment.GetEnvironmentVariable("MAELSTROM_RECOVERY_UI_RENDER");
        if (string.IsNullOrWhiteSpace(output)) return;
        var directory = Path.GetFullPath(output);
        OnSta(() =>
        {
            Directory.CreateDirectory(directory);
            foreach (var item in new[]
            {
                (Name: "recovery-ready.png", View: new RecoveryFlowView(RecoveryFlowStage.Ready,
                    "Cleanup checks passed. Choose Restore controls for one final check. No routine will restart automatically.", true, "synthetic-test-evidence")),
                (Name: "recovery-restored.png", View: new RecoveryFlowView(RecoveryFlowStage.Restored,
                    "Recovery is complete. Start a new routine when you are ready; your control settings and interrupted runs are unchanged.", false, "synthetic-test-evidence"))
            })
            {
                var workflow = new FakeWorkflow(item.View);
                using var form = new InputRecoveryForm(workflow);
                form.PerformLayout();
                // Hidden ancestors suppress FlowLayout's visible children. Detach
                // only the test's content panel, retaining the original client
                // bounds. Its controls can lay out locally while any handles stay
                // in WinForms' hidden parking parent; no top-level window exists.
                using var body = form.Controls.OfType<FlowLayoutPanel>().Single();
                var bounds = body.Bounds;
                form.Controls.Remove(body);
                body.Dock = DockStyle.None; body.Bounds = bounds;
                foreach (var control in Descendants(body).Reverse()) control.PerformLayout();
                body.PerformLayout();
                foreach (var control in Descendants(body)) control.PerformLayout();
                body.PerformLayout();
                Assert.True(body.Visible);
                Assert.False(form.IsHandleCreated);
                AssertRecoveryLayout(body);
                using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height + 30);
                Assert.False(form.Visible);
                // Offscreen composite, not a desktop screenshot: a hidden Form's
                // WM_PRINT skips its children. Paint each real control directly,
                // preserving its layout position and parent clipping instead.
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(form.BackColor);
                    var clip = new Rectangle(Point.Empty, form.ClientSize);
                    CompositeControl(body, graphics, Point.Empty, clip);
                    using var caption = new Font("Segoe UI", 9);
                    TextRenderer.DrawText(graphics, "Offscreen composite · fake state · no window shown", caption,
                        new Rectangle(10, form.ClientSize.Height + 4, form.ClientSize.Width - 20, 22), Color.LightGray,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                }
                int contentPixels = 0;
                for (int y = 0; y < form.ClientSize.Height; y += 2)
                    for (int x = 0; x < form.ClientSize.Width; x += 2)
                        if (bitmap.GetPixel(x, y).GetBrightness() > 0.5f) contentPixels++;
                Assert.True(contentPixels > 100, "Offscreen recovery preview contains no readable client content.");
                bitmap.Save(Path.Combine(directory, item.Name), System.Drawing.Imaging.ImageFormat.Png);
                Assert.False(form.Visible);
                Assert.False(form.IsHandleCreated);
                Assert.Equal(0, workflow.StopCalls + workflow.CheckCalls + workflow.RestoreCalls);
            }
        });
    }

    private static void AssertRecoveryLayout(FlowLayoutPanel body)
    {
        var rows = body.Controls.Cast<Control>().ToArray();
        Assert.All(rows, row => Assert.True(row.Visible && row.Width > 0 && row.Height > 0,
            "A recovery row was omitted from offscreen layout."));
        for (int i = 0; i < rows.Length; i++)
            for (int j = i + 1; j < rows.Length; j++)
                Assert.False(rows[i].Bounds.IntersectsWith(rows[j].Bounds),
                    $"Recovery rows overlap: {rows[i].Text} / {rows[j].Text}");
        var actions = body.Controls.OfType<FlowLayoutPanel>().Single();
        Assert.True(body.ClientRectangle.Contains(actions.Bounds), "The action row is clipped.");
        var buttons = actions.Controls.OfType<Button>().ToArray();
        Assert.Equal(3, buttons.Length);
        Assert.All(buttons, button => Assert.True(button.Visible && button.Width > 0 && button.Height > 0 &&
            actions.ClientRectangle.Contains(button.Bounds), "An action button is omitted or clipped."));
        for (int i = 0; i < buttons.Length; i++)
            for (int j = i + 1; j < buttons.Length; j++)
                Assert.False(buttons[i].Bounds.IntersectsWith(buttons[j].Bounds), "Recovery action buttons overlap.");
    }

    private static void CompositeControl(Control control, Graphics graphics, Point parentOrigin, Rectangle parentClip)
    {
        if (control.Width <= 0 || control.Height <= 0) return;
        var origin = new Point(parentOrigin.X + control.Left, parentOrigin.Y + control.Top);
        var clip = Rectangle.Intersect(parentClip, new Rectangle(origin, control.Size));
        if (clip.IsEmpty) return;
        var saved = graphics.Save();
        try
        {
            graphics.SetClip(clip);
            using var part = new Bitmap(control.Width, control.Height);
            control.DrawToBitmap(part, new Rectangle(Point.Empty, control.Size));
            graphics.DrawImageUnscaled(part, origin);
            foreach (Control child in control.Controls.Cast<Control>().Reverse())
                CompositeControl(child, graphics, origin, clip);
        }
        finally { graphics.Restore(saved); }
    }

    private sealed class FakeWorkflow : IInputRecoveryWorkflow
    {
        public RecoveryFlowView State { get; private set; }
        public FakeWorkflow(RecoveryFlowView? initial = null) => State = initial ?? new(RecoveryFlowStage.Initial, "Stop routines before checking cleanup.", false);
        public int StopCalls, CheckCalls, RestoreCalls;
        public bool Disposed;
        public CancellationToken LastToken;
        public TaskCompletionSource<RecoveryFlowView>? PendingStop;
        public Exception? CheckFailure;
        public async Task<RecoveryFlowView> StopAsync(CancellationToken token)
        {
            StopCalls++; LastToken = token;
            State = new(RecoveryFlowStage.Stopping, "Waiting for routines to stop.", false);
            return State = PendingStop is null ? new(RecoveryFlowStage.Ready, "Routines stopped. Check cleanup next.", false) : await PendingStop.Task;
        }
        public Task<RecoveryFlowView> CheckAsync(CancellationToken token)
        {
            CheckCalls++; LastToken = token;
            if (CheckFailure is not null) throw CheckFailure;
            State = new(RecoveryFlowStage.Ready, "Cleanup checked. Restore controls when ready.", true);
            return Task.FromResult(State);
        }
        public Task<RecoveryFlowView> RestoreAsync(CancellationToken token)
        {
            RestoreCalls++; LastToken = token;
            State = new(RecoveryFlowStage.Restored, "Controls restored. No routines restarted.", false);
            return Task.FromResult(State);
        }
        public void Dispose() => Disposed = true;
    }

    private static IEnumerable<Control> Descendants(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));
    private static Button Button(Control parent, string name) => Descendants(parent).OfType<Button>().Single(button => button.AccessibleName == name);
    private static string Text(Control parent) => string.Join("\n", Descendants(parent).Select(control => control.Text));
    private static void Wait(Task task)
    {
        var timer = Stopwatch.StartNew();
        while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(5)) { Application.DoEvents(); Thread.Sleep(1); }
        Assert.True(task.IsCompleted, "Hidden recovery operation timed out."); task.GetAwaiter().GetResult();
    }
    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Hidden recovery UI test timed out.");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
