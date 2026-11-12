using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Opt-in capture diagnostic using only a window created by this process.</summary>
internal static class ObserveVerification
{
    public static void Run(string reportPath)
    {
        using var target = new Form { Text = "Maelstrom capture verification", ClientSize = new Size(480, 320), BackColor = Color.CornflowerBlue, StartPosition = FormStartPosition.CenterScreen };
        target.Shown += async (_, _) =>
        {
            // Shell startup may request a hidden first window; this diagnostic needs a rendered surface.
            ShowWindow(target.Handle, 4);
            await Task.Delay(300);
            var checks = new Dictionary<string, object>();
            try
            {
                var identity = GameWindows.Read(target.Handle) ?? throw new InvalidOperationException("Cannot identify test window.");
                await using (var observer = new WindowObserver(identity))
                {
                    Bitmap? captured = null;
                    for (var i = 0; i < 40 && captured == null; i++)
                    {
                        await Task.Delay(100);
                        captured = await observer.ReadAsync();
                    }
                    using (captured)
                    {
                        if (captured == null) throw new InvalidOperationException("No test frame received.");
                        var center = captured.GetPixel(captured.Width / 2, captured.Height / 2);
                        if (Math.Abs(center.B - Color.CornflowerBlue.B) > 10 || Math.Abs(center.R - Color.CornflowerBlue.R) > 10)
                            throw new InvalidOperationException($"Unexpected test frame color: {center}");
                        captured.Save(reportPath + ".capture.png", System.Drawing.Imaging.ImageFormat.Png);
                        checks["selectedWindowPixels"] = true;
                        checks["frameSize"] = new[] { captured.Width, captured.Height };
                        var dataRoot = reportPath + ".data";
                        var recording = TeachingRecording.Start(dataRoot, "Capture test", "Maelstrom test window");
                        recording.AddFrame(captured);
                        recording.Finish();
                        recording.Label(0, "test-blue");
                        var reopened = TeachingRecording.Open(Path.Combine(recording.DirectoryPath, "recording.json"));
                        checks["teachingPack"] = reopened.ExportPack(dataRoot, "capture-test", "test-game", "Capture teaching check");
                        using var review = new TeachingReviewForm(reopened, dataRoot, null);
                        review.StartPosition = FormStartPosition.Manual;
                        review.Location = new Point(-30000, -30000);
                        review.Show();
                        using var preview = new Bitmap(review.Width, review.Height);
                        review.DrawToBitmap(preview, new Rectangle(Point.Empty, review.Size));
                        preview.Save(reportPath + ".teaching.png", System.Drawing.Imaging.ImageFormat.Png);
                        review.Close();
                        checks["recordLabelExport"] = true;
                    }
                    // Cover the target: the capture must still contain the selected window's pixels.
                    using var cover = new Form { Text = "Capture occlusion test", Bounds = target.Bounds, BackColor = Color.Red, StartPosition = FormStartPosition.Manual };
                    var coveredPaints = 0;
                    PaintEventHandler countCoveredPaint = (_, _) => coveredPaints++;
                    target.Paint += countCoveredPaint;
                    cover.Show();
                    target.BackColor = Color.MediumSeaGreen;
                    target.Refresh();
                    Bitmap? covered = null;
                    for (var i = 0; i < 30; i++)
                    {
                        await Task.Delay(100);
                        covered?.Dispose();
                        covered = await observer.ReadAsync();
                        if (covered != null && Math.Abs(covered.GetPixel(covered.Width / 2, covered.Height / 2).B - Color.MediumSeaGreen.B) <= 10) break;
                    }
                    target.Paint -= countCoveredPaint;
                    checks["coveredTargetPaintEvents"] = coveredPaints;
                    using var coveredFrame = covered;
                    if (covered == null || Math.Abs(covered.GetPixel(covered.Width / 2, covered.Height / 2).B - Color.MediumSeaGreen.B) > 10)
                    {
                        if (covered != null) covered.Save(reportPath + ".covered.png", System.Drawing.Imaging.ImageFormat.Png);
                        throw new InvalidOperationException("Window capture did not show the updated target during occlusion. Last center pixel: " + (covered == null ? "no frame" : covered.GetPixel(covered.Width / 2, covered.Height / 2).ToString()));
                    }
                    checks["occlusionSafe"] = true;
                    cover.Close();
                    using (var first = new Bitmap(reportPath + ".capture.png"))
                    {
                        var training = TeachingRecording.Start(reportPath + ".data", "Visual test", "Maelstrom test window");
                        training.AddFrame(first);
                        await Task.Delay(1100);
                        training.AddFrame(covered);
                        training.Finish();
                        training.Label(0, "blue-state");
                        training.Label(1, "green-state");
                        var pack = training.ExportPack(reportPath + ".data", "visual-test", "test-game", "Visual recognition test");
                        var recognizer = VisualStateRecognizer.Learn(Path.Combine(pack, "pack.json"));
                        target.BackColor = Color.CornflowerBlue;
                        target.Refresh();
                        var recognized = false;
                        for (var i = 0; i < 30 && !recognized; i++)
                        {
                            await Task.Delay(100);
                            using var live = await observer.ReadAsync();
                            if (live != null) recognized = recognizer.Predict(live).State == "blue-state";
                        }
                        if (!recognized) throw new InvalidOperationException("Visual recognition did not identify the live test state.");
                        checks["learnExportedPackRecognizeLiveState"] = true;
                        checks["visualPack"] = pack;
                    }
                    target.Width += 80;
                    var invalidated = false;
                    for (var i = 0; i < 30 && !invalidated; i++)
                    {
                        await Task.Delay(100);
                        try { using var frame = await observer.ReadAsync(); }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("size changed")) { invalidated = true; }
                    }
                    if (!invalidated) throw new InvalidOperationException("Resize did not invalidate capture.");
                    checks["resizeInvalidated"] = true;
                }
                await using (var reconnected = new WindowObserver(GameWindows.Read(target.Handle)!))
                {
                    await Task.Delay(400);
                    using var frame = await reconnected.ReadAsync();
                    if (frame == null) throw new InvalidOperationException("Reconnect did not produce a frame.");
                    checks["reconnect"] = true;
                    target.Hide();
                    try { using var unexpected = await reconnected.ReadAsync(); throw new Exception("Hidden target was not rejected."); }
                    catch (InvalidOperationException) { checks["hiddenTargetInvalidated"] = true; }
                }
                checks["passed"] = true;
            }
            catch (Exception ex) { checks["passed"] = false; checks["error"] = ex.ToString(); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllText(reportPath, JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
                target.Close();
            }
        };
        Application.Run(target);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);
}
